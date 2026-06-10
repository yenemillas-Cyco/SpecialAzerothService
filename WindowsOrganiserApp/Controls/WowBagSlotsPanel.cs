using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpecialAzerothService.Core.Models.WowSync;

namespace WindowsOrganiserApp.Controls;

public static class WowBagSlotsPanel
{
    private static readonly SolidColorBrush TitleBrush = new(Color.FromRgb(0xFF, 0xD7, 0x00));
    private static readonly SolidColorBrush LabelBrush = new(Color.FromRgb(0xE8, 0xD5, 0xA3));
    private static readonly SolidColorBrush SubtextBrush = new(Color.FromRgb(0xA0, 0x90, 0x70));
    private static readonly SolidColorBrush FullBrush = new(Color.FromRgb(0xFF, 0x70, 0x70));
    private static readonly SolidColorBrush OkBrush = new(Color.FromRgb(0xC0, 0xA0, 0x60));

    public static FrameworkElement Build(string title, IReadOnlyList<WowBagContainer> bags)
    {
        var root = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        root.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = new FontFamily("Georgia"),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = TitleBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        if (bags.Count == 0)
        {
            root.Children.Add(new TextBlock
            {
                Text = "— non synchronisé (addon ≥ 1.7, /reload puis déco) —",
                FontSize = 10,
                Foreground = SubtextBrush,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 2)
            });
            return root;
        }

        var totalSlots = bags.Sum(b => b.Slots);
        var usedSlots = bags.Sum(b => b.UsedSlots);
        root.Children.Add(new TextBlock
        {
            Text = $"Total : {usedSlots}/{totalSlots} emplacements",
            FontFamily = new FontFamily("Georgia"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = usedSlots >= totalSlots && totalSlots > 0 ? FullBrush : OkBrush,
            Margin = new Thickness(0, 0, 0, 6)
        });

        foreach (var bag in bags)
            root.Children.Add(BuildBagRow(bag));

        return root;
    }

    private static UIElement BuildBagRow(WowBagContainer bag)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 3), LastChildFill = true };

        var capacity = new TextBlock
        {
            Text = bag.CapacityText,
            FontFamily = new FontFamily("Georgia"),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = bag.Slots > 0 && bag.UsedSlots >= bag.Slots ? FullBrush : OkBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        DockPanel.SetDock(capacity, Dock.Right);
        row.Children.Add(capacity);

        if (bag.BagItemId > 0)
        {
            var icon = new WowItemSlot
            {
                DataContext = new WowItem { ItemId = bag.BagItemId, Count = bag.Slots },
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
        }

        var labelPanel = new StackPanel { Orientation = Orientation.Vertical };
        labelPanel.Children.Add(new TextBlock
        {
            Text = bag.Label,
            FontFamily = new FontFamily("Georgia"),
            FontSize = 11,
            Foreground = LabelBrush
        });

        var detail = bag.Slots <= 0
            ? "emplacement vide"
            : bag.BagItemId > 0
                ? $"taille {bag.Slots}"
                : $"taille {bag.Slots} (fixe)";
        labelPanel.Children.Add(new TextBlock
        {
            Text = detail,
            FontFamily = new FontFamily("Georgia"),
            FontSize = 9,
            Foreground = SubtextBrush
        });
        row.Children.Add(labelPanel);

        return row;
    }
}
