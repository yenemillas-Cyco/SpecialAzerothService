namespace SpecialAzerothService.Core.Models.Carto;public enum CapitalFaction
{
    Horde,
    Alliance
}

/// <summary>Mini-cartes de capitales (Classic Era) affichées à droite de la carte monde.</summary>
public sealed record CapitalMapDefinition(
    int MapId,
    string Title,
    CapitalFaction Faction,
    string AssetFileName,
    int GridRow,
    int GridColumn);

public static class CapitalMapDefinitions
{
    /// <summary>Espace entre la carte monde et le bloc capitales.</summary>
    public const double PanelMarginLeft = 8;
    /// <summary>Marge gauche/droite dans le viewport (évite que les cartes touchent les bords).</summary>
    public const double ViewportPaddingHorizontal = 20;
    /// <summary>Largeur bloc capitales = carte monde × ce facteur.</summary>
    public const double PanelWidthScale = 2.0;
    /// <summary>Hauteur bloc capitales = carte monde (2 lignes, alignées en haut avec la carte).</summary>
    public const double PanelHeightScale = 1.0;

    /// <summary>Grille 3×2 — ligne 0 Horde, ligne 1 Alliance.</summary>
    public static IReadOnlyList<CapitalMapDefinition> All { get; } =
    [
        new(1454, "Orgrimmar", CapitalFaction.Horde, "orgrimmar.jpg", 0, 0),
        new(1456, "Thunder Bluff", CapitalFaction.Horde, "thunder-bluff.jpg", 0, 1),
        new(1458, "Undercity", CapitalFaction.Horde, "undercity.jpg", 0, 2),
        new(1453, "Stormwind", CapitalFaction.Alliance, "stormwind-city.jpg", 1, 0),
        new(1455, "Ironforge", CapitalFaction.Alliance, "ironforge.jpg", 1, 1),
        new(1457, "Darnassus", CapitalFaction.Alliance, "darnassus.jpg", 1, 2),
    ];
}
