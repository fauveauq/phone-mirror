using System.Globalization;
using System.Text.RegularExpressions;

namespace PhoneMirror.Core;

/// <summary>
/// Applications du téléphone : celles qu'on peut afficher seules, et celle au premier plan d'un écran donné.
/// Toujours pour l'utilisateur principal (0) : un Dossier sécurisé Samsung fait échouer
/// les commandes qui portent sur tous les utilisateurs.
/// </summary>
public static partial class PhoneApps
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>Applications installées par l'utilisateur et présentes dans le lanceur, triées.</summary>
    public static async Task<IReadOnlyList<string>> ListLaunchableAsync(string adbPath, string? serial, CancellationToken ct)
    {
        var adb = new AdbRunner(adbPath, serial);
        string launchable = await adb.RunAsync(Timeout, ct, "shell", "cmd", "package", "query-activities", "--brief",
            "--user", "0", "-a", "android.intent.action.MAIN", "-c", "android.intent.category.LAUNCHER").ConfigureAwait(false);
        string installed = await adb.RunAsync(Timeout, ct, "shell", "pm", "list", "packages", "-3", "--user", "0")
            .ConfigureAwait(false);
        return Launchable(launchable, installed);
    }

    /// <summary>Croise « query-activities » (paquet/activité) et « pm list packages -3 » (package:nom).</summary>
    internal static List<string> Launchable(string queryActivitiesOutput, string thirdPartyOutput)
    {
        HashSet<string> thirdParty = thirdPartyOutput.Split('\n', StringSplitOptions.TrimEntries)
            .Where(l => l.StartsWith("package:", StringComparison.Ordinal))
            .Select(l => l["package:".Length..])
            .ToHashSet(StringComparer.Ordinal);

        return ComponentRegex().Matches(queryActivitiesOutput)
            .Select(m => m.Groups[1].Value)
            .Where(thirdParty.Contains)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

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

    // « com.salesforce.chatter/.Chatter » dans la sortie de query-activities.
    [GeneratedRegex(@"^\s*([A-Za-z0-9_.]+)/\S+\s*$", RegexOptions.Multiline)]
    private static partial Regex ComponentRegex();

    [GeneratedRegex(@"^\s*Display #(\d+)")]
    private static partial Regex DisplayRegex();

    // « topResumedActivity=ActivityRecord{be2df25 u0 com.salesforce.fieldservice.app/.Activity t4852} »
    [GeneratedRegex(@"topResumedActivity=ActivityRecord\{\S+ u\d+ ([A-Za-z0-9_.]+)/")]
    private static partial Regex TopResumedRegex();
}
