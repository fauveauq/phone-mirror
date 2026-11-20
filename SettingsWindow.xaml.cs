using System.Windows;
using System.Windows.Controls;
using PhoneMirror.Core;

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
        ScreenOffBox.IsChecked = current.TurnScreenOff;
        TopmostBox.IsChecked = current.AlwaysOnTop;

        Loaded += async (_, _) => await LoadDevicesAsync();
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
            MaxSize = SelectedValue(SizeBox),
            VideoBitRateMbps = SelectedValue(BitRateBox),
            MaxFps = SelectedValue(FpsBox),
            TurnScreenOff = ScreenOffBox.IsChecked == true,
            AlwaysOnTop = TopmostBox.IsChecked == true,
        };
        DialogResult = true;
    }

    private static int SelectedValue(ComboBox box) => (int)((ComboBoxItem)box.SelectedItem).Tag;
}
