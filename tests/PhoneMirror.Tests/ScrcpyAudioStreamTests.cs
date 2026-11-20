using System.Buffers.Binary;
using PhoneMirror.Core;

namespace PhoneMirror.Tests;

public class ScrcpyAudioStreamTests
{
    [Theory]
    [InlineData(0x6F707573u, AudioAvailability.Available)] // "opus"
    [InlineData(0u, AudioAvailability.Disabled)]           // téléphone sans capture audio
    [InlineData(1u, AudioAvailability.Error)]              // erreur de configuration
    [InlineData(0x00616163u, AudioAvailability.Error)]     // "aac" : non demandé
    public async Task ReadHeader_interprets_codec_and_special_codes(uint code, AudioAvailability expected)
    {
        using var pair = await LoopbackPair.CreateAsync();
        var header = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(header, code);
        await pair.ServerStream.WriteAsync(header);

        using var stream = new ScrcpyAudioStream(pair.Client);

        Assert.Equal(expected, stream.ReadHeader());
    }

    [Fact]
    public async Task ReadPacket_reads_audio_frames()
    {
        using var pair = await LoopbackPair.CreateAsync();
        var data = new byte[4 + 12 + 3];
        BinaryPrimitives.WriteUInt32BigEndian(data, 0x6F707573);
        BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(4), 20_000); // PTS, sans indicateur
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(12), 3);
        data[16] = 7; data[17] = 8; data[18] = 9;
        await pair.ServerStream.WriteAsync(data);

        using var stream = new ScrcpyAudioStream(pair.Client);
        stream.ReadHeader();
        StreamPacket packet = stream.ReadPacket();

        Assert.Equal(PacketKind.Frame, packet.Kind);
        Assert.Equal(20_000, packet.Pts);
        Assert.Equal([7, 8, 9], stream.GetPayload(packet).ToArray());
    }
}
