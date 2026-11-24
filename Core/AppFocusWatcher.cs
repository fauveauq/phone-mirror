using System.Text.RegularExpressions;

namespace PhoneMirror.Core;

/// <summary>État de l'écran virtuel vu par l'utilisateur.</summary>
public enum AppScreenState
{
    /// <summary>L'application choisie est affichée et utilisable.</summary>
    Visible,
    /// <summary>Une autre application, ou aucune, est au premier plan.</summary>
    OtherApp,
    /// <summary>Téléphone entièrement en veille : les clics sont ignorés.</summary>
    Asleep,
}

/// <summary>
/// Surveille l'application affichée sur l'écran virtuel (mode « une seule application ») et signale
/// quand l'application choisie n'est plus au premier plan, pour masquer l'image.
/// </summary>
public sealed partial class AppFocusWatcher : IAsyncDisposable
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
    private AppScreenState? _state;

    /// <summary>Déclenché à chaque changement d'état, hors du thread de l'interface.</summary>
    public event Action<AppScreenState>? StateChanged;

    public AppFocusWatcher(MirrorOptions options, string package, Func<int?> displayId)
    {
        _adb = new AdbRunner(options.AdbPath, options.DeviceSerial);
        _package = package;
        _displayId = displayId;
        _loop = Task.Run(LoopAsync);
    }

    internal static bool IsAppVisible(string? topPackage, string package) =>
        topPackage != null && (topPackage == package || AllowedOverlays.Contains(topPackage));

    /// <summary>
    /// Déduit l'état de la sortie combinée de dumpsys power et activity. Si tout le téléphone est en veille,
    /// Android ignore les clics même si l'application paraît encore au premier plan.
    /// Le verrouillage seul ne gêne pas : l'écran virtuel est « toujours déverrouillé » (FLAG_ALWAYS_UNLOCKED)
    /// et forme son propre groupe d'écrans, il continue de fonctionner téléphone verrouillé.
    /// </summary>
    internal static AppScreenState Evaluate(string output, int displayId, string package)
    {
        Match wakefulness = WakefulnessRegex().Match(output);
        if (wakefulness.Success && wakefulness.Groups[1].Value != "Awake")
            return AppScreenState.Asleep;

        return IsAppVisible(PhoneApps.TopPackage(output, displayId), package) ? AppScreenState.Visible : AppScreenState.OtherApp;
    }

    [GeneratedRegex(@"mWakefulness=(\w+)")]
    private static partial Regex WakefulnessRegex();

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
                        "dumpsys power | grep mWakefulness=; " +
                        "dumpsys activity activities | grep -E 'Display #|topResumedActivity'").ConfigureAwait(false);
                    AppScreenState state = Evaluate(output, id, _package);
                    if (state != _state)
                    {
                        _state = state;
                        StateChanged?.Invoke(state);
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
