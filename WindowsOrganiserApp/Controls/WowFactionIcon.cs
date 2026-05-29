using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SpecialAzerothService.Core.Models.Carto;

namespace WindowsOrganiserApp.Controls;

public static class WowFactionIcon
{
    private static BitmapSource? _alliance;
    private static BitmapSource? _horde;

    public static Faction? ResolveFaction(string? race)
    {
        if (string.IsNullOrWhiteSpace(race))
            return null;

        var r = race.Trim().ToLowerInvariant();

        // Noms anglais UnitRace (export WowSync) + français client
        if (r is "orc" or "tauren" or "troll"
            or "mort-vivant" or "mort vivant" or "undead" or "forsaken" or "scourge"
            or "réprouvé" or "reprouve")
            return Faction.Horde;

        if (r is "humain" or "human" or "nain" or "dwarf" or "gnome"
            or "elfe de la nuit" or "night elf" or "nightelf")
            return Faction.Alliance;

        if (r.Contains("orc") || r.Contains("tauren") || r.Contains("troll")
            || r.Contains("mort") || r.Contains("undead") || r.Contains("scourge")
            || r.Contains("réprouvé") || r.Contains("reprouve"))
            return Faction.Horde;

        if (r.Contains("humain") || r.Contains("human") || r.Contains("nain") || r.Contains("dwarf")
            || r.Contains("gnome") || r.Contains("elfe") || r.Contains("night"))
            return Faction.Alliance;

        return null;
    }

    public static Image? Create(Faction faction, int size)
    {
        var source = GetBitmap(faction);
        if (source == null)
            return null;

        var image = new Image
        {
            Source = source,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = false,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            ToolTip = faction == Faction.Horde ? "Horde" : "Alliance"
        };

        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return image;
    }

    private static BitmapSource? GetBitmap(Faction faction)
    {
        if (faction == Faction.Horde)
        {
            _horde ??= LoadBitmap("Horde.png");
            return _horde;
        }

        _alliance ??= LoadBitmap("Alliance.png");
        return _alliance;
    }

    private static BitmapSource? LoadBitmap(string file)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/{file}", UriKind.Absolute);
            var decoder = BitmapDecoder.Create(
                uri,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
                return null;

            var frame = decoder.Frames[0];
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            return MakeCircularMedallionTransparent(converted);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Retire uniquement le damier dans les coins (hors du médaillon circulaire).</summary>
    private static BitmapSource MakeCircularMedallionTransparent(BitmapSource source)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        var cx = width / 2.0;
        var cy = height / 2.0;
        var radius = Math.Min(width, height) * 0.495;
        var feather = 1.5;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * stride + x * 4;
                var dx = x - cx;
                var dy = y - cy;
                var dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist <= radius - feather)
                    continue;

                byte alpha = pixels[i + 3];
                if (dist >= radius + feather)
                {
                    pixels[i + 3] = 0;
                    continue;
                }

                var t = (radius + feather - dist) / (2 * feather);
                pixels[i + 3] = (byte)(alpha * Math.Clamp(t, 0, 1));
            }
        }

        var result = BitmapSource.Create(
            width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }
}
