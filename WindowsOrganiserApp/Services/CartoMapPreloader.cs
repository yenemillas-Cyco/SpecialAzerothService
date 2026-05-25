using System.Windows.Media.Imaging;

namespace WindowsOrganiserApp.Services;

/// <summary>Précharge WowMap.png une fois (splash + onglet Carto).</summary>
public static class CartoMapPreloader
{
    private static readonly object Gate = new();
    private static BitmapImage? _bitmap;
    private static int _pixelW;
    private static int _pixelH;

    public static bool IsLoaded => _bitmap != null;

    public static (int W, int H) PixelSize
    {
        get
        {
            EnsureLoaded();
            return (_pixelW, _pixelH);
        }
    }

    public static BitmapImage? GetBitmap()
    {
        EnsureLoaded();
        return _bitmap;
    }

    public static void EnsureLoaded()
    {
        if (_bitmap != null)
            return;

        lock (Gate)
        {
            if (_bitmap != null)
                return;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = CartoMapAssets.PackUri;
            bmp.EndInit();
            bmp.Freeze();
            _bitmap = bmp;
            _pixelW = bmp.PixelWidth;
            _pixelH = bmp.PixelHeight;
        }
    }
}
