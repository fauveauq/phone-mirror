using System.Buffers.Binary;
using System.Text;
using PhoneMirror.Core;

namespace PhoneMirror.Tests;

public class ScrcpyVideoStreamTests
{
    [Fact]
    public async Task Handshake_reads_device_name_and_accepts_h264()
    {
        using var pair = await LoopbackPair.CreateAsync();
        await pair.ServerStream.WriteAsync(DeviceMeta("SM-G980F", 0x68323634));

        using ScrcpyVideoStream stream = await ScrcpyVideoStream.HandshakeAsync(pair.Client, CancellationToken.None);

        Assert.Equal("SM-G980F", stream.DeviceName);
    }

    [Fact]
    public async Task Handshake_rejects_other_codecs()
    {
        using var pair = await LoopbackPair.CreateAsync();
        await pair.ServerStream.WriteAsync(DeviceMeta("Pixel", 0x68323635)); // h265

        await Assert.ThrowsAsync<InvalidDataException>(
            () => ScrcpyVideoStream.HandshakeAsync(pair.Client, CancellationToken.None));
    }

    [Fact]
    public async Task ReadPacket_decodes_session_config_and_frame_packets()
    {
        using var pair = await LoopbackPair.CreateAsync();
        await pair.ServerStream.WriteAsync(DeviceMeta("Phone", 0x68323634));
        using ScrcpyVideoStream stream = await ScrcpyVideoStream.HandshakeAsync(pair.Client, CancellationToken.None);

        await pair.ServerStream.WriteAsync(SessionPacket(864, 1920));
        await pair.ServerStream.WriteAsync(MediaPacket(config: true, keyFrame: false, pts: 0, [1, 2, 3]));
        await pair.ServerStream.WriteAsync(MediaPacket(config: false, keyFrame: true, pts: 123_456, [4, 5, 6, 7]));

        StreamPacket session = stream.ReadPacket();
        Assert.Equal(PacketKind.Session, session.Kind);
        Assert.Equal((864, 1920), (session.Width, session.Height));

        StreamPacket config = stream.ReadPacket();
        Assert.Equal(PacketKind.Config, config.Kind);
        Assert.Equal([1, 2, 3], stream.GetPayload(config).ToArray());

        StreamPacket frame = stream.ReadPacket();
        Assert.Equal(PacketKind.Frame, frame.Kind);
        Assert.True(frame.IsKeyFrame);
        Assert.Equal(123_456, frame.Pts);
        Assert.Equal([4, 5, 6, 7], stream.GetPayload(frame).ToArray());
    }

    [Fact]
    public async Task ReadPacket_rejects_invalid_sizes()
    {
        using var pair = await LoopbackPair.CreateAsync();
        await pair.ServerStream.WriteAsync(DeviceMeta("Phone", 0x68323634));
        using ScrcpyVideoStream stream = await ScrcpyVideoStream.HandshakeAsync(pair.Client, CancellationToken.None);

        var header = new byte[12];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(8), -1);
        await pair.ServerStream.WriteAsync(header);

        Assert.Throws<InvalidDataException>(() => stream.ReadPacket());
    }

    private static byte[] DeviceMeta(string name, uint codec)
    {
        var data = new byte[64 + 4];
        Encoding.UTF8.GetBytes(name).CopyTo(data, 0);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(64), codec);
        return data;
    }

    private static byte[] SessionPacket(int width, int height)
    {
        var data = new byte[12];
        data[0] = 0x80; // bit 63 : paquet de session
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(8), height);
        return data;
    }

    private static byte[] MediaPacket(bool config, bool keyFrame, long pts, byte[] payload)
    {
        ulong first = (ulong)pts | (config ? 1UL << 62 : 0) | (keyFrame ? 1UL << 61 : 0);
        var data = new byte[12 + payload.Length];
        BinaryPrimitives.WriteUInt64BigEndian(data, first);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(8), payload.Length);
        payload.CopyTo(data, 12);
        return data;
    }
}
