using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WindowsOrganiserApp.Controls;

/// <summary>Affichage or / argent / cuivre façon WoW : montant puis icône de pièce.</summary>
public static class WowCurrencyDisplay
{
    private static readonly ConcurrentDictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Brush AmountBrush = Brushes.White;

    private const string GoldIconSlug = "inv_misc_coin_01";
    private const string SilverIconSlug = "inv_misc_coin_03";
    private const string CopperIconSlug = "inv_misc_coin_05";

    public static StackPanel Build(long copperTotal, int iconSize = 16, int fontSize = 12)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var gold = (int)(copperTotal / 10000);
        var silver = (int)((copperTotal % 10000) / 100);
        var copper = (int)(copperTotal % 100);

        if (gold > 0)
            row.Children.Add(BuildCoinGroup(gold, CoinKind.Gold, iconSize, fontSize));
        if (silver > 0)
            row.Children.Add(BuildCoinGroup(silver, CoinKind.Silver, iconSize, fontSize));
        if (copper > 0 || row.Children.Count == 0)
            row.Children.Add(BuildCoinGroup(copper, CoinKind.Copper, iconSize, fontSize));

        return row;
    }

    private enum CoinKind { Gold, Silver, Copper }

    private static StackPanel BuildCoinGroup(int amount, CoinKind kind, int iconSize, int fontSize)
    {
        var group = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        group.Children.Add(new TextBlock
        {
            Text = amount.ToString(CultureInfo.InvariantCulture),
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = AmountBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 3, 0)
        });

        group.Children.Add(CreateCoinIcon(kind, iconSize));

        return group;
    }

    private static UIElement CreateCoinIcon(CoinKind kind, int size)
    {
        var slug = kind switch
        {
            CoinKind.Gold => GoldIconSlug,
            CoinKind.Silver => SilverIconSlug,
            _ => CopperIconSlug
        };

        var grid = new Grid
        {
            Width = size,
            Height = size,
            VerticalAlignment = VerticalAlignment.Center
        };

        grid.Children.Add(CreateCoinFallback(kind, size));

        var img = new Image
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true
        };
        grid.Children.Add(img);

        if (IconCache.TryGetValue(slug, out var cached) && cached != null)
            img.Source = cached;
        else
            _ = LoadIconAsync(slug, img);

        return grid;
    }

    private static Ellipse CreateCoinFallback(CoinKind kind, int size)
    {
        var (center, edge) = kind switch
        {
            CoinKind.Gold => (Color.FromRgb(255, 215, 0), Color.FromRgb(184, 134, 11)),
            CoinKind.Silver => (Color.FromRgb(232, 232, 240), Color.FromRgb(140, 140, 155)),
            _ => (Color.FromRgb(210, 120, 70), Color.FromRgb(120, 65, 35))
        };

        return new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.35, 0.35),
                Center = new Point(0.35, 0.35),
                RadiusX = 0.65,
                RadiusY = 0.65,
                GradientStops =
                {
                    new GradientStop(center, 0),
                    new GradientStop(edge, 1)
                }
            },
            Stroke = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            StrokeThickness = 0.6
        };
    }

    private static async Task LoadIconAsync(string iconSlug, Image target)
    {
        try
        {
            if (IconCache.TryGetValue(iconSlug, out var cached) && cached != null)
            {
                await target.Dispatcher.InvokeAsync(() => target.Source = cached);
                return;
            }

            var url = $"https://wow.zamimg.com/images/wow/icons/small/{iconSlug}.jpg";
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(url, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            IconCache[iconSlug] = bitmap;

            await target.Dispatcher.InvokeAsync(() => target.Source = bitmap);
        }
        catch
        {
            // Le dégradé sous-jacent reste visible
        }
    }
}
