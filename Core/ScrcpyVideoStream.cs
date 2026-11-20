using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace PhoneMirror.Core;

/// <summary>
/// Flux vidéo du serveur scrcpy (protocole de scrcpy 4.x/5.x, voir doc/develop.md du projet scrcpy).
/// La lecture des paquets est bloquante et doit se faire depuis un thread dédié.
/// </summary>
public sealed class ScrcpyVideoStream : IDisposable
{
    private const uint CodecH264 = 0x68323634; // "h264"
    private const int DeviceNameLength = 64;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly MediaPacketReader _reader;

    public string DeviceName { get; }

    private ScrcpyVideoStream(TcpClient client, NetworkStream stream, string deviceName)
    {
        _client = client;
        _stream = stream;
        _reader = new MediaPacketReader(stream);
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
    public StreamPacket ReadPacket() => _reader.ReadPacket();

    /// <summary>Contenu du dernier paquet lu, valable jusqu'au prochain appel à <see cref="ReadPacket"/>.</summary>
    public ReadOnlySpan<byte> GetPayload(in StreamPacket packet) => _reader.GetPayload(packet);

    /// <summary>Ferme la connexion ; une lecture en cours se termine alors par une exception.</summary>
    public void Dispose()
    {
        _stream.Dispose();
        _client.Dispose();
    }
}
