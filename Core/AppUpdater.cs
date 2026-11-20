using Velopack;
using Velopack.Sources;

namespace PhoneMirror.Core;

/// <summary>
/// Mises à jour automatiques depuis les releases GitHub du projet : la nouvelle version est
/// téléchargée en arrière-plan puis installée à la fermeture de l'application.
/// </summary>
public static class AppUpdater
{
    private const string RepositoryUrl = "https://github.com/fauveauq/phone-mirror";

    /// <summary>
    /// Renvoie le numéro de la version téléchargée (installée à la fermeture), ou null s'il n'y en a pas.
    /// Ne lève jamais d'exception : une mise à jour impossible ne doit pas gêner l'utilisation.
    /// </summary>
    public static async Task<string?> DownloadPendingUpdateAsync()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepositoryUrl, null, false));
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
            AppLog.Write("Vérification des mises à jour impossible : " + ex.Message);
            return null;
        }
    }
}
