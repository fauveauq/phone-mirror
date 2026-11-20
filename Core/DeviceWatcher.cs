namespace PhoneMirror.Core;

/// <summary>Attend qu'un téléphone soit branché et autorisé (état adb « device »).</summary>
public sealed class DeviceWatcher(MirrorOptions options)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    // Large : le premier appel peut devoir démarrer le démon adb (plusieurs secondes).
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);

    private readonly AdbRunner _adb = new(options.AdbPath, options.DeviceSerial);

    /// <summary>Se termine dès que le téléphone est prêt ; interrompu par <paramref name="ct"/>.</summary>
    public async Task WaitForDeviceAsync(CancellationToken ct)
    {
        while (true)
        {
            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
            try
            {
                string state = await _adb.RunAsync(CommandTimeout, ct, "get-state").ConfigureAwait(false);
                if (state.Trim() == "device") return;
            }
            catch (AdbException)
            {
                // Aucun téléphone, ou pas encore autorisé : on continue d'attendre.
            }
        }
    }
}
