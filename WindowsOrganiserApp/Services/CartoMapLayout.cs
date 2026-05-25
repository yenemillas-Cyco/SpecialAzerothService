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
}
