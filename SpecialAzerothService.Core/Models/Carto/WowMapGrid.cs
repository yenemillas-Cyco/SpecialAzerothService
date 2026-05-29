namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>
/// Référence pour calibrer WowMap.png à partir de Wowmap.jpg (grille A–K × 1–8).
/// Non utilisé à l'exécution — aide manuelle au réglage des positions dans MapOverlayData.
/// </summary>
public static class WowMapGrid
{
    private const double Left = 0.11;
    private const double Top = 0.14;
    private const double Width = 0.78;
    private const double Height = 0.72;

    /// <summary>Centre d'une case sur Wowmap.jpg (col 0=A, row 0=1).</summary>
    public static (double X, double Y) ReferenceCell(int col, int row)
    {
        var x = Left + (col + 0.5) * (Width / 11.0);
        var y = Top + (row + 0.5) * (Height / 8.0);
        return (x, y);
    }
}
