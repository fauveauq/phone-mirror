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

        DisplayGeometry? display = null;
        if (options.OnlyApp != null)
        {
            display = await ReadDisplayGeometryAsync(ct).ConfigureAwait(false);
            log($"Écran virtuel {display} pour {options.OnlyApp}");
        }

        log("Démarrage du serveur…");
        _process = _adb.StartLongRunning(OnServerOutput, [.. BuildArguments(display)]);
    }

    private async Task<DisplayGeometry> ReadDisplayGeometryAsync(CancellationToken ct)
    {
        string size = await _adb.RunAsync(CommandTimeout, ct, "shell", "wm", "size").ConfigureAwait(false);
        string density = await _adb.RunAsync(CommandTimeout, ct, "shell", "wm", "density").ConfigureAwait(false);
        return DisplayGeometry.Parse(size, density)
            ?? throw new AdbException("Taille de l'écran du téléphone illisible :" + Environment.NewLine + size + density);
    }

    private IEnumerable<string> BuildArguments(DisplayGeometry? display)
    {
        string[] common =
        [
            "shell",
            $"CLASSPATH={RemoteJarPath}",
            "app_process", "/", "com.genymobile.scrcpy.Server", options.ServerVersion,
            "tunnel_forward=true",
            options.EnableAudio ? "audio=true" : "audio=false",
            "audio_codec=opus",
            options.EnableControl ? "control=true" : "control=false",
            "cleanup=true",
            "video_codec=h264",
            $"max_size={options.MaxSize}",
            $"video_bit_rate={options.VideoBitRate}",
            $"max_fps={options.MaxFps}",
            "log_level=info",
        ];
        foreach (string arg in common)
            yield return arg;

        if (options.OnlyApp != null && display is { } geometry)
        {
            // Écran virtuel à la taille de l'écran du téléphone, sans accueil ni barre système :
            // si l'application se ferme, il reste vide au lieu de montrer autre chose.
            yield return $"new_display={geometry}";
            // Les clics sur l'écran virtuel ne comptent pas comme une activité pour Android :
            // sans cela, le téléphone se met en veille puis se verrouille en pleine démonstration.
            // Le réglage d'origine est rétabli par le serveur en fin de session (cleanup=true).
            yield return "stay_awake=true";
        }
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

    /// <summary>Écran virtuel créé par le serveur (mode « une seule application »), une fois annoncé.</summary>
    public int? VirtualDisplayId { get; private set; }

    /// <summary>Nom affiché de l'application lancée, une fois annoncé par le serveur.</summary>
    public string? StartedAppLabel { get; private set; }

    private void OnServerOutput(string line)
    {
        log("[serveur] " + line);
        VirtualDisplayId = ServerMessages.VirtualDisplayId(line) ?? VirtualDisplayId;
        StartedAppLabel = ServerMessages.StartedAppLabel(line) ?? StartedAppLabel;
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
