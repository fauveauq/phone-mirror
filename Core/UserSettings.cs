using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhoneMirror.Core;

/// <summary>Couleur des indicateurs de clic.</summary>
public enum ClickColor { Turquoise, Rouge, Orange, Jaune, Vert, Violet, Blanc, Gris }

/// <summary>Forme des indicateurs de clic.</summary>
public enum ClickStyle { OndeEtPoint, OndeSeule, PointSeul }

/// <summary>Taille des indicateurs de clic.</summary>
public enum ClickSize { Petite, Moyenne, Grande }

/// <summary>
/// Réglages modifiables depuis l'interface, enregistrés dans %APPDATA%\PhoneMirror\settings.json.
/// </summary>
public sealed record UserSettings
{
    // Énumérations écrites en toutes lettres (« Rouge », « Grande ») : fichier lisible et modifiable à la main.
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string FilePath { get; } = Path.Combine(AppLog.DataDirectory, "settings.json");

    /// <summary>Numéro de série adb du téléphone ; null = le seul téléphone branché.</summary>
    public string? DeviceSerial { get; init; }

    /// <summary>Plus grande dimension de la vidéo en pixels ; 0 = résolution native.</summary>
    public int MaxSize { get; init; } = 1920;

    public int VideoBitRateMbps { get; init; } = 16;

    public int MaxFps { get; init; } = 60;

    /// <summary>Son du téléphone joué sur le PC.</summary>
    public bool EnableAudio { get; init; } = true;

    /// <summary>Éteint l'écran du téléphone pendant la recopie (batterie, discrétion).</summary>
    public bool TurnScreenOff { get; init; }

    /// <summary>Indicateur affiché à chaque clic sur l'image du téléphone (partage d'écran, démonstrations).</summary>
    public bool ShowClicks { get; init; } = true;

    public ClickStyle ClickStyle { get; init; } = ClickStyle.OndeEtPoint;

    public ClickColor ClickColor { get; init; } = ClickColor.Turquoise;

    public ClickSize ClickSize { get; init; } = ClickSize.Moyenne;

    /// <summary>Fenêtre toujours au premier plan.</summary>
    public bool AlwaysOnTop { get; init; }

    /// <summary>
    /// Source des mises à jour : null = releases GitHub du projet ; sinon un autre dépôt GitHub,
    /// une adresse web ou un dossier réseau (\\serveur\partage\PhoneMirror). Modifiable dans le fichier.
    /// </summary>
    public string? UpdateSource { get; init; }

    /// <summary>Paquet Android à afficher seul (démonstrations) ; null pour tout l'écran.</summary>
    public string? OnlyApp { get; init; }

    /// <summary>Position et taille de la fenêtre à la dernière fermeture.</summary>
    public WindowBounds? Window { get; init; }

    public static UserSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new UserSettings();
            UserSettings? loaded = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath), JsonOptions);
            return (loaded ?? new UserSettings()).Normalized();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            AppLog.Write($"Réglages illisibles ({FilePath}), valeurs par défaut utilisées : {ex.Message}");
            return new UserSettings();
        }
    }

    /// <summary>Enregistre les réglages ; lève une IOException si le fichier ne peut pas être écrit.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public MirrorOptions ApplyTo(MirrorOptions options) => options with
    {
        DeviceSerial = DeviceSerial,
        MaxSize = MaxSize,
        VideoBitRate = VideoBitRateMbps * 1_000_000,
        MaxFps = MaxFps,
        EnableAudio = EnableAudio,
        OnlyApp = OnlyApp,
    };

    /// <summary>Ramène dans des bornes raisonnables les valeurs d'un fichier modifié à la main.</summary>
    internal UserSettings Normalized() => this with
    {
        DeviceSerial = string.IsNullOrWhiteSpace(DeviceSerial) ? null : DeviceSerial.Trim(),
        UpdateSource = string.IsNullOrWhiteSpace(UpdateSource) ? null : UpdateSource.Trim(),
        OnlyApp = string.IsNullOrWhiteSpace(OnlyApp) ? null : OnlyApp.Trim(),
        MaxSize = MaxSize <= 0 ? 0 : Math.Clamp(MaxSize, 320, 4096),
        VideoBitRateMbps = Math.Clamp(VideoBitRateMbps, 1, 100),
        MaxFps = Math.Clamp(MaxFps, 1, 240),
        Window = Window is { Width: > 0, Height: > 0 } ? Window : null,
        ClickStyle = Enum.IsDefined(ClickStyle) ? ClickStyle : ClickStyle.OndeEtPoint,
        ClickColor = Enum.IsDefined(ClickColor) ? ClickColor : ClickColor.Turquoise,
        ClickSize = Enum.IsDefined(ClickSize) ? ClickSize : ClickSize.Moyenne,
    };
}

/// <summary>Position et taille d'une fenêtre, en unités WPF.</summary>
public sealed record WindowBounds(double Left, double Top, double Width, double Height);
