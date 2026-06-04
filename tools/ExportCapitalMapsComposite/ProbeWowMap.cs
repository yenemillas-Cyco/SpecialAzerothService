using System.Windows.Media.Imaging;
using SpecialAzerothService.Core.Models.Carto;

namespace ExportCapitalMapsComposite;

internal static class ProbeWowMap
{
    public static void Run(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "WindowsOrganiserApp", "Assets", "WowMap.png");
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();

        var w = bmp.PixelWidth;
        var h = bmp.PixelHeight;
        var m = WowMapLayout.Measure(w, h);
        var tb = WowMapLayout.GetCapitalCellRect(w, h, 1, 0);

        Console.WriteLine($"WowMap {w}x{h}");
        Console.WriteLine($"Layout world={m.WorldWidth} capLeft={m.CapitalsBandLeft:F1} capW={m.CapitalsBandWidth:F1}");
        Console.WriteLine($"Thunder Bluff cell x={tb.X:F1} y={tb.Y:F1} w={tb.Width:F1} h={tb.Height:F1}");
    }
}
