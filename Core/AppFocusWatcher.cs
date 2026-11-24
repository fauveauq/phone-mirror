namespace PhoneMirror.Core;

/// <summary>
/// Surveille l'application affichée sur l'écran virtuel (mode « une seule application ») et signale
/// quand l'application choisie n'est plus au premier plan, pour masquer l'image.
/// </summary>
public sealed class AppFocusWatcher : IAsyncDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);

    // Fenêtres système qu'une application ouvre par-dessus elle-même (demandes d'autorisation).
    private static readonly HashSet<string> AllowedOverlays = new(StringComparer.Ordinal)
    {
        "com.google.android.permissioncontroller",
        "com.android.permissioncontroller",
    };

    private readonly AdbRunner _adb;
    private readonly string _package;
    private readonly Func<int?> _displayId;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private bool? _visible;

    /// <summary>true quand l'application choisie est affichée. Déclenché hors du thread de l'interface.</summary>
    public event Action<bool>? VisibilityChanged;

    public AppFocusWatcher(MirrorOptions options, string package, Func<int?> displayId)
    {
        _adb = new AdbRunner(options.AdbPath, options.DeviceSerial);
        _package = package;
        _displayId = displayId;
        _loop = Task.Run(LoopAsync);
    }

    internal static bool IsAppVisible(string? topPackage, string package) =>
        topPackage != null && (topPackage == package || AllowedOverlays.Contains(topPackage));

    private async Task LoopAsync()
    {
        CancellationToken ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_displayId() is { } id)
                {
                    // Filtré sur le téléphone : la sortie complète de dumpsys est volumineuse.
                    string output = await _adb.RunAsync(CommandTimeout, ct, "shell",
                        "dumpsys activity activities | grep -E 'Display #|topResumedActivity'").ConfigureAwait(false);
                    bool visible = IsAppVisible(PhoneApps.TopPackage(output, id), _package);
                    if (visible != _visible)
                    {
                        _visible = visible;
                        VisibilityChanged?.Invoke(visible);
                    }
                }
                await Task.Delay(Interval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (AdbException)
            {
                // Téléphone débranché : la session s'arrête d'elle-même ; on garde le dernier état.
                try { await Task.Delay(Interval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        await _loop.ConfigureAwait(false);
        _cts.Dispose();
    }
}
