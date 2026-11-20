namespace PhoneMirror.Core;

/// <summary>
/// Envoi de fichiers vers le téléphone : un APK est installé, tout autre fichier est copié
/// dans le dossier Téléchargements.
/// </summary>
public sealed class FileTransfer(MirrorOptions options)
{
    private const string DownloadFolder = "/sdcard/Download/";
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    private readonly AdbRunner _adb = new(options.AdbPath, options.DeviceSerial);

    public static bool IsApk(string path) => Path.GetExtension(path).Equals(".apk", StringComparison.OrdinalIgnoreCase);

    /// <summary>Installe ou copie le fichier ; lève une AdbException avec un message clair en cas d'échec.</summary>
    public async Task SendAsync(string path, CancellationToken ct)
    {
        if (IsApk(path))
        {
            // -r : remplace l'application si elle est déjà installée.
            string output = await _adb.RunAsync(Timeout, ct, "install", "-r", path).ConfigureAwait(false);
            if (!output.Contains("Success", StringComparison.Ordinal))
                throw new AdbException($"Installation de {Path.GetFileName(path)} refusée : {output.Trim()}");
        }
        else
        {
            await _adb.RunAsync(Timeout, ct, "push", path, DownloadFolder).ConfigureAwait(false);
        }
    }
}
