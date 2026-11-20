using System.Collections.Concurrent;
using System.Diagnostics;

namespace PhoneMirror.Core;

/// <summary>
/// Installe le serveur scrcpy sur le téléphone, ouvre le tunnel adb et lance le serveur.
/// Le serveur capture l'écran et l'encode en H.264 avec l'encodeur matériel du téléphone.
/// </summary>
public sealed class ScrcpyServer(MirrorOptions options, Action<string> log) : IAsyncDisposable
{
    private const string RemoteJarPath = "/data/local/tmp/phone-mirror-server.jar";
    private const string SocketName = "scrcpy";
    private const int RecentOutputLines = 20;
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);

    private readonly AdbRunner _adb = new(options.AdbPath, options.DeviceSerial);
    private readonly ConcurrentQueue<string> _recentOutput = new();
    private Process? _process;
    private bool _forwardActive;

    public bool HasExited => _process is { HasExited: true };

    /// <summary>Dernières lignes écrites par le serveur, utiles pour expliquer un échec.</summary>
    public string RecentOutput => string.Join(Environment.NewLine, _recentOutput);

    public async Task StartAsync(CancellationToken ct)
    {
        if (!File.Exists(options.ServerJarPath))
            throw new FileNotFoundException(
                $"Serveur scrcpy introuvable : {options.ServerJarPath}. Voir le README pour l'installer.");

        await EnsureDeviceReadyAsync(ct).ConfigureAwait(false);

        log("Copie du serveur sur le téléphone…");
        await _adb.RunAsync(CommandTimeout, ct, "push", options.ServerJarPath, RemoteJarPath).ConfigureAwait(false);

        await _adb.RunAsync(CommandTimeout, ct, "forward", $"tcp:{options.LocalPort}", $"localabstract:{SocketName}")
            .ConfigureAwait(false);
        _forwardActive = true;

        log("Démarrage du serveur…");
        _process = _adb.StartLongRunning(OnServerOutput,
            "shell",
            $"CLASSPATH={RemoteJarPath}",
            "app_process", "/", "com.genymobile.scrcpy.Server", options.ServerVersion,
            "tunnel_forward=true",
            "audio=false",
            options.EnableControl ? "control=true" : "control=false",
            "cleanup=true",
            "video_codec=h264",
            $"max_size={options.MaxSize}",
            $"video_bit_rate={options.VideoBitRate}",
            $"max_fps={options.MaxFps}",
            "log_level=info");
    }

    private async Task EnsureDeviceReadyAsync(CancellationToken ct)
    {
        string state;
        try
        {
            state = (await _adb.RunAsync(CommandTimeout, ct, "get-state").ConfigureAwait(false)).Trim();
        }
        catch (AdbException ex)
        {
            throw new AdbException(
                "Aucun téléphone prêt. Branche-le en USB, active le débogage USB et accepte l'autorisation sur l'écran du téléphone.\n\n"
                + ex.Message);
        }

        if (state != "device")
            throw new AdbException($"Le téléphone est dans l'état « {state} ». Déverrouille-le et accepte le débogage USB.");
    }

    private void OnServerOutput(string line)
    {
        log("[serveur] " + line);
        _recentOutput.Enqueue(line);
        while (_recentOutput.Count > RecentOutputLines) _recentOutput.TryDequeue(out _);
    }

    public async ValueTask DisposeAsync()
    {
        if (_process != null)
        {
            // Fermer le shell adb arrête aussi le serveur côté téléphone (cleanup=true).
            AdbRunner.TryKill(_process);
            _process.Dispose();
            _process = null;
        }

        if (_forwardActive)
        {
            _forwardActive = false;
            try
            {
                await _adb.RunAsync(TimeSpan.FromSeconds(3), CancellationToken.None,
                    "forward", "--remove", $"tcp:{options.LocalPort}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log("Suppression du tunnel adb impossible : " + ex.Message);
            }
        }
    }
}
