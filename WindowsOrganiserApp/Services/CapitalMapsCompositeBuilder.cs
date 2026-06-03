using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SpecialAzerothService.Core.Models.Carto;

namespace WindowsOrganiserApp.Services;

/// <summary>Assemble l'image ressource des capitales (géométrie = <see cref="CapitalMapsCompositeLayout"/>).</summary>
public static class CapitalMapsCompositeBuilder
{
    public sealed record BuildResult(BitmapSource Composite, IReadOnlyDictionary<int, BitmapSource> SourcesByMapId);

    public static BuildResult BuildFromCapitalsFolder(string capitalsDirectory) =>
        Build(capitalsDirectory);

    public static BuildResult Build() => Build(capitalsDirectory: null);

    public static void SaveCompositeJpeg(string outputPath, string capitalsDirectory)
    {
        var result = Build(capitalsDirectory);
        var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
        encoder.Frames.Add(BitmapFrame.Create(result.Composite));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }

    public static BitmapImage LoadCompositeFromPackResource()
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(
            $"pack://application:,,,/Assets/Capitals/{CapitalMapsCompositeLayout.CompositeAssetFileName}");
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public static IReadOnlyDictionary<int, BitmapSource> LoadCapitalSourcesFromPack()
    {
        var sources = new Dictionary<int, BitmapSource>();
        foreach (var def in CapitalMapDefinitions.All)
            sources[def.MapId] = LoadCapitalBitmap(def.AssetFileName, capitalsDirectory: null);
        return sources;
    }

    private static BuildResult Build(string? capitalsDirectory)
    {
        var sources = new Dictionary<int, BitmapSource>();
        foreach (var def in CapitalMapDefinitions.All)
            sources[def.MapId] = LoadCapitalBitmap(def.AssetFileName, capitalsDirectory);

        var width = CapitalMapsCompositeLayout.PixelWidth;
        var height = CapitalMapsCompositeLayout.PixelHeight;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)), null, new Rect(0, 0, width, height));

            foreach (var def in CapitalMapDefinitions.All)
            {
                if (!sources.TryGetValue(def.MapId, out var bmp))
                    continue;

                var cell = GetCellPixelRect(def.GridColumn, def.GridRow);
                DrawImageUniform(dc, bmp, cell);
            }
        }

        var composite = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        composite.Render(visual);
        composite.Freeze();

        foreach (var key in sources.Keys.ToList())
        {
            if (sources[key].CanFreeze)
                sources[key].Freeze();
        }

        return new BuildResult(composite, sources);
    }

    public static Rect GetCellPixelRect(int gridColumn, int gridRow)
    {
        var x = gridColumn * (CapitalMapsCompositeLayout.CellPixelSize + CapitalMapsCompositeLayout.CellGapPixels);
        var y = gridRow * (CapitalMapsCompositeLayout.CellPixelSize + CapitalMapsCompositeLayout.CellGapPixels);
        return new Rect(x, y, CapitalMapsCompositeLayout.CellPixelSize, CapitalMapsCompositeLayout.CellPixelSize);
    }

    private static void DrawImageUniform(DrawingContext dc, BitmapSource image, Rect bounds)
    {
        var scale = Math.Min(bounds.Width / image.PixelWidth, bounds.Height / image.PixelHeight);
        var w = image.PixelWidth * scale;
        var h = image.PixelHeight * scale;
        var dest = new Rect(
            bounds.X + (bounds.Width - w) / 2,
            bounds.Y + (bounds.Height - h) / 2,
            w,
            h);
        dc.DrawImage(image, dest);
    }

    private static BitmapImage LoadCapitalBitmap(string assetFileName, string? capitalsDirectory)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = capitalsDirectory != null
            ? new Uri(Path.Combine(capitalsDirectory, assetFileName), UriKind.Absolute)
            : new Uri($"pack://application:,,,/Assets/Capitals/{assetFileName}");
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
