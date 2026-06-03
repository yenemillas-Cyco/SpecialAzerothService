using SpecialAzerothService.Core.Models.Carto;

namespace SpecialAzerothService.Core.Services;

/// <summary>Dimensions carte monde + bloc capitales — calcul pur, pas de WPF.</summary>
public readonly record struct CartoMapDimensions(
    double WorldWidth,
    double WorldHeight,
    double CapitalsWidth,
    double CapitalsHeight,
    double TotalWidth,
    double TotalHeight)
{
    /// <summary>Hauteur minimale du bloc 3×2 pour les JPG capitales (~1878×1252).</summary>
    public static double ComputeCapitalsMinHeight(double capitalsPanelWidth)
    {
        if (capitalsPanelWidth <= 0)
            return 0;

        const double capitalAspect = 1878.0 / 1252.0;
        var cellWidth = capitalsPanelWidth / 3.0;
        var cellHeight = cellWidth / capitalAspect;
        return cellHeight * 2;
    }

    public static CartoMapDimensions FromWorldPixels(
        double mapPixelW,
        double mapPixelH,
        bool includeCapitals,
        double? maxViewportContentWidth = null)
    {
        if (mapPixelW <= 0) mapPixelW = 1024;
        if (mapPixelH <= 0) mapPixelH = 768;

        if (!includeCapitals)
            return new CartoMapDimensions(mapPixelW, mapPixelH, 0, 0, mapPixelW, mapPixelH);

        var preferredCapW = mapPixelW * CapitalMapDefinitions.PanelWidthScale;
        var capW = preferredCapW;

        if (maxViewportContentWidth is > 0)
        {
            var roomForCapitals = maxViewportContentWidth.Value - mapPixelW - CapitalMapDefinitions.PanelMarginLeft;
            if (roomForCapitals > 0)
                capW = Math.Min(preferredCapW, roomForCapitals);
        }

        var capH = Math.Max(
            mapPixelH * CapitalMapDefinitions.PanelHeightScale,
            ComputeCapitalsMinHeight(capW));
        var totalW = mapPixelW + CapitalMapDefinitions.PanelMarginLeft + capW;
        var totalH = Math.Max(mapPixelH, capH);
        return new CartoMapDimensions(mapPixelW, mapPixelH, capW, capH, totalW, totalH);
    }
}
