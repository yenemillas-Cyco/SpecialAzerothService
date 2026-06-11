using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SpecialAzerothService.Core.Models.Bounty;

namespace WindowsOrganiserApp.Controls;

/// <summary>Montant de prime pour l'affiche screenshot : chiffre + pièce d'or.</summary>
public static class BountyPosterRewardDisplay
{
    private const string CoinSlug = "inv_misc_coin_01";

    private static readonly ConcurrentDictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    public static void WarmupIcons() => _ = GetCoinIcon();

    public static UIElement Build(BountyEntry bounty, int fontSize, int iconSize, bool center = true) =>
        Build(bounty.TotalGold, fontSize, iconSize, center);

    public static UIElement Build(int totalGold, int fontSize, int iconSize, bool center = true)
    {
        var tier = BountyTierHelper.GetTier(totalGold);
        var amountColor = (Color)ColorConverter.ConvertFromString(BountyTierHelper.GetForegroundHex(tier))!;
        var coinSize = Math.Max(10, (int)Math.Round(iconSize * 0.82));

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left
        };

        row.Children.Add(new TextBlock
        {
            Text = totalGold.ToString(CultureInfo.InvariantCulture),
            FontSize = fontSize,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Georgia"),
            Foreground = new SolidColorBrush(amountColor),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 3, 0)
        });

        row.Children.Add(CreateCoinIcon(coinSize));
        return row;
    }

    private static UIElement CreateCoinIcon(int size)
    {
        var host = new Grid
        {
            Width = size,
            Height = size,
            VerticalAlignment = VerticalAlignment.Center
        };

        host.Children.Add(CreateCoinFallback(size));

        var image = new Image
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true,
            Source = GetCoinIcon()
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        host.Children.Add(image);
        return host;
    }

    private static ImageSource? GetCoinIcon()
    {
        if (IconCache.TryGetValue(CoinSlug, out var cached))
            return cached;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"https://wow.zamimg.com/images/wow/icons/large/{CoinSlug}.jpg", UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            IconCache[CoinSlug] = bitmap;
            return bitmap;
        }
        catch
        {
            IconCache[CoinSlug] = null;
            return null;
        }
    }

    private static Ellipse CreateCoinFallback(int size) => new()
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
                new GradientStop(Color.FromRgb(255, 215, 0), 0),
                new GradientStop(Color.FromRgb(184, 134, 11), 1)
            }
        },
        Stroke = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
        StrokeThickness = 0.6
    };
}
