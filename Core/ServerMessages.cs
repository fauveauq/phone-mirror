using System.Globalization;
using System.Text.RegularExpressions;

namespace PhoneMirror.Core;

/// <summary>Informations utiles tirées du journal du serveur scrcpy.</summary>
public static partial class ServerMessages
{
    /// <summary>« [server] INFO: New display: 1080x2400/360 (id=58) »</summary>
    internal static int? VirtualDisplayId(string line)
    {
        Match m = NewDisplayRegex().Match(line);
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>« [server] INFO: Starting app "Field Service" [com.salesforce.fieldservice.app] on display 58... »</summary>
    internal static string? StartedAppLabel(string line)
    {
        Match m = StartingAppRegex().Match(line);
        return m.Success ? m.Groups[1].Value : null;
    }

    [GeneratedRegex(@"New display: .*\(id=(\d+)\)")]
    private static partial Regex NewDisplayRegex();

    [GeneratedRegex(@"Starting app ""(.+)"" \[")]
    private static partial Regex StartingAppRegex();
}
