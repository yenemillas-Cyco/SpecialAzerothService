using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SpecialAzerothService.Core.Models.Bounty;
using SpecialAzerothService.Core.Services;

namespace WindowsOrganiserApp.Controls;

/// <summary>Portrait de classe dans un cadre dragon Elite WoW (or / argent / bronze selon le rang).</summary>
public static class BountyElitePortrait
{
    private enum FrameVariant { Gold, Silver, Bronze }

    private static readonly Dictionary<FrameVariant, BitmapSource?> FrameCache = new();

    public static UIElement Build(BountyEntry bounty, int rank, double size)
    {
        var variant = rank switch
        {
            1 => FrameVariant.Gold,
            2 => FrameVariant.Silver,
            _ => FrameVariant.Bronze
        };

        var host = new Grid
        {
            Width = size,
            Height = size,
            Margin = new Thickness(0, 0, 0, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = Brushes.Transparent
        };

        // Disque noir centré un peu plus haut pour éviter la coupe par la queue du dragon.
        var portraitBackdrop = size * 0.62;
        var iconDiameter = size * 0.38;
        var portraitLift = size * 0.028;

        var portraitHost = new Grid
        {
            Width = portraitBackdrop,
            Height = portraitBackdrop,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -portraitLift, 0, portraitLift)
        };
        portraitHost.Children.Add(new Ellipse
        {
            Width = portraitBackdrop,
            Height = portraitBackdrop,
            Fill = new SolidColorBrush(Color.FromRgb(6, 6, 6))
        });
        portraitHost.Children.Add(BuildClassIcon(bounty, iconDiameter));
        host.Children.Add(portraitHost);
        host.Children.Add(BuildDragonFrame(size, variant));

        return host;
    }

    private static UIElement BuildClassIcon(BountyEntry bounty, double diameter)
    {
        var wowClass = CartoSyncMapper.ParseClass(bounty.TargetClass);
        var icon = WowClassIcon.Create(wowClass, (int)diameter);
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);

        return new Border
        {
            Width = diameter,
            Height = diameter,
            CornerRadius = new CornerRadius(diameter / 2),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromRgb(10, 10, 10)),
            Child = icon
        };
    }

    private static UIElement BuildDragonFrame(double size, FrameVariant variant)
    {
        var frame = GetFrameBitmap(variant);
        if (frame == null)
        {
            return new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = Brushes.Goldenrod,
                StrokeThickness = 3,
                Fill = Brushes.Transparent
            };
        }

        var image = new Image
        {
            Source = frame,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            SnapsToDevicePixels = false
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return image;
    }

    private static BitmapSource? GetFrameBitmap(FrameVariant variant)
    {
        if (FrameCache.TryGetValue(variant, out var cached))
            return cached;

        var built = BuildFrameBitmap(variant);
        FrameCache[variant] = built;
        return built;
    }

    private static BitmapSource? BuildFrameBitmap(FrameVariant variant)
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/elite_portrait_frame.png", UriKind.Absolute);
            var decoder = BitmapDecoder.Create(
                uri,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
                return null;

            var source = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
            var width = source.PixelWidth;
            var height = source.PixelHeight;
            var stride = width * 4;
            var pixels = new byte[stride * height];
            source.CopyPixels(pixels, stride, 0);

            PunchPortraitHole(pixels, width, height);
            RemoveNearBlackBackground(pixels);
            if (variant != FrameVariant.Gold)
                TintFramePixels(pixels, variant);

            var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, stride);
            result.Freeze();
            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Perce le centre opaque du PNG pour laisser voir l'icône de classe.</summary>
    private static void PunchPortraitHole(byte[] pixels, int width, int height)
    {
        var cx = width / 2.0;
        var cy = height / 2.0;
        var radius = Math.Min(width, height) * 0.368;
        var feather = 0.6;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * width * 4 + x * 4;
                if (pixels[i + 3] == 0)
                    continue;

                var dx = x - cx;
                var dy = y - cy;
                var dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist >= radius + feather)
                    continue;

                if (dist <= radius - feather)
                {
                    pixels[i + 3] = 0;
                    continue;
                }

                var t = (radius + feather - dist) / (2 * feather);
                pixels[i + 3] = (byte)(pixels[i + 3] * Math.Clamp(t, 0, 1));
            }
        }
    }

    /// <summary>Retire le fond noir carré du PNG (coins du bitmap).</summary>
    private static void RemoveNearBlackBackground(byte[] pixels)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == 0)
                continue;

            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];
            if (r > 32 || g > 32 || b > 32)
                continue;

            var lum = 0.299 * r + 0.587 * g + 0.114 * b;
            if (lum < 24)
                pixels[i + 3] = 0;
        }
    }

    private static void TintFramePixels(byte[] pixels, FrameVariant variant)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3];
            if (alpha == 0)
                continue;

            var r = pixels[i + 2];
            var g = pixels[i + 1];
            var b = pixels[i];
            var lum = 0.299 * r + 0.587 * g + 0.114 * b;

            if (variant == FrameVariant.Silver)
            {
                var s = (byte)Math.Clamp(lum * 1.08, 0, 255);
                pixels[i] = pixels[i + 1] = pixels[i + 2] = s;
            }
            else
            {
                pixels[i + 2] = (byte)Math.Clamp(lum * 1.18, 0, 255);
                pixels[i + 1] = (byte)Math.Clamp(lum * 0.80, 0, 255);
                pixels[i] = (byte)Math.Clamp(lum * 0.48, 0, 255);
            }
        }
    }

}
