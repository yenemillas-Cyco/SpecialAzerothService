namespace WindowsOrganiserApp.Services;

/// <summary>Positions normalisées (0–1) pour la pile de persos en haut à gauche de la carte monde.</summary>
public static class CartoMapLayout
{
    private const int StackColumns = 6;
    private const double OriginX = 0.018;
    private const double OriginY = 0.018;
    private const double StepX = 0.022;
    private const double StepY = 0.028;

    public static (double X, double Y) GetStackPosition(int index)
    {
        if (index < 0) index = 0;
        var col = index % StackColumns;
        var row = index / StackColumns;
        return (OriginX + col * StepX, OriginY + row * StepY);
    }

    /// <summary>True si (x,y) correspond à la grille de pile en haut à gauche (pas une position WowSync sauvegardée).</summary>
    public static bool IsStackPosition(double x, double y, double tolerance = 0.004)
    {
        if (x < OriginX - tolerance || y < OriginY - tolerance)
            return false;

        var maxStackX = OriginX + (StackColumns - 1) * StepX;
        if (x > maxStackX + tolerance)
            return false;

        var col = Math.Round((x - OriginX) / StepX);
        var expectedX = OriginX + col * StepX;
        if (Math.Abs(x - expectedX) > tolerance)
            return false;

        var row = Math.Round((y - OriginY) / StepY);
        if (row < 0)
            return false;

        var expectedY = OriginY + row * StepY;
        return Math.Abs(y - expectedY) <= tolerance;
    }
}
