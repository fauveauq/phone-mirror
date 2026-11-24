using System.Windows;
using System.Windows.Controls;
using PhoneMirror.Core;

namespace PhoneMirror.UI;

/// <summary>
/// Écran d'attente du mode « une seule application » : couvre l'image tant que l'application choisie
/// n'est pas au premier plan (lancement en cours, application quittée ou fermée), pour qu'aucun autre
/// contenu du téléphone n'apparaisse pendant une démonstration.
/// </summary>
public sealed class AppPauseScreen
{
    private readonly Border _overlay;
    private readonly TextBlock _title;
    private readonly TextBlock _detail;
    private readonly Button _relaunch;
    private MirrorSession? _session;
    private string _package = "";
    private AppFocusWatcher? _watcher;
    private bool _seen;

    public AppPauseScreen(Border overlay, TextBlock title, TextBlock detail, Button relaunch)
    {
        _overlay = overlay;
        _title = title;
        _detail = detail;
        _relaunch = relaunch;
        _relaunch.Click += (_, _) => Relaunch();
    }

    /// <summary>Vrai quand l'image est masquée : le clavier n'est alors plus transmis au téléphone.</summary>
    public bool IsCovering => _overlay.Visibility == Visibility.Visible;

    /// <summary>Démarre la surveillance pour une session ; sans effet hors du mode « une seule application ».</summary>
    public void Start(MirrorSession session, MirrorOptions options)
    {
        if (options.OnlyApp is not { } package) return;

        _session = session;
        _package = package;
        _seen = false;
        Show();

        _watcher = new AppFocusWatcher(options, package, () => session.VirtualDisplayId);
        _watcher.VisibilityChanged += visible => _overlay.Dispatcher.InvokeAsync(() => OnVisibilityChanged(visible));
    }

    public async Task StopAsync()
    {
        _session = null;
        _overlay.Visibility = Visibility.Collapsed;
        AppFocusWatcher? watcher = _watcher;
        _watcher = null;
        if (watcher != null) await watcher.DisposeAsync();
    }

    private void OnVisibilityChanged(bool visible)
    {
        if (_session == null) return; // session arrêtée entre-temps
        if (visible)
        {
            _seen = true;
            _overlay.Visibility = Visibility.Collapsed;
        }
        else
        {
            Show();
        }
    }

    private void Show()
    {
        string name = _session?.AppLabel ?? _package;
        _title.Text = _seen ? "Démonstration en pause" : $"Lancement de {name}…";
        _detail.Text = _seen
            ? $"{name} n'est plus affichée. Le reste du téléphone reste masqué."
            : "Seule cette application sera affichée.";
        _relaunch.Content = $"Relancer {name}";
        _overlay.Visibility = Visibility.Visible;
    }

    private void Relaunch()
    {
        if (_session?.Control is { } control) control.StartApp(_package);
    }
}
