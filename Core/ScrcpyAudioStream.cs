using System.Buffers.Binary;
using System.Net.Sockets;

namespace PhoneMirror.Core;

/// <summary>État du son annoncé par le téléphone au début du flux audio.</summary>
public enum AudioAvailability
{
    /// <summary>Son en Opus : la lecture peut commencer.</summary>
    Available,
    /// <summary>Le téléphone ne peut pas capturer le son (Android 10 ou antérieur, application qui l'interdit…).</summary>
    Disabled,
    /// <summary>Erreur de configuration côté téléphone, ou codec inattendu.</summary>
    Error,
}

/// <summary>
/// Flux audio du serveur scrcpy : identifiant du codec (u32), puis des paquets au même format que la vidéo,
/// sans paquet de session. Lecture bloquante, depuis un thread dédié.
/// </summary>
public sealed class ScrcpyAudioStream : IDisposable
{
    private const uint CodecOpus = 0x6F707573; // "opus"
    private const uint CodeDisabled = 0;
    private const uint CodeError = 1;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly MediaPacketReader _reader;

    public ScrcpyAudioStream(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        _reader = new MediaPacketReader(_stream);
    }

    /// <summary>Lit l'en-tête du flux (bloquant : le téléphone l'envoie quand la capture démarre).</summary>
    public AudioAvailability ReadHeader()
    {
        Span<byte> codec = stackalloc byte[4];
        _stream.ReadExactly(codec);
        return BinaryPrimitives.ReadUInt32BigEndian(codec) switch
        {
            CodecOpus => AudioAvailability.Available,
            CodeDisabled => AudioAvailability.Disabled,
            _ => AudioAvailability.Error, // CodeError ou codec non demandé
        };
    }

    public StreamPacket ReadPacket() => _reader.ReadPacket();

    public ReadOnlySpan<byte> GetPayload(in StreamPacket packet) => _reader.GetPayload(packet);

    public void Dispose()
    {
        _stream.Dispose();
        _client.Dispose();
    }
}
