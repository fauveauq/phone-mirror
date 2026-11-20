using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PhoneMirror.Core;

public enum PacketKind
{
    /// <summary>Début d'une session de capture (au démarrage et à chaque rotation).</summary>
    Session,
    /// <summary>Paramètres du codec (SPS/PPS), à fournir au décodeur avec l'image suivante.</summary>
    Config,
    /// <summary>Image encodée.</summary>
    Frame,
}

public readonly record struct StreamPacket(PacketKind Kind, int Width, int Height, bool IsKeyFrame, long Pts, int Length);

/// <summary>
/// Flux vidéo du serveur scrcpy (protocole de scrcpy 4.x/5.x, voir doc/develop.md du projet scrcpy).
/// La lecture des paquets est bloquante et doit se faire depuis un thread dédié.
/// </summary>
public sealed class ScrcpyVideoStream : IDisposable
{
    private const uint CodecH264 = 0x68323634; // "h264"
    private const int DeviceNameLength = 64;
    private const int MaxPacketSize = 64 * 1024 * 1024;

    private const ulong FlagSession = 1UL << 63;
    private const ulong FlagConfig = 1UL << 62;
    private const ulong FlagKeyFrame = 1UL << 61;
    private const ulong PtsMask = FlagKeyFrame - 1;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly byte[] _header = new byte[12];
    private byte[] _payload = new byte[1024 * 1024];

    public string DeviceName { get; }

    private ScrcpyVideoStream(TcpClient client, NetworkStream stream, string deviceName)
    {
        _client = client;
        _stream = stream;
        DeviceName = deviceName;
    }

    /// <summary>
    /// Lit les métadonnées (nom de l'appareil, codec) sur une connexion vidéo dont l'octet factice
    /// a déjà été reçu. Voir <see cref="ScrcpyConnector"/> pour l'ouverture des connexions.
    /// </summary>
    internal static async Task<ScrcpyVideoStream> HandshakeAsync(TcpClient client, CancellationToken ct)
    {
        NetworkStream stream = client.GetStream();
        var name = new byte[DeviceNameLength];
        await stream.ReadExactlyAsync(name, ct).ConfigureAwait(false);
        int nameLength = Array.IndexOf(name, (byte)0);
        string deviceName = Encoding.UTF8.GetString(name, 0, nameLength < 0 ? name.Length : nameLength);

        var codec = new byte[4];
        await stream.ReadExactlyAsync(codec, ct).ConfigureAwait(false);
        uint codecId = BinaryPrimitives.ReadUInt32BigEndian(codec);
        if (codecId != CodecH264)
            throw new InvalidDataException($"Codec vidéo inattendu (0x{codecId:X8}) : H.264 attendu.");

        return new ScrcpyVideoStream(client, stream, deviceName);
    }

    /// <summary>Lit le paquet suivant (bloquant). Son contenu est accessible via <see cref="GetPayload"/>.</summary>
    public StreamPacket ReadPacket()
    {
        _stream.ReadExactly(_header);
        ulong first = BinaryPrimitives.ReadUInt64BigEndian(_header);

        if ((first & FlagSession) != 0)
        {
            int width = BinaryPrimitives.ReadInt32BigEndian(_header.AsSpan(4));
            int height = BinaryPrimitives.ReadInt32BigEndian(_header.AsSpan(8));
            return new StreamPacket(PacketKind.Session, width, height, false, 0, 0);
        }

        int size = BinaryPrimitives.ReadInt32BigEndian(_header.AsSpan(8));
        if (size <= 0 || size > MaxPacketSize)
            throw new InvalidDataException($"Taille de paquet invalide : {size} octets.");

        if (_payload.Length < size)
            _payload = new byte[Math.Max(size, _payload.Length * 2)];
        _stream.ReadExactly(_payload, 0, size);

        PacketKind kind = (first & FlagConfig) != 0 ? PacketKind.Config : PacketKind.Frame;
        return new StreamPacket(kind, 0, 0, (first & FlagKeyFrame) != 0, (long)(first & PtsMask), size);
    }

    /// <summary>Contenu du dernier paquet lu, valable jusqu'au prochain appel à <see cref="ReadPacket"/>.</summary>
    public ReadOnlySpan<byte> GetPayload(in StreamPacket packet) => _payload.AsSpan(0, packet.Length);

    /// <summary>Ferme la connexion ; une lecture en cours se termine alors par une exception.</summary>
    public void Dispose()
    {
        _stream.Dispose();
        _client.Dispose();
    }
}
