using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PhoneMirror.UI;

namespace PhoneMirror;

/// <summary>Aide : tous les raccourcis souris et clavier (F1, ou bouton « ? » des réglages).</summary>
public partial class ShortcutsWindow : Window
{
    private static readonly Brush TitleBrush = Frozen(0x22, 0xD3, 0xEE);
    private static readonly Brush KeyBrush = Frozen(0xFF, 0xFF, 0xFF);
    private static readonly Brush EffectBrush = Frozen(0xB8, 0xBC, 0xC6);

    public ShortcutsWindow()
    {
        InitializeComponent();
        foreach (ShortcutGroup group in ShortcutCatalog.Groups)
            GroupsPanel.Children.Add(CreateGroup(group));
    }

    private static UIElement CreateGroup(ShortcutGroup group)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        panel.Children.Add(new TextBlock
        {
            Text = group.Title.ToUpperInvariant(),
            Foreground = TitleBrush,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6),
        });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        foreach (Shortcut item in group.Items)
        {
            int row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition());
            grid.Children.Add(Cell(item.Keys, KeyBrush, FontWeights.SemiBold, row, 0));
            grid.Children.Add(Cell(item.Effect, EffectBrush, FontWeights.Normal, row, 1));
        }
        panel.Children.Add(grid);
        return panel;
    }

    private static TextBlock Cell(string text, Brush brush, FontWeight weight, int row, int column)
    {
        var cell = new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontWeight = weight,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 12, 3),
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        return cell;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
