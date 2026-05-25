using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Services;

/// <summary>Dimensions carte monde + bloc capitales — calcul pur, pas de WPF.</summary>
public readonly record struct CartoMapDimensions(
    double WorldWidth,
    double WorldHeight,
    double CapitalsWidth,
    double CapitalsHeight,
    double TotalWidth,
    double TotalHeight)
{
    public static CartoMapDimensions FromWorldPixels(double mapPixelW, double mapPixelH, bool includeCapitals)
    {
        if (mapPixelW <= 0) mapPixelW = 1024;
        if (mapPixelH <= 0) mapPixelH = 768;

        if (!includeCapitals)
            return new CartoMapDimensions(mapPixelW, mapPixelH, 0, 0, mapPixelW, mapPixelH);

        var capW = mapPixelW * CapitalMapDefinitions.PanelWidthScale;
        var capH = mapPixelH * CapitalMapDefinitions.PanelHeightScale;
        var totalW = mapPixelW + CapitalMapDefinitions.PanelMarginLeft + capW;
        return new CartoMapDimensions(mapPixelW, mapPixelH, capW, capH, totalW, mapPixelH);
    }
}
