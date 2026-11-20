namespace PhoneMirror.Core;

/// <summary>Appareil vu par adb (« device » = prêt, « unauthorized » = autorisation à accepter…).</summary>
public sealed record AdbDevice(string Serial, string State, string? Model)
{
    public bool IsReady => State == "device";
}

/// <summary>Liste des appareils branchés (adb devices -l).</summary>
public static class AdbDevices
{
    public static async Task<IReadOnlyList<AdbDevice>> ListAsync(string adbPath, CancellationToken ct)
    {
        var adb = new AdbRunner(adbPath, serial: null);
        string output = await adb.RunAsync(TimeSpan.FromSeconds(10), ct, "devices", "-l").ConfigureAwait(false);
        return Parse(output);
    }

    // Exemple de ligne : « RF8N73G8YEX  device product:x1seea model:SM_G980F device:x1s transport_id:1 »
    internal static List<AdbDevice> Parse(string output)
    {
        var devices = new List<AdbDevice>();
        foreach (string line in output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("List of devices", StringComparison.Ordinal) || line.StartsWith('*')) continue;

            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            string? model = parts.FirstOrDefault(p => p.StartsWith("model:", StringComparison.Ordinal))?[6..].Replace('_', ' ');
            devices.Add(new AdbDevice(parts[0], parts[1], model));
        }
        return devices;
    }
}
