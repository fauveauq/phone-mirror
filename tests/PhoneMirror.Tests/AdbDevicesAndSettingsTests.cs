using PhoneMirror.Core;

namespace PhoneMirror.Tests;

public class AdbDevicesTests
{
    [Fact]
    public void Parse_reads_serial_state_and_model()
    {
        const string output = """
            * daemon not running; starting now at tcp:5037
            * daemon started successfully
            List of devices attached
            RF8N73G8YEX            device product:x1seea model:SM_G980F device:x1s transport_id:1
            emulator-5554          unauthorized transport_id:2

            """;

        List<AdbDevice> devices = AdbDevices.Parse(output.Replace("\n", "\r\n"));

        Assert.Equal(2, devices.Count);
        Assert.Equal(new AdbDevice("RF8N73G8YEX", "device", "SM G980F"), devices[0]);
        Assert.True(devices[0].IsReady);
        Assert.Equal(new AdbDevice("emulator-5554", "unauthorized", null), devices[1]);
        Assert.False(devices[1].IsReady);
    }

    [Fact]
    public void Parse_returns_empty_list_without_devices()
    {
        Assert.Empty(AdbDevices.Parse("List of devices attached\n\n"));
    }
}

public class UserSettingsTests
{
    [Fact]
    public void Normalized_clamps_values_edited_by_hand()
    {
        var settings = new UserSettings
        {
            DeviceSerial = "  ",
            MaxSize = 50,
            VideoBitRateMbps = 500,
            MaxFps = 0,
            Window = new WindowBounds(0, 0, -1, 100),
        }.Normalized();

        Assert.Null(settings.DeviceSerial);
        Assert.Equal(320, settings.MaxSize);
        Assert.Equal(100, settings.VideoBitRateMbps);
        Assert.Equal(1, settings.MaxFps);
        Assert.Null(settings.Window);
    }

    [Fact]
    public void Click_options_are_saved_in_words_and_read_back()
    {
        var settings = new UserSettings { ClickColor = ClickColor.Gris, ClickSize = ClickSize.Grande, ClickStyle = ClickStyle.OndeSeule };

        string json = System.Text.Json.JsonSerializer.Serialize(settings, UserSettings.JsonOptions);
        UserSettings? read = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(json, UserSettings.JsonOptions);

        Assert.Contains("\"Gris\"", json);
        Assert.Contains("\"Grande\"", json);
        Assert.Contains("\"OndeSeule\"", json);
        Assert.Equal(settings, read);
    }

    [Fact]
    public void Normalized_replaces_unknown_click_options()
    {
        UserSettings settings = new UserSettings { ClickStyle = (ClickStyle)7, ClickColor = (ClickColor)99, ClickSize = (ClickSize)(-1) }.Normalized();

        Assert.Equal(ClickStyle.OndeEtPoint, settings.ClickStyle);
        Assert.Equal(ClickColor.Turquoise, settings.ClickColor);
        Assert.Equal(ClickSize.Moyenne, settings.ClickSize);
    }

    [Fact]
    public void Normalized_keeps_native_resolution()
    {
        Assert.Equal(0, new UserSettings { MaxSize = 0 }.Normalized().MaxSize);
        Assert.Equal(0, new UserSettings { MaxSize = -5 }.Normalized().MaxSize);
    }

    [Fact]
    public void ApplyTo_converts_bit_rate_to_bits_per_second()
    {
        var defaults = new MirrorOptions { AdbPath = "adb", ServerJarPath = "s", FfmpegDirectory = "f" };
        var settings = new UserSettings { DeviceSerial = "ABC", MaxSize = 1280, VideoBitRateMbps = 24, MaxFps = 90 };

        MirrorOptions options = settings.ApplyTo(defaults);

        Assert.Equal("ABC", options.DeviceSerial);
        Assert.Equal(1280, options.MaxSize);
        Assert.Equal(24_000_000, options.VideoBitRate);
        Assert.Equal(90, options.MaxFps);
    }

    [Fact]
    public void Display_only_settings_do_not_change_session_options()
    {
        var defaults = new MirrorOptions { AdbPath = "adb", ServerJarPath = "s", FfmpegDirectory = "f" };
        var before = new UserSettings();
        UserSettings after = before with { AlwaysOnTop = true, TurnScreenOff = true };

        // MainWindow s'appuie sur cette égalité pour éviter de couper la recopie.
        Assert.Equal(before.ApplyTo(defaults), after.ApplyTo(defaults));
    }
}
