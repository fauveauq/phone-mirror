using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace PhoneMirror.Core;

/// <summary>Actions tactiles Android (MotionEvent).</summary>
public enum MotionAction : byte { Down = 0, Up = 1, Move = 2 }

/// <summary>Actions clavier Android (KeyEvent).</summary>
public enum KeyAction : byte { Down = 0, Up = 1 }

/// <summary>Touche à presser sur le téléphone avant de lire son presse-papiers.</summary>
public enum CopyKey : byte { None = 0, Copy = 1, Cut = 2 }

/// <summary>Commandes sans paramètre du protocole de contrôle.</summary>
public enum PhoneCommand : byte
{
    ExpandNotificationPanel = 5,
    ExpandSettingsPanel = 6,
    CollapsePanels = 7,
    RotateDevice = 11,
}

/// <summary>
/// Canal de contrôle du serveur scrcpy (format de scrcpy v5.0.1, voir ControlMessageReader.java) :
/// envoie toucher, défilement, touches, texte et presse-papiers, et reçoit le presse-papiers du téléphone.
/// Les envois sont synchrones et immédiats (messages de quelques octets, Nagle désactivé), sans file d'attente.
/// </summary>
public sealed class ScrcpyControlChannel : IDisposable
{
    private const byte TypeInjectKeycode = 0;
    private const byte TypeInjectText = 1;
    private const byte TypeInjectTouch = 2;
    private const byte TypeInjectScroll = 3;
    private const byte TypeBackOrScreenOn = 4;
    private const byte TypeGetClipboard = 8;
    private const byte TypeSetClipboard = 9;
    private const byte TypeSetDisplayPower = 10;

    private const byte DeviceMessageClipboard = 0;
    private const byte DeviceMessageAckClipboard = 1;
    private const byte DeviceMessageUhidOutput = 2;

    private const int InjectTextMaxBytes = 300;
    private const int ClipboardMaxBytes = (1 << 18) - 14;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly Action<string> _log;
    private readonly Lock _writeGate = new();
    private readonly Thread _reader;
    private volatile bool _disposed;
    private bool _writeFailed;

    /// <summary>Texte copié sur le téléphone. Déclenché depuis un thread de lecture.</summary>
    public event Action<string>? ClipboardReceived;

    public ScrcpyControlChannel(TcpClient client, Action<string> log)
    {
        _client = client;
        _stream = client.GetStream();
        _log = log;
        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "PhoneMirror control" };
        _reader.Start();
    }

    public void InjectKeycode(KeyAction action, int keycode, int repeat, int metaState)
    {
        Span<byte> msg = stackalloc byte[14];
        msg[0] = TypeInjectKeycode;
        msg[1] = (byte)action;
        BinaryPrimitives.WriteInt32BigEndian(msg[2..], keycode);
        BinaryPrimitives.WriteInt32BigEndian(msg[6..], repeat);
        BinaryPrimitives.WriteInt32BigEndian(msg[10..], metaState);
        Send(msg);
    }

    /// <summary>Appui puis relâchement d'une touche.</summary>
    public void PressKey(int keycode)
    {
        InjectKeycode(KeyAction.Down, keycode, 0, 0);
        InjectKeycode(KeyAction.Up, keycode, 0, 0);
    }

    public void InjectText(string text)
    {
        byte[] utf8 = TruncatedUtf8(text, InjectTextMaxBytes);
        var msg = new byte[5 + utf8.Length];
        msg[0] = TypeInjectText;
        BinaryPrimitives.WriteInt32BigEndian(msg.AsSpan(1), utf8.Length);
        utf8.CopyTo(msg, 5);
        Send(msg);
    }

    /// <summary>Toucher à la position (x, y) d'une vidéo de taille width × height.</summary>
    public void InjectTouch(MotionAction action, long pointerId, VideoPoint point, float pressure, int actionButton, int buttons)
    {
        Span<byte> msg = stackalloc byte[32];
        msg[0] = TypeInjectTouch;
        msg[1] = (byte)action;
        BinaryPrimitives.WriteInt64BigEndian(msg[2..], pointerId);
        WritePosition(msg[10..], point);
        BinaryPrimitives.WriteUInt16BigEndian(msg[22..], ToU16FixedPoint(pressure));
        BinaryPrimitives.WriteInt32BigEndian(msg[24..], actionButton);
        BinaryPrimitives.WriteInt32BigEndian(msg[28..], buttons);
        Send(msg);
    }

    /// <summary>Défilement ; une unité correspond à un cran de molette (plage -16 à 16).</summary>
    public void InjectScroll(VideoPoint point, float horizontal, float vertical)
    {
        Span<byte> msg = stackalloc byte[21];
        msg[0] = TypeInjectScroll;
        WritePosition(msg[1..], point);
        BinaryPrimitives.WriteInt16BigEndian(msg[13..], ToScrollFixedPoint(horizontal));
        BinaryPrimitives.WriteInt16BigEndian(msg[15..], ToScrollFixedPoint(vertical));
        BinaryPrimitives.WriteInt32BigEndian(msg[17..], 0);
        Send(msg);
    }

    /// <summary>Retour, ou allume l'écran s'il est éteint.</summary>
    public void BackOrScreenOn(KeyAction action) => Send([TypeBackOrScreenOn, (byte)action]);

    /// <summary>Demande au téléphone de copier (ou couper) la sélection ; le texte revient par <see cref="ClipboardReceived"/>.</summary>
    public void GetClipboard(CopyKey copyKey) => Send([TypeGetClipboard, (byte)copyKey]);

    /// <summary>Remplace le presse-papiers du téléphone, et colle si <paramref name="paste"/>.</summary>
    public void SetClipboard(string text, bool paste)
    {
        byte[] utf8 = TruncatedUtf8(text, ClipboardMaxBytes);
        var msg = new byte[14 + utf8.Length];
        msg[0] = TypeSetClipboard;
        BinaryPrimitives.WriteInt64BigEndian(msg.AsSpan(1), 0); // pas d'accusé de réception
        msg[9] = paste ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt32BigEndian(msg.AsSpan(10), utf8.Length);
        utf8.CopyTo(msg, 14);
        Send(msg);
    }

    /// <summary>Éteint ou rallume l'écran du téléphone sans couper la recopie.</summary>
    public void SetDisplayPower(bool on) => Send([TypeSetDisplayPower, on ? (byte)1 : (byte)0]);

    public void Send(PhoneCommand command) => Send([(byte)command]);

    private void Send(ReadOnlySpan<byte> message)
    {
        if (_disposed) return;
        lock (_writeGate)
        {
            try
            {
                _stream.Write(message);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // La perte de connexion est signalée par le flux vidéo ; on ne journalise qu'une fois.
                if (!_writeFailed && !_disposed) _log("Envoi de contrôle impossible : " + ex.Message);
                _writeFailed = true;
            }
        }
    }

    private void ReadLoop()
    {
        try
        {
            Span<byte> header = stackalloc byte[8];
            while (!_disposed)
            {
                _stream.ReadExactly(header[..1]);
                switch (header[0])
                {
                    case DeviceMessageClipboard:
                        _stream.ReadExactly(header[..4]);
                        int length = BinaryPrimitives.ReadInt32BigEndian(header);
                        if (length < 0 || length > ClipboardMaxBytes)
                            throw new InvalidDataException($"Presse-papiers de taille invalide : {length} octets.");
                        var text = new byte[length];
                        _stream.ReadExactly(text);
                        ClipboardReceived?.Invoke(Encoding.UTF8.GetString(text));
                        break;

                    case DeviceMessageAckClipboard:
                        _stream.ReadExactly(header[..8]);
                        break;

                    case DeviceMessageUhidOutput:
                        _stream.ReadExactly(header[..4]);
                        var skipped = new byte[BinaryPrimitives.ReadUInt16BigEndian(header[2..])];
                        _stream.ReadExactly(skipped);
                        break;

                    default:
                        throw new InvalidDataException($"Message inconnu du téléphone (type {header[0]}).");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidDataException)
        {
            if (!_disposed) _log("Lecture du canal de contrôle arrêtée : " + ex.Message);
        }
    }

    private static void WritePosition(Span<byte> target, VideoPoint point)
    {
        BinaryPrimitives.WriteInt32BigEndian(target, point.X);
        BinaryPrimitives.WriteInt32BigEndian(target[4..], point.Y);
        BinaryPrimitives.WriteUInt16BigEndian(target[8..], (ushort)point.VideoWidth);
        BinaryPrimitives.WriteUInt16BigEndian(target[10..], (ushort)point.VideoHeight);
    }

    private static ushort ToU16FixedPoint(float value) =>
        value >= 1f ? ushort.MaxValue : (ushort)(Math.Max(0f, value) * 65536f);

    // Le serveur décode sur [-1, 1] puis multiplie par 16.
    private static short ToScrollFixedPoint(float value) =>
        (short)Math.Clamp((int)(value / 16f * 32768f), short.MinValue, short.MaxValue);

    /// <summary>Encode en UTF-8 en coupant au plus près de la limite, sans couper un caractère.</summary>
    private static byte[] TruncatedUtf8(string text, int maxBytes)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(text);
        if (utf8.Length <= maxBytes) return utf8;
        int length = maxBytes;
        while (length > 0 && (utf8[length] & 0xC0) == 0x80) length--;
        return utf8[..length];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stream.Dispose(); // débloque la lecture en cours
        _client.Dispose();
    }
}

/// <summary>Position dans la vidéo, avec la taille de la vidéo à laquelle elle se rapporte.</summary>
public readonly record struct VideoPoint(int X, int Y, int VideoWidth, int VideoHeight);
