using Velopack;
using Velopack.Sources;

namespace PhoneMirror.Core;

/// <summary>
/// Mises à jour automatiques : la nouvelle version est téléchargée en arrière-plan puis installée
/// à la fermeture de l'application. La source se choisit dans les réglages (UpdateSource) :
/// releases GitHub par défaut, ou dossier réseau, ou adresse web.
/// </summary>
public static class AppUpdater
{
    public const string DefaultSource = "https://github.com/fauveauq/phone-mirror";

    /// <summary>
    /// Renvoie le numéro de la version téléchargée (installée à la fermeture), ou null s'il n'y en a pas.
    /// Ne lève jamais d'exception : une mise à jour impossible ne doit pas gêner l'utilisation.
    /// </summary>
    public static async Task<string?> DownloadPendingUpdateAsync(string? source)
    {
        try
        {
            var manager = new UpdateManager(CreateSource(source ?? DefaultSource));
            if (!manager.IsInstalled) return null; // lancé depuis Visual Studio ou dotnet run

            UpdateInfo? update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update == null) return null;

            await manager.DownloadUpdatesAsync(update).ConfigureAwait(false);
            manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);

            string version = update.TargetFullRelease.Version.ToString();
            AppLog.Write($"Mise à jour {version} téléchargée, installée à la fermeture.");
            return version;
        }
        catch (Exception ex)
        {
            AppLog.Write($"Vérification des mises à jour impossible ({source ?? DefaultSource}) : {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Dépôt GitHub (releases), adresse web contenant les fichiers de Velopack,
    /// ou dossier local / réseau (\\serveur\partage\PhoneMirror).
    /// </summary>
    internal static IUpdateSource CreateSource(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https")
        {
            return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                ? new GithubSource(source, null, false)
                : new SimpleWebSource(uri);
        }
        return new SimpleFileSource(new DirectoryInfo(source));
    }
}
