using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PhoneMirror.Core;
using PhoneMirror.UI;

namespace PhoneMirror;

/// <summary>Fenêtre de réglages : téléphone, résolution, débit et images par seconde.</summary>
public partial class SettingsWindow : Window
{
    private static readonly int[] Sizes = [0, 2560, 1920, 1600, 1280, 1024, 800];
    private static readonly int[] BitRates = [4, 8, 16, 24, 32, 48];
    private static readonly int[] FpsValues = [30, 60, 90, 120];

    private readonly UserSettings _current;
    private readonly string _adbPath;

    /// <summary>Réglages choisis, valables si la fenêtre a renvoyé true.</summary>
    public UserSettings Result { get; private set; }

    public SettingsWindow(UserSettings current, string adbPath)
    {
        InitializeComponent();
        _current = current;
        _adbPath = adbPath;
        Result = current;

        Fill(SizeBox, Sizes, current.MaxSize, v => v == 0 ? "Native (la plus nette)" : $"{v} px");
        Fill(BitRateBox, BitRates, current.VideoBitRateMbps, v => $"{v} Mbit/s");
        Fill(FpsBox, FpsValues, current.MaxFps, v => $"{v} i/s");
        AudioBox.IsChecked = current.EnableAudio;
        ScreenOffBox.IsChecked = current.TurnScreenOff;
        TopmostBox.IsChecked = current.AlwaysOnTop;
        ShowClicksBox.IsChecked = current.ShowClicks;
        FillClickOptions(current);
        FillApps([], current.OnlyApp); // complétée une fois la liste lue sur le téléphone

        Loaded += async (_, _) => await LoadDevicesAsync();
        DeviceBox.SelectionChanged += async (_, _) => await LoadAppsAsync();
    }

    private void FillClickOptions(UserSettings current)
    {
        ClickStyleBox.Items.Add(new ComboBoxItem { Content = "Onde et point", Tag = ClickStyle.OndeEtPoint });
        ClickStyleBox.Items.Add(new ComboBoxItem { Content = "Onde seule", Tag = ClickStyle.OndeSeule });
        ClickStyleBox.Items.Add(new ComboBoxItem { Content = "Point seul", Tag = ClickStyle.PointSeul });
        ClickStyleBox.SelectedItem = ClickStyleBox.Items.Cast<ComboBoxItem>().First(i => (ClickStyle)i.Tag == current.ClickStyle);

        foreach (ClickColor color in Enum.GetValues<ClickColor>())
        {
            // Pastille de la couleur devant son nom.
            var swatch = new Ellipse
            {
                Width = 12, Height = 12, Margin = new Thickness(0, 0, 8, 0),
                Fill = new SolidColorBrush(ClickAppearance.ToColor(color)),
                Stroke = Brushes.Gray, StrokeThickness = 0.5,
            };
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(swatch);
            content.Children.Add(new TextBlock { Text = color.ToString(), Foreground = Brushes.Black });
            ClickColorBox.Items.Add(new ComboBoxItem { Content = content, Tag = color });
        }
        ClickColorBox.SelectedItem = ClickColorBox.Items.Cast<ComboBoxItem>().First(i => (ClickColor)i.Tag == current.ClickColor);

        foreach (ClickSize size in Enum.GetValues<ClickSize>())
            ClickSizeBox.Items.Add(new ComboBoxItem { Content = size.ToString(), Tag = size });
        ClickSizeBox.SelectedItem = ClickSizeBox.Items.Cast<ComboBoxItem>().First(i => (ClickSize)i.Tag == current.ClickSize);
    }

    /// <summary>Remplit une liste ; la valeur actuelle est ajoutée si elle n'y figure pas (fichier modifié à la main).</summary>
    private static void Fill(ComboBox box, int[] values, int selected, Func<int, string> label)
    {
        IEnumerable<int> all = values.Contains(selected) ? values : values.Append(selected);
        foreach (int value in all)
            box.Items.Add(new ComboBoxItem { Content = label(value), Tag = value });
        box.SelectedItem = box.Items.Cast<ComboBoxItem>().First(i => (int)i.Tag == selected);
    }

    private async Task LoadDevicesAsync()
    {
        string? selected = DeviceBox.SelectedItem is ComboBoxItem item ? item.Tag as string : _current.DeviceSerial;
        DeviceBox.Items.Clear();
        DeviceBox.Items.Add(new ComboBoxItem { Content = "Automatique (le seul téléphone branché)", Tag = null });

        IReadOnlyList<AdbDevice> devices;
        try
        {
            devices = await AdbDevices.ListAsync(_adbPath, CancellationToken.None);
        }
        catch (AdbException ex)
        {
            AppLog.Write("Liste des appareils impossible : " + ex.Message);
            devices = [];
        }

        foreach (AdbDevice device in devices)
        {
            string name = device.Model ?? device.Serial;
            string state = device.IsReady ? "" : $" — {DescribeState(device.State)}";
            DeviceBox.Items.Add(new ComboBoxItem { Content = $"{name} ({device.Serial}){state}", Tag = device.Serial });
        }

        if (selected != null && devices.All(d => d.Serial != selected))
            DeviceBox.Items.Add(new ComboBoxItem { Content = $"{selected} — non branché", Tag = selected });

        DeviceBox.SelectedItem = DeviceBox.Items.Cast<ComboBoxItem>().First(i => (string?)i.Tag == selected);
    }

    /// <summary>Applications proposées pour le mode « une seule application », lues sur le téléphone choisi.</summary>
    private async Task LoadAppsAsync()
    {
        string? selected = AppBox.SelectedItem is ComboBoxItem item ? item.Tag as string : _current.OnlyApp;
        string? serial = (DeviceBox.SelectedItem as ComboBoxItem)?.Tag as string;

        IReadOnlyList<string> apps;
        try
        {
            apps = await PhoneApps.ListLaunchableAsync(_adbPath, serial, CancellationToken.None);
        }
        catch (AdbException ex)
        {
            AppLog.Write("Liste des applications impossible : " + ex.Message);
            apps = [];
        }

        FillApps(apps, selected);
    }

    private void FillApps(IReadOnlyList<string> apps, string? selected)
    {
        AppBox.Items.Clear();
        AppBox.Items.Add(new ComboBoxItem { Content = "Tout l'écran du téléphone", Tag = null });
        foreach (string app in selected == null || apps.Contains(selected) ? apps : apps.Append(selected))
            AppBox.Items.Add(new ComboBoxItem { Content = $"Seulement {app}", Tag = app });
        AppBox.SelectedItem = AppBox.Items.Cast<ComboBoxItem>().First(i => (string?)i.Tag == selected);
    }

    private static string DescribeState(string state) => state switch
    {
        "unauthorized" => "autorisation à accepter sur le téléphone",
        "offline" => "hors ligne",
        _ => state,
    };

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await LoadDevicesAsync();

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        Result = _current with
        {
            DeviceSerial = (DeviceBox.SelectedItem as ComboBoxItem)?.Tag as string,
            OnlyApp = (AppBox.SelectedItem as ComboBoxItem)?.Tag as string,
            MaxSize = SelectedValue(SizeBox),
            VideoBitRateMbps = SelectedValue(BitRateBox),
            MaxFps = SelectedValue(FpsBox),
            EnableAudio = AudioBox.IsChecked == true,
            TurnScreenOff = ScreenOffBox.IsChecked == true,
            AlwaysOnTop = TopmostBox.IsChecked == true,
            ShowClicks = ShowClicksBox.IsChecked == true,
            ClickStyle = (ClickStyle)((ComboBoxItem)ClickStyleBox.SelectedItem).Tag,
            ClickColor = (ClickColor)((ComboBoxItem)ClickColorBox.SelectedItem).Tag,
            ClickSize = (ClickSize)((ComboBoxItem)ClickSizeBox.SelectedItem).Tag,
        };
        DialogResult = true;
    }

    private static int SelectedValue(ComboBox box) => (int)((ComboBoxItem)box.SelectedItem).Tag;
}
