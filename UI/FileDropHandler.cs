using System.Windows;
using PhoneMirror.Core;

namespace PhoneMirror.UI;

/// <summary>
/// Glisser-déposer de fichiers sur la fenêtre : les APK sont installés, les autres fichiers
/// copiés dans Téléchargements. Les envois se font l'un après l'autre.
/// </summary>
public sealed class FileDropHandler
{
    private readonly Func<MirrorOptions?> _activeOptions;
    private readonly Action<string> _notify;
    private Task _pending = Task.CompletedTask;

    /// <param name="activeOptions">Réglages de la session en cours, ou null si aucun téléphone n'est connecté.</param>
    /// <param name="notify">Affiche un message bref à l'utilisateur.</param>
    public FileDropHandler(Window window, Func<MirrorOptions?> activeOptions, Action<string> notify)
    {
        _activeOptions = activeOptions;
        _notify = notify;
        window.DragOver += OnDragOver;
        window.Drop += OnDrop;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        bool accepted = e.Data.GetDataPresent(DataFormats.FileDrop) && _activeOptions() != null;
        e.Effects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        MirrorOptions? options = _activeOptions();
        if (options == null || e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

        string[] files = paths.Where(File.Exists).ToArray();
        if (files.Length < paths.Length)
            _notify("Les dossiers ne peuvent pas être envoyés : seuls les fichiers le sont.");

        // À la suite des envois précédents, pour ne pas lancer plusieurs installations en parallèle.
        _pending = _pending.ContinueWith(_ => SendAllAsync(new FileTransfer(options), files),
            TaskScheduler.FromCurrentSynchronizationContext()).Unwrap();
    }

    private async Task SendAllAsync(FileTransfer transfer, string[] files)
    {
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            bool apk = FileTransfer.IsApk(file);
            _notify(apk ? $"Installation de {name}…" : $"Envoi de {name}…");
            try
            {
                await transfer.SendAsync(file, CancellationToken.None);
                _notify(apk ? $"{name} installé." : $"{name} copié dans Téléchargements.");
                AppLog.Write($"Fichier envoyé : {file}");
            }
            catch (AdbException ex)
            {
                _notify(ex.Message);
                AppLog.Write($"Envoi impossible ({file}) : {ex.Message}");
            }
        }
    }
}
