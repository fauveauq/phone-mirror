using System.Buffers.Binary;
using System.Text;
using PhoneMirror.Core;

namespace PhoneMirror.Tests;

/// <summary>Vérifie le format binaire attendu par ControlMessageReader.java (scrcpy v5.0.1).</summary>
public class ScrcpyControlChannelTests
{
    [Fact]
    public async Task InjectTouch_writes_32_bytes_in_scrcpy_layout()
    {
        using var pair = await LoopbackPair.CreateAsync();
        using var channel = new ScrcpyControlChannel(pair.Client, _ => { });

        channel.InjectTouch(MotionAction.Down, -1, new VideoPoint(100, 200, 864, 1920), 1f, 1, 1);
        byte[] msg = await pair.ReadFromServerAsync(32);

        Assert.Equal(2, msg[0]); // TYPE_INJECT_TOUCH_EVENT
        Assert.Equal(0, msg[1]); // ACTION_DOWN
        Assert.Equal(-1L, BinaryPrimitives.ReadInt64BigEndian(msg.AsSpan(2)));
        Assert.Equal(100, BinaryPrimitives.ReadInt32BigEndian(msg.AsSpan(10)));
        Assert.Equal(200, BinaryPrimitives.ReadInt32BigEndian(msg.AsSpan(14)));
        Assert.Equal(864, BinaryPrimitives.ReadUInt16BigEndian(msg.AsSpan(18)));
        Assert.Equal(1920, BinaryPrimitives.ReadUInt16BigEndian(msg.AsSpan(20)));
        Assert.Equal(0xFFFF, BinaryPrimitives.ReadUInt16BigEndian(msg.AsSpan(22))); // pression 1.0
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(msg.AsSpan(24)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(msg.AsSpan(28)));
    }

    [Fact]
    public async Task InjectKeycode_writes_14_bytes()
    {
        using var pair = await LoopbackPair.CreateAsync();
        using var channel = new ScrcpyControlChannel(pair.Client, _ => { });

        channel.InjectKeycode(KeyAction.Up, 66, 2, 0x3000);
        byte[] msg = await pair.ReadFromServerAsync(14);

        Assert.Equal(0, msg[0]);
        Assert.Equal(1, msg[1]);
        Assert.Equal(66, BinaryPrimitives.ReadInt32BigEndian(msg.AsSpan(2)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(msg.AsSpan(6)));
        Assert.Equal(0x3000, BinaryPrimitives.ReadInt32BigEndian(msg.AsSpan(10)));
    }

    [Fact]
    public async Task InjectScroll_encodes_values_on_the_16_unit_scale()
    {
        using var pair = await LoopbackPair.CreateAsync();
        using var channel = new ScrcpyControlChannel(pair.Client, _ => { });

        channel.InjectScroll(new VideoPoint(1, 2, 3, 4), -1f, 3f);
        byte[] msg = await pair.ReadFromServerAsync(21);

        Assert.Equal(3, msg[0]);
        // Le serveur décode valeur / 2^15 puis multiplie par 16.
        Assert.Equal(-1f, BinaryPrimitives.ReadInt16BigEndian(msg.AsSpan(13)) / 32768f * 16f, 2);
        Assert.Equal(3f, BinaryPrimitives.ReadInt16BigEndian(msg.AsSpan(15)) / 32768f * 16f, 2);
    }

    [Fact]
    public async Task InjectText_writes_utf8_with_length_prefix()
    {
        using var pair = await LoopbackPair.CreateAsync();
        using var channel = new ScrcpyControlChannel(pair.Client, _ => { });

        channel.InjectText("café");
        byte[] header = await pair.ReadFromServerAsync(5);
        int length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(1));
        byte[] text = await pair.ReadFromServerAsync(length);

        Assert.Equal(1, header[0]);
        Assert.Equal("café", Encoding.UTF8.GetString(text));
    }

    [Fact]
    public async Task SetClipboard_requests_paste_without_acknowledgement()
    {
        using var pair = await LoopbackPair.CreateAsync();
        using var channel = new ScrcpyControlChannel(pair.Client, _ => { });

        channel.SetClipboard("abc", paste: true);
        byte[] msg = await pair.ReadFromServerAsync(14 + 3);

        Assert.Equal(9, msg[0]);
        Assert.Equal(0L, BinaryPrimitives.ReadInt64BigEndian(msg.AsSpan(1)));
        Assert.Equal(1, msg[9]);
        Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(msg.AsSpan(10)));
        Assert.Equal("abc", Encoding.UTF8.GetString(msg, 14, 3));
    }

    [Fact]
    public async Task Clipboard_from_phone_raises_event()
    {
        using var pair = await LoopbackPair.CreateAsync();
        using var channel = new ScrcpyControlChannel(pair.Client, _ => { });
        var received = new TaskCompletionSource<string>();
        channel.ClipboardReceived += text => received.TrySetResult(text);

        byte[] text = Encoding.UTF8.GetBytes("copié");
        var msg = new byte[5 + text.Length];
        msg[0] = 0; // DeviceMessage.TYPE_CLIPBOARD
        BinaryPrimitives.WriteInt32BigEndian(msg.AsSpan(1), text.Length);
        text.CopyTo(msg, 5);
        await pair.ServerStream.WriteAsync(msg);

        Assert.Equal("copié", await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
