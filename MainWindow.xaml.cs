using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PhoneMirror.Core;
using PhoneMirror.Input;
using PhoneMirror.UI;

namespace PhoneMirror;

public partial class MainWindow : Window
{
    private UserSettings _settings = UserSettings.Load();
    private readonly AppPauseScreen _appPause;
    private MirrorOptions _options;
    private readonly DispatcherTimer _statsTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    // Au-delà de ce nombre d'échecs rapprochés, on arrête de se reconnecter tout seul
    // (panne réelle plutôt que câble débranché) : l'utilisateur garde le bouton « Réessayer ».
    private const int MaxQuickFailures = 3;
    private static readonly TimeSpan StableSessionDuration = TimeSpan.FromSeconds(10);

    private DeviceWatcher _deviceWatcher;
    private MirrorSession? _session;
    private CancellationTokenSource? _connectCts;
    private CancellationTokenSource? _waitCts;
    private DateTime _sessionStartedAt;
    private int _quickFailures;
    private WriteableBitmap? _bitmap;

    private long _displayedFrames;
    private long _lastDecodedFrames;
    private long _lastDisplayedFrames;
    private long _statsTicks;

    private readonly FullScreenToggle _fullScreen;
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(4) };

    public MainWindow()
    {
        InitializeComponent();
        _options = _settings.ApplyTo(MirrorOptions.CreateDefault());
        _deviceWatcher = new DeviceWatcher(_options);
        _appPause = new AppPauseScreen(AppPauseOverlay, AppPauseTitle, AppPauseDetail, AppRelaunchButton);

        // Souris et clavier transmis au téléphone tant qu'une session est active.
        _ = new MouseForwarder(VideoImage, () => _session?.Control);
        // L'écran d'attente couvre déjà la souris ; le clavier est coupé pendant qu'il est affiché.
        _ = new KeyboardForwarder(this, () => _appPause.IsCovering ? null : _session?.Control);
        _ = new FileDropHandler(this, () => _session?.Control != null ? _options : null, ShowToast);

        // Ondes de clic : vers l'extérieur pour le toucher (gauche), vers l'intérieur pour le retour (droit).
        // PreviewMouseDown passe avant MouseForwarder, qui marque l'événement comme traité.
        var ripples = new ClickRippleLayer(ClickLayer);
        VideoImage.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (_settings.ShowClicks && _session?.Control != null)
                ripples.Show(e.GetPosition(ClickLayer), ClickAppearance.From(_settings));
        };
        VideoImage.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (_settings.ShowClicks && _session?.Control != null)
                ripples.ShowInward(e.GetPosition(ClickLayer), ClickAppearance.From(_settings));
        };

        _fullScreen = new FullScreenToggle(this);
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); Toast.Visibility = Visibility.Collapsed; };
        Topmost = _settings.AlwaysOnTop;
        RestoreWindowBounds();

        Loaded += async (_, _) =>
        {
            _ = CheckForUpdatesAsync();
            await ConnectAsync();
        };
        Closing += OnClosing;
        KeyDown += OnKeyDown;
        _statsTimer.Tick += (_, _) => UpdateStats();

        // Appelé à chaque rafraîchissement de l'écran : on y affiche l'image la plus récente.
        CompositionTarget.Rendering += OnRendering;
    }

    private async Task ConnectAsync()
    {
        if (_session != null) return;
        CancelWaitForDevice();

        ShowStatus("Connexion au téléphone…",
            "Branche ton téléphone en USB, avec le débogage USB activé.", canRetry: false);

        var session = new MirrorSession(_options);
        session.Log += AppLog.Write;
        session.Faulted += error => Dispatcher.InvokeAsync(() => OnSessionFaulted(session, error));

        _session = session;
        _connectCts = new CancellationTokenSource();
        _lastDecodedFrames = 0;

        try
        {
            await session.StartAsync(_connectCts.Token);
            if (_session != session) return; // remplacée pendant la connexion
            Title = $"Phone Mirror — {session.DeviceName}";
            if (session.Control != null)
            {
                session.Control.ClipboardReceived += text => Dispatcher.InvokeAsync(() => SetPcClipboard(text));
                if (_settings.TurnScreenOff) session.Control.SetDisplayPower(false);
                if (_options.OnlyApp is { } app) session.Control.StartApp(app);
            }
            _appPause.Start(session, _options);
            HideStatus();
            _sessionStartedAt = DateTime.UtcNow;
            _statsTimer.Start();
        }
        catch (OperationCanceledException)
        {
            // Fenêtre fermée ou réglages changés pendant la connexion.
        }
        catch (Exception ex)
        {
            // Session déjà remplacée (réglages changés) : ne pas toucher à la nouvelle.
            if (_session != session) return;

            AppLog.Write("Connexion impossible : " + ex);
            await StopSessionAsync();

            // Téléphone absent ou non autorisé : on attend qu'il soit prêt.
            // Les autres erreurs (fichier manquant, FFmpeg…) demandent une action de l'utilisateur.
            if (ex is AdbException)
                WaitForDeviceThenReconnect("Téléphone non détecté", ex.Message);
            else
                ShowStatus("Connexion impossible", ex.Message, canRetry: true);
        }
    }

    private async void OnSessionFaulted(MirrorSession session, Exception error)
    {
        if (_session != session) return;
        await StopSessionAsync();

        bool quickFailure = DateTime.UtcNow - _sessionStartedAt < StableSessionDuration;
        _quickFailures = quickFailure ? _quickFailures + 1 : 0;

        if (_quickFailures >= MaxQuickFailures)
        {
            AppLog.Write("Reconnexion automatique suspendue après plusieurs échecs rapprochés.");
            _quickFailures = 0;
            ShowStatus("Connexion perdue", error.Message, canRetry: true);
            return;
        }

        WaitForDeviceThenReconnect("Connexion perdue", error.Message);
    }

    /// <summary>Affiche l'erreur puis se reconnecte tout seul dès que le téléphone est prêt.</summary>
    private async void WaitForDeviceThenReconnect(string title, string detail)
    {
        CancelWaitForDevice();
        var cts = new CancellationTokenSource();
        _waitCts = cts;

        ShowStatus(title, detail + "\n\nReconnexion automatique dès que le téléphone sera branché et autorisé.",
            canRetry: true);
        AppLog.Write("En attente du téléphone…");

        try
        {
            await _deviceWatcher.WaitForDeviceAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return; // fenêtre fermée ou « Réessayer » cliqué
        }

        if (_waitCts != cts) return;
        _waitCts = null;
        cts.Dispose();

        AppLog.Write("Téléphone détecté, reconnexion.");
        await ConnectAsync();
    }

    private void CancelWaitForDevice()
    {
        CancellationTokenSource? cts = _waitCts;
        _waitCts = null;
        cts?.Cancel();
        cts?.Dispose();
    }

    private async Task StopSessionAsync()
    {
        _statsTimer.Stop();
        await _appPause.StopAsync();

        _connectCts?.Cancel();
        _connectCts?.Dispose();
        _connectCts = null;

        MirrorSession? session = _session;
        _session = null;
        if (session != null)
            await Task.Run(() => session.DisposeAsync().AsTask());
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        VideoFrame? frame = _session?.Frames.TryAcquireLatest();
        if (frame == null) return;

        if (_bitmap == null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
        {
            // Premier affichage ou rotation du téléphone.
            _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr32, null);
            VideoImage.Source = _bitmap;
            FitWindowToVideo(frame.Width, frame.Height);
        }

        _bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Pixels, frame.Stride, 0);
        _displayedFrames++;
    }

    /// <summary>
    /// Adapte la fenêtre aux proportions de la vidéo (au démarrage et à chaque rotation) :
    /// le plus grand côté de la zone d'affichage est conservé, dans la limite de l'écran.
    /// Sans cela, une vidéo paysage dans une fenêtre portrait serait fortement réduite.
    /// </summary>
    private void FitWindowToVideo(int videoWidth, int videoHeight)
    {
        if (WindowState != WindowState.Normal || Content is not FrameworkElement content) return;
        if (content.ActualWidth <= 0 || content.ActualHeight <= 0) return;

        double chromeWidth = ActualWidth - content.ActualWidth;
        double chromeHeight = ActualHeight - content.ActualHeight;
        Rect area = SystemParameters.WorkArea;

        double scale = Math.Max(content.ActualWidth, content.ActualHeight) / Math.Max(videoWidth, videoHeight);
        scale = Math.Min(scale, (area.Width - chromeWidth) / videoWidth);
        scale = Math.Min(scale, (area.Height - chromeHeight) / videoHeight);

        double width = videoWidth * scale + chromeWidth;
        double height = videoHeight * scale + chromeHeight;

        // La fenêtre pivote autour de son centre, sans sortir de l'écran.
        double centerX = Left + ActualWidth / 2;
        double centerY = Top + ActualHeight / 2;
        Width = width;
        Height = height;
        Left = Math.Clamp(centerX - width / 2, area.Left, Math.Max(area.Left, area.Right - width));
        Top = Math.Clamp(centerY - height / 2, area.Top, Math.Max(area.Top, area.Bottom - height));
    }

    private void UpdateStats()
    {
        MirrorSession? session = _session;
        if (session == null) return;

        long decoded = session.DecodedFrames;
        string size = _bitmap == null ? "—" : $"{_bitmap.PixelWidth}×{_bitmap.PixelHeight}";

        StatsText.Text =
            $"{size}\n" +
            $"décodées  {decoded - _lastDecodedFrames,3} i/s\n" +
            $"affichées {_displayedFrames - _lastDisplayedFrames,3} i/s\n" +
            $"erreurs   {session.DecodeErrors}";

        if (++_statsTicks % 5 == 0)
            AppLog.Write(StatsText.Text.Replace('\n', ' ').Replace("  ", " "));

        _lastDecodedFrames = decoded;
        _lastDisplayedFrames = _displayedFrames;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            StatsText.Visibility = StatsText.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        else if (e.Key == Key.F3)
        {
            OpenSettings();
        }
        else if (e.Key == Key.F11)
        {
            _fullScreen.Toggle();
        }
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e) => await ConnectAsync();

    private void OnSettingsClick(object sender, RoutedEventArgs e) => OpenSettings();

    /// <summary>Ouvre les réglages ; s'ils changent, ils sont enregistrés et la recopie redémarre.</summary>
    private async void OpenSettings()
    {
        var dialog = new SettingsWindow(_settings, MirrorOptions.CreateDefault()) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == _settings) return;

        UserSettings previous = _settings;
        _settings = dialog.Result;
        SaveSettings(showErrors: true);
        AppLog.Write($"Nouveaux réglages : {_settings}");

        // Réglages sans effet sur la vidéo : appliqués sans couper la recopie.
        Topmost = _settings.AlwaysOnTop;
        if (_settings.TurnScreenOff != previous.TurnScreenOff)
            _session?.Control?.SetDisplayPower(!_settings.TurnScreenOff);

        MirrorOptions options = _settings.ApplyTo(MirrorOptions.CreateDefault());
        if (options == _options) return;

        _options = options;
        _deviceWatcher = new DeviceWatcher(_options);
        _quickFailures = 0;

        CancelWaitForDevice();
        await StopSessionAsync();
        await ConnectAsync();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _statsTimer.Stop();

        Rect bounds = RestoreBounds; // taille « normale », même si la fenêtre est maximisée ou en plein écran
        if (!bounds.IsEmpty)
        {
            _settings = _settings with { Window = new WindowBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height) };
            SaveSettings(showErrors: false);
        }
        _connectCts?.Cancel();
        CancelWaitForDevice();

        MirrorSession? session = _session;
        _session = null;

        // Arrêt synchrone mais borné : on ne bloque jamais la fermeture plus de 3 secondes.
        if (session != null)
            Task.Run(() => session.DisposeAsync().AsTask()).Wait(TimeSpan.FromSeconds(3));
    }

    private void ShowStatus(string title, string detail, bool canRetry)
    {
        StatusTitle.Text = title;
        StatusDetail.Text = detail;
        RetryButtons.Visibility = canRetry ? Visibility.Visible : Visibility.Collapsed;
        StatusOverlay.Visibility = Visibility.Visible;
    }

    private void HideStatus()
    {
        StatusOverlay.Visibility = Visibility.Collapsed;
        VideoImage.Focus(); // le clavier va au téléphone
    }

    private void SaveSettings(bool showErrors)
    {
        try
        {
            _settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write("Enregistrement des réglages impossible : " + ex.Message);
            if (showErrors)
                MessageBox.Show(this, $"Les réglages s'appliquent mais n'ont pas pu être enregistrés :\n{ex.Message}",
                    "Phone Mirror", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Remet la fenêtre où elle était, si cet emplacement est encore visible (écran débranché…).</summary>
    private void RestoreWindowBounds()
    {
        if (_settings.Window is not { } saved) return;

        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var window = new Rect(saved.Left, saved.Top, saved.Width, saved.Height);
        if (!screen.IntersectsWith(window)) return;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = saved.Left;
        Top = saved.Top;
        Width = saved.Width;
        Height = saved.Height;
    }

    private async Task CheckForUpdatesAsync()
    {
        string? source = _settings.UpdateSource;
        string? version = await Task.Run(() => AppUpdater.DownloadPendingUpdateAsync(source));
        if (version != null)
            ShowToast($"La version {version} sera installée à la fermeture de Phone Mirror.");
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        Toast.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    /// <summary>Texte copié sur le téléphone : recopié dans le presse-papiers du PC.</summary>
    private static void SetPcClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        {
            AppLog.Write("Presse-papiers du PC indisponible : " + ex.Message);
        }
    }
}
