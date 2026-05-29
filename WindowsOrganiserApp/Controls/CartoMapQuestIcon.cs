using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;

namespace WindowsOrganiserApp.Controls;

/// <summary>
/// Icône légère pour la carte (cache mémoire, pas de requête détails Wowhead).
/// </summary>
public static class CartoMapQuestIcon
{
    private static readonly ConcurrentDictionary<int, ImageSource?> IconCache = new();

    public static Border Create(WowItem item, double size = 22, bool bordered = true)
    {
        var content = BuildIconContent(item, size);
        if (!bordered)
        {
            return new Border
            {
                Width = size,
                Height = size,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Child = content
            };
        }

        var borderColor = (Color)ColorConverter.ConvertFromString(item.QualityColor);
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(3),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1.5),
            Background = new SolidColorBrush(Color.FromRgb(24, 18, 12)),
            Padding = new Thickness(1),
            Child = content
        };
    }

    private static Grid BuildIconContent(WowItem item, double size)
    {
        var grid = new Grid();
        var img = new Image
        {
            Width = size - (item.ItemId > 0 ? 2 : 4),
            Height = size - (item.ItemId > 0 ? 2 : 4),
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var fallback = new TextBlock
        {
            Text = QuestEmoji(item.ItemId),
            FontSize = Math.Max(10, size * 0.55),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Visible
        };
        grid.Children.Add(fallback);
        grid.Children.Add(img);

        if (item.ItemId > 0)
        {
            if (IconCache.TryGetValue(item.ItemId, out var cached) && cached != null)
            {
                img.Source = cached;
                fallback.Visibility = Visibility.Collapsed;
            }
            else if (WowItemSlot.LookupService != null)
                _ = LoadIconAsync(item, img, fallback);
        }

        return grid;
    }

    public static void PreloadQuestStubIcons()
    {
        if (WowItemSlot.LookupService == null)
            return;

        foreach (QuestItemType type in Enum.GetValues(typeof(QuestItemType)))
        {
            var stub = CartoCharacterEnricher.QuestItemWowStub(type);
            if (stub.ItemId <= 0)
                continue;

            _ = PreloadIconAsync(stub);
        }
    }

    private static async Task PreloadIconAsync(WowItem item)
    {
        var lookup = WowItemSlot.LookupService;
        if (lookup == null || item.ItemId <= 0)
            return;

        try
        {
            var src = await lookup.GetIconAsync(item).ConfigureAwait(false);
            if (src != null)
                IconCache[item.ItemId] = src;
        }
        catch
        {
            // ignore
        }
    }

    private static string QuestEmoji(int itemId) => itemId switch
    {
        12630 => "🗣",
        18422 => "🗣",
        19002 => "🗣",
        19802 => "❤",
        _ => "?"
    };

    private static async Task LoadIconAsync(WowItem item, Image target, TextBlock fallback)
    {
        var lookup = WowItemSlot.LookupService;
        if (lookup == null || item.ItemId <= 0)
            return;

        try
        {
            if (IconCache.TryGetValue(item.ItemId, out var cached) && cached != null)
            {
                await target.Dispatcher.InvokeAsync(() =>
                {
                    target.Source = cached;
                    fallback.Visibility = Visibility.Collapsed;
                });
                return;
            }

            var src = await lookup.GetIconAsync(item).ConfigureAwait(false);
            if (src == null)
                return;

            IconCache[item.ItemId] = src;
            await target.Dispatcher.InvokeAsync(() =>
            {
                if (!target.IsVisible && target.Parent == null)
                    return;

                target.Source = src;
                fallback.Visibility = Visibility.Collapsed;
            });
        }
        catch
        {
            // emoji fallback reste visible
        }
    }
}
