using System.Globalization;
using System.Text.RegularExpressions;

namespace PhoneMirror.Core;

/// <summary>Application du téléphone : nom affiché et nom de paquet.</summary>
public sealed record PhoneApp(string Label, string Package);

/// <summary>Applications du téléphone : celles qu'on peut afficher seules, et celle au premier plan d'un écran donné.</summary>
public static partial class PhoneApps
{
    private const string ListJarPath = "/data/local/tmp/phone-mirror-apps.jar";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Applications installées par l'utilisateur, avec leur nom affiché, triées par nom.
    /// Utilise l'option list_apps du serveur scrcpy, qui efface lui-même son fichier en sortant.
    /// </summary>
    public static async Task<IReadOnlyList<PhoneApp>> ListAsync(MirrorOptions options, CancellationToken ct)
    {
        var adb = new AdbRunner(options.AdbPath, options.DeviceSerial);
        await adb.RunAsync(Timeout, ct, "push", options.ServerJarPath, ListJarPath).ConfigureAwait(false);
        string output = await adb.RunAsync(Timeout, ct, "shell", $"CLASSPATH={ListJarPath}",
            "app_process", "/", "com.genymobile.scrcpy.Server", options.ServerVersion, "list_apps=true").ConfigureAwait(false);
        return ParseAppList(output);
    }

    /// <summary>
    /// Lit la liste du serveur : « - Field Service      com.salesforce.fieldservice.app ».
    /// « - » = installée par l'utilisateur, « * » = application système (écartée).
    /// </summary>
    internal static List<PhoneApp> ParseAppList(string output) =>
        AppLineRegex().Matches(output)
            .Where(m => m.Groups[1].Value == "-")
            .Select(m => new PhoneApp(m.Groups[2].Value.Trim(), m.Groups[3].Value))
            .DistinctBy(a => a.Package)
            .OrderBy(a => a.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>
    /// Paquet de l'activité au premier plan sur l'écran <paramref name="displayId"/>, d'après
    /// « dumpsys activity activities » ; null si aucune activité n'y est affichée.
    /// </summary>
    internal static string? TopPackage(string dumpsysOutput, int displayId)
    {
        string wanted = displayId.ToString(CultureInfo.InvariantCulture);
        bool inDisplay = false;
        foreach (string line in dumpsysOutput.Split('\n'))
        {
            Match display = DisplayRegex().Match(line);
            if (display.Success)
            {
                inDisplay = display.Groups[1].Value == wanted;
                continue;
            }
            if (!inDisplay) continue;

            Match top = TopResumedRegex().Match(line);
            if (top.Success) return top.Groups[1].Value;
        }
        return null;
    }

    [GeneratedRegex(@"^\s*([*-])\s+(.+?)\s+([A-Za-z0-9_.]+)\s*$", RegexOptions.Multiline)]
    private static partial Regex AppLineRegex();

    [GeneratedRegex(@"^\s*Display #(\d+)")]
    private static partial Regex DisplayRegex();

    // « topResumedActivity=ActivityRecord{be2df25 u0 com.salesforce.fieldservice.app/.Activity t4852} »
    [GeneratedRegex(@"topResumedActivity=ActivityRecord\{\S+ u\d+ ([A-Za-z0-9_.]+)/")]
    private static partial Regex TopResumedRegex();
}
