using System.Text;
using PhoneMirror.Core;

namespace PhoneMirror.Tests;

/// <summary>Mode « une seule application » : écran virtuel, lancement, surveillance et liste des applications.</summary>
public class OnlyAppTests
{
    [Fact]
    public async Task StartApp_writes_type_16_then_one_byte_length_and_name()
    {
        using var pair = await LoopbackPair.CreateAsync();
        using var channel = new ScrcpyControlChannel(pair.Client, _ => { });

        channel.StartApp("com.salesforce.fieldservice.app");
        byte[] msg = await pair.ReadFromServerAsync(2 + 31);

        Assert.Equal(16, msg[0]); // TYPE_START_APP
        Assert.Equal(31, msg[1]);
        Assert.Equal("com.salesforce.fieldservice.app", Encoding.UTF8.GetString(msg, 2, 31));
    }

    [Fact]
    public void DisplayGeometry_scales_density_when_only_the_size_is_reduced()
    {
        // Galaxy S20 en mode FHD+ : taille réduite, densité physique seule.
        DisplayGeometry? g = DisplayGeometry.Parse(
            "Physical size: 1440x3200\r\nOverride size: 1080x2400\r\n", "Physical density: 480\r\n");

        Assert.Equal(new DisplayGeometry(1080, 2400, 360), g);
        Assert.Equal("1080x2400/360", g.ToString());
    }

    [Fact]
    public void DisplayGeometry_prefers_override_density_and_handles_plain_values()
    {
        Assert.Equal(new DisplayGeometry(1080, 2340, 440),
            DisplayGeometry.Parse("Physical size: 1080x2340\n", "Physical density: 400\nOverride density: 440\n"));
        Assert.Null(DisplayGeometry.Parse("", "Physical density: 400"));
    }

    [Fact]
    public void ServerMessages_read_virtual_display_and_app_label()
    {
        Assert.Equal(141, ServerMessages.VirtualDisplayId("[server] INFO: New display: 1080x2400/360 (id=141)"));
        Assert.Null(ServerMessages.VirtualDisplayId("[server] INFO: Device: [samsung] samsung SM-G980F (Android 13)"));
        Assert.Equal("Field Service", ServerMessages.StartedAppLabel(
            "[server] INFO: Starting app \"Field Service\" [com.salesforce.fieldservice.app] on display 141..."));
    }

    private const string Dumpsys = """
        Display #141 (activities from top to bottom):
            topResumedActivity=ActivityRecord{be2df25 u0 com.salesforce.fieldservice.app/com.salesforce.fieldservice.ui.launcher.FSLoginActivity} t4852}
        Display #2 (activities from top to bottom):
        Display #0 (activities from top to bottom):
              topResumedActivity=ActivityRecord{9860a85 u0 com.sec.android.app.launcher/.activities.LauncherActivity} t7}
        """;

    [Fact]
    public void TopPackage_reads_the_activity_of_the_requested_display_only()
    {
        Assert.Equal("com.salesforce.fieldservice.app", PhoneApps.TopPackage(Dumpsys, 141));
        Assert.Equal("com.sec.android.app.launcher", PhoneApps.TopPackage(Dumpsys, 0));
        Assert.Null(PhoneApps.TopPackage(Dumpsys, 2));  // écran sans activité
        Assert.Null(PhoneApps.TopPackage(Dumpsys, 14)); // ne pas confondre avec 141
    }

    [Fact]
    public void App_is_visible_only_when_it_or_a_permission_dialog_is_on_top()
    {
        const string app = "com.salesforce.fieldservice.app";
        Assert.True(AppFocusWatcher.IsAppVisible(app, app));
        Assert.True(AppFocusWatcher.IsAppVisible("com.google.android.permissioncontroller", app));
        Assert.False(AppFocusWatcher.IsAppVisible("com.sec.android.app.launcher", app));
        Assert.False(AppFocusWatcher.IsAppVisible(null, app));
    }

    [Fact]
    public void Launchable_keeps_user_apps_present_in_the_launcher()
    {
        const string activities = """
            181 activities found:
              Activity #0:
                priority=500 preferredOrder=0 match=0x108000 specificIndex=-1 isDefault=true
                com.sec.android.gallery3d/com.samsung.android.gallery.app.activity.GalleryActivity
              Activity #1:
                com.salesforce.fieldservice.app/com.salesforce.fieldservice.ui.launcher.AppLaunchActivity
              Activity #2:
                com.salesforce.chatter/.Chatter
              Activity #3:
                com.salesforce.chatter/.Other
            """;
        const string thirdParty = "package:com.salesforce.chatter\r\npackage:com.salesforce.fieldservice.app\r\npackage:com.example.noicon\r\n";

        Assert.Equal(["com.salesforce.chatter", "com.salesforce.fieldservice.app"], PhoneApps.Launchable(activities, thirdParty));
    }

    [Fact]
    public void OnlyApp_setting_is_trimmed_and_passed_to_the_session()
    {
        var defaults = new MirrorOptions { AdbPath = "adb", ServerJarPath = "s", FfmpegDirectory = "f" };

        Assert.Null(new UserSettings { OnlyApp = "  " }.Normalized().OnlyApp);
        Assert.Equal("com.a.b", new UserSettings { OnlyApp = " com.a.b " }.Normalized().ApplyTo(defaults).OnlyApp);
    }
}
