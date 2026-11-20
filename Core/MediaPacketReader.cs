using System.Buffers.Binary;

namespace PhoneMirror.Core;

public enum PacketKind
{
    /// <summary>Début d'une session de capture vidéo (au démarrage et à chaque rotation).</summary>
    Session,
    /// <summary>Paramètres du codec (SPS/PPS pour H.264, OpusHead pour Opus).</summary>
    Config,
    /// <summary>Image ou bloc audio encodé.</summary>
    Frame,
}

public readonly record struct StreamPacket(PacketKind Kind, int Width, int Height, bool IsKeyFrame, long Pts, int Length);

/// <summary>
/// Lit les paquets d'un flux scrcpy (vidéo ou audio) : en-tête de 12 octets puis contenu.
/// Bit 63 = paquet de session (vidéo uniquement), bit 62 = configuration, bit 61 = image clé.
/// Lecture bloquante, depuis un thread dédié ; un seul tampon réutilisé, sans allocation par paquet.
/// </summary>
internal sealed class MediaPacketReader(Stream stream)
{
    private const int MaxPacketSize = 64 * 1024 * 1024;

    private const ulong FlagSession = 1UL << 63;
    private const ulong FlagConfig = 1UL << 62;
    private const ulong FlagKeyFrame = 1UL << 61;
    private const ulong PtsMask = FlagKeyFrame - 1;

    private readonly byte[] _header = new byte[12];
    private byte[] _payload = new byte[64 * 1024];

    public StreamPacket ReadPacket()
    {
        stream.ReadExactly(_header);
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
        stream.ReadExactly(_payload, 0, size);

        PacketKind kind = (first & FlagConfig) != 0 ? PacketKind.Config : PacketKind.Frame;
        return new StreamPacket(kind, 0, 0, (first & FlagKeyFrame) != 0, (long)(first & PtsMask), size);
    }

    /// <summary>Contenu du dernier paquet lu, valable jusqu'au prochain appel à <see cref="ReadPacket"/>.</summary>
    public ReadOnlySpan<byte> GetPayload(in StreamPacket packet) => _payload.AsSpan(0, packet.Length);
}
