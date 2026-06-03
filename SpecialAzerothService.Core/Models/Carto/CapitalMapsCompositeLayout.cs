namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>
/// Géométrie figée de l'image ressource <c>capitals-composite.jpg</c>.
/// Ne pas modifier sans régénérer l'image et recalibrer les zones capitales.
/// </summary>
public static class CapitalMapsCompositeLayout
{
    public const int GridColumns = 3;
    public const int GridRows = 2;
    public const int CellPixelSize = 280;
    public const int CellGapPixels = 2;
    public const string CompositeAssetFileName = "capitals-composite.jpg";

    public static int PixelWidth => GridColumns * CellPixelSize + (GridColumns - 1) * CellGapPixels;

    public static int PixelHeight => GridRows * CellPixelSize + (GridRows - 1) * CellGapPixels;

    /// <summary>Emplacement d'une cellule en coords 0–1 dans l'image composite (gaps inclus).</summary>
    public static (double X, double Y, double Width, double Height) GetCellNormalizedRect(int gridColumn, int gridRow)
    {
        var x = gridColumn * (CellPixelSize + CellGapPixels) / (double)PixelWidth;
        var y = gridRow * (CellPixelSize + CellGapPixels) / (double)PixelHeight;
        var w = CellPixelSize / (double)PixelWidth;
        var h = CellPixelSize / (double)PixelHeight;
        return (x, y, w, h);
    }
}
