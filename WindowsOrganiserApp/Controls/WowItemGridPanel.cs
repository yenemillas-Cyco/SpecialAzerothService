using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsOrganiserApp.Models.WowSync;

namespace WindowsOrganiserApp.Controls;

public static class WowItemGridPanel
{
    public static FrameworkElement Build(string title, IReadOnlyList<WowItem> items)
    {
        var root = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        root.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = new FontFamily("Georgia"),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
            Margin = new Thickness(0, 0, 0, 6)
        });

        if (items.Count == 0)
        {
            root.Children.Add(new TextBlock
            {
                Text = "— vide —",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0x90, 0x70)),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 0, 0, 2)
            });
            return root;
        }

        var wrap = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var item in items)
            wrap.Children.Add(new WowItemSlot { DataContext = item, Margin = new Thickness(2) });

        root.Children.Add(wrap);
        return root;
    }
}
