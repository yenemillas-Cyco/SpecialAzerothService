using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Controls;

public static class WowClassIcon
{
    private static readonly Dictionary<WowClass, string> IconFileByClass = new()
    {
        [WowClass.Guerrier] = "class_warrior.jpg",
        [WowClass.Paladin] = "class_paladin.jpg",
        [WowClass.Chasseur] = "class_hunter.jpg",
        [WowClass.Voleur] = "class_rogue.jpg",
        [WowClass.Pretre] = "class_priest.jpg",
        [WowClass.Chaman] = "class_shaman.jpg",
        [WowClass.Mage] = "class_mage.jpg",
        [WowClass.Demoniste] = "class_warlock.jpg",
        [WowClass.Druide] = "class_druid.jpg"
    };

    private static readonly Dictionary<WowClass, BitmapImage?> Cache = new();

    public static Image Create(WowClass wowClass, int size = 28)
    {
        var image = new Image
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true,
            VerticalAlignment = VerticalAlignment.Center
        };

        var source = GetBitmap(wowClass);
        if (source != null)
            image.Source = source;

        return image;
    }

    public static BitmapImage? GetBitmap(WowClass wowClass)
    {
        if (Cache.TryGetValue(wowClass, out var cached))
            return cached;

        if (!IconFileByClass.TryGetValue(wowClass, out var file))
        {
            Cache[wowClass] = null;
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/Assets/Classes/{file}", UriKind.Absolute);
            bitmap.DecodePixelWidth = 64;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            Cache[wowClass] = bitmap;
            return bitmap;
        }
        catch
        {
            Cache[wowClass] = null;
            return null;
        }
    }
}
