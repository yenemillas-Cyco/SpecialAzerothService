using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsOrganiserApp.Resources;

/// <summary>Icônes barre d'outils Craft (chargement pack:// différé, sans réseau).</summary>
public static class CraftToolbarIcons
{
    private static readonly Lazy<ImageSource?> CraftAdd = new(() => Load("trade_blacksmithing.jpg", 36));
    private static readonly Lazy<ImageSource?> CraftQuest = new(() => Load("quest-available.png", 32, useNearestNeighbor: true));
    private static readonly Lazy<ImageSource?> CraftGuide = new(() => Load("craft-guide.jpg", 36));

    public static ImageSource? CraftAddIcon => CraftAdd.Value;
    public static ImageSource? CraftQuestIcon => CraftQuest.Value;
    public static ImageSource? CraftGuideIcon => CraftGuide.Value;

    private static ImageSource? Load(string fileName, int decodePixelWidth, bool useNearestNeighbor = false)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/Assets/Craft/{fileName}", UriKind.Absolute);
            bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            if (useNearestNeighbor)
                RenderOptions.SetBitmapScalingMode(bitmap, BitmapScalingMode.NearestNeighbor);
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
