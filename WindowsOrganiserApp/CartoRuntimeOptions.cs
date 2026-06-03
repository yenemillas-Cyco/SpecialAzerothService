namespace WindowsOrganiserApp;

/// <summary>Options perf / simplicité pour l'onglet Carto.</summary>
public static class CartoRuntimeOptions
{
    /// <summary>Panneau 6 capitales à droite de la carte monde.</summary>
    public static bool ShowCapitalMaps { get; set; } = true;

    /// <summary>Liste plate XAML (secours). False = expanders (utilisateur → compte → catégorie), pas TreeView.</summary>
    public static bool UseSimpleCharacterList { get; set; } = false;

    /// <summary>Rectangles zones open world sur WowMap (désactivé le temps du calibrage donjons).</summary>
    public static bool ShowWorldZoneRectOverlays { get; set; } = false;
}
