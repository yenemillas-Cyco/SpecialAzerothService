namespace WindowsOrganiserApp;

/// <summary>Options perf / simplicité pour l'onglet Carto.</summary>
public static class CartoRuntimeOptions
{
    /// <summary>Panneau 6 capitales intégré à droite de WowMap.png.</summary>
    public static bool ShowCapitalMaps { get; set; } = true;

    /// <summary>Liste plate XAML (secours). False = expanders (utilisateur → compte → catégorie), pas TreeView.</summary>
    public static bool UseSimpleCharacterList { get; set; } = false;

    /// <summary>Zones et repères lieux-dits dessinés par-dessus WowMap.png (optionnel).</summary>
    public static bool ShowMapOverlays { get; set; } = false;
}
