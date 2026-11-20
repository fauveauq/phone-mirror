namespace PhoneMirror.Core;

/// <summary>Réglages d'une session de recopie d'écran.</summary>
public sealed record MirrorOptions
{
    /// <summary>Chemin de adb.exe (ou simplement "adb" s'il est dans le PATH).</summary>
    public required string AdbPath { get; init; }

    /// <summary>Fichier scrcpy-server téléchargé depuis les releases de scrcpy.</summary>
    public required string ServerJarPath { get; init; }

    /// <summary>Doit correspondre exactement à la version du fichier scrcpy-server.</summary>
    public string ServerVersion { get; init; } = "5.0.1";

    /// <summary>Dossier contenant les DLL FFmpeg « shared » (avcodec-*.dll, etc.).</summary>
    public required string FfmpegDirectory { get; init; }

    /// <summary>Numéro de série adb du téléphone ; null pour le seul appareil branché.</summary>
    public string? DeviceSerial { get; init; }

    public int LocalPort { get; init; } = 27183;

    /// <summary>Plus grande dimension de la vidéo, en pixels (0 = résolution native).</summary>
    public int MaxSize { get; init; } = 1920;

    /// <summary>Débit vidéo en bit/s. L'USB supporte largement 16 Mbit/s, utile pour les vidéos plein écran.</summary>
    public int VideoBitRate { get; init; } = 16_000_000;

    public int MaxFps { get; init; } = 60;

    /// <summary>Son du téléphone joué sur le PC (Android 11 ou plus récent).</summary>
    public bool EnableAudio { get; init; } = true;

    /// <summary>Contrôle du téléphone depuis le PC (souris, clavier, presse-papiers).</summary>
    public bool EnableControl { get; init; } = true;

    public static MirrorOptions CreateDefault()
    {
        string tools = Path.Combine(AppContext.BaseDirectory, "third_party");
        string bundledAdb = Path.Combine(tools, "adb", "adb.exe");

        return new MirrorOptions
        {
            AdbPath = File.Exists(bundledAdb) ? bundledAdb : "adb",
            ServerJarPath = Path.Combine(tools, "scrcpy-server"),
            FfmpegDirectory = Path.Combine(tools, "ffmpeg"),
        };
    }
}
