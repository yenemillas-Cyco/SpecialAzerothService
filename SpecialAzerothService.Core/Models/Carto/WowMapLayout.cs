namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>
/// Géométrie de <c>WowMap.png</c> : Azeroth à gauche, grille 3×2 des capitales à droite (une seule image).
/// Les cellules sur l'asset sont côte à côte (sans gap comme le JPEG composite exporté).
/// </summary>
public readonly record struct WowMapLayoutInfo(
    int FullWidth,
    int FullHeight,
    int WorldWidth,
    double CapitalsBandLeft,
    double CapitalsBandWidth,
    double CapitalsBandHeight)
{
    public double CapitalsBandTop => 0;
}

public static class WowMapLayout
{
    public static WowMapLayoutInfo Measure(int fullWidth, int fullHeight)
    {
        if (fullWidth <= 0) fullWidth = 1024;
        if (fullHeight <= 0) fullHeight = 768;

        var scale = fullHeight / (double)CapitalMapsCompositeLayout.PixelHeight;
        var capBandW = CapitalMapsCompositeLayout.PixelWidth * scale;
        var worldW = (int)Math.Round(Math.Max(1, fullWidth - capBandW));
        var capLeft = worldW;

        return new WowMapLayoutInfo(
            fullWidth,
            fullHeight,
            worldW,
            capLeft,
            capBandW,
            fullHeight);
    }

    public static (double X, double Y, double Width, double Height) GetCapitalsBandRect(int fullWidth, int fullHeight)
    {
        var m = Measure(fullWidth, fullHeight);
        return (m.CapitalsBandLeft, m.CapitalsBandTop, m.CapitalsBandWidth, m.CapitalsBandHeight);
    }

    /// <summary>Cellule 3×2 sur WowMap.png (même géométrie que capitals-composite.jpg, gaps inclus).</summary>
    public static (double X, double Y, double Width, double Height) GetCapitalCellRect(
        int fullWidth,
        int fullHeight,
        int gridColumn,
        int gridRow)
    {
        var (bx, by, bw, bh) = GetCapitalsBandRect(fullWidth, fullHeight);
        var (nx, ny, nw, nh) = CapitalMapsCompositeLayout.GetCellNormalizedRect(gridColumn, gridRow);
        return (
            bx + nx * bw,
            by + ny * bh,
            nw * bw,
            nh * bh);
    }

    /// <summary>Rectangle pixel sur WowMap.png (coords 0–1 sur l'image entière).</summary>
    public static (double X, double Y, double Width, double Height) ZoneRectToPixels(
        int fullWidth,
        int fullHeight,
        double left,
        double top,
        double width,
        double height) =>
        (
            left * fullWidth,
            top * fullHeight,
            width * fullWidth,
            height * fullHeight);

    /// <summary>Ancien format tuile 0–1 — conversion vers coords image entière.</summary>
    public static (double Left, double Top, double Width, double Height) TileRelativeToFullMapNorm(
        int fullWidth,
        int fullHeight,
        int mapId,
        double tileLeft,
        double tileTop,
        double tileWidth,
        double tileHeight)
    {
        var def = CapitalMapDefinitions.All.FirstOrDefault(d => d.MapId == mapId);
        if (def == null || fullWidth <= 0 || fullHeight <= 0)
            return (tileLeft, tileTop, tileWidth, tileHeight);

        var (cx, cy, cw, ch) = GetCapitalCellRect(fullWidth, fullHeight, def.GridColumn, def.GridRow);
        var px = cx + tileLeft * cw;
        var py = cy + tileTop * ch;
        var pw = tileWidth * cw;
        var ph = tileHeight * ch;
        return (px / fullWidth, py / fullHeight, pw / fullWidth, ph / fullHeight);
    }

    public static bool LooksLikeTileRelativeCapitalRect(
        double left,
        double top,
        double width,
        double height) =>
        width > 0 && width <= 0.42 && height > 0 && height <= 0.42
        && left >= 0 && left <= 0.75 && top >= 0 && top <= 0.75;

    /// <summary>Coords in-game 0–1 dans une capitale → position 0–1 sur WowMap.png entier.</summary>
    public static bool TryProjectCapitalZoneToFullMapNorm(
        int capitalMapId,
        double zoneX,
        double zoneY,
        int fullWidth,
        int fullHeight,
        out double mapX,
        out double mapY)
    {
        mapX = 0;
        mapY = 0;
        if (fullWidth <= 0 || fullHeight <= 0)
            return false;

        var def = CapitalMapDefinitions.All.FirstOrDefault(d => d.MapId == capitalMapId);
        if (def == null)
            return false;

        var (cellX, cellY, cellW, cellH) = GetCapitalCellRect(
            fullWidth,
            fullHeight,
            def.GridColumn,
            def.GridRow);

        mapX = Math.Clamp((cellX + zoneX * cellW) / fullWidth, 0, 1);
        mapY = Math.Clamp((cellY + zoneY * cellH) / fullHeight, 0, 1);
        return true;
    }
}
