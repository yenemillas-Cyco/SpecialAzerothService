namespace WindowsOrganiserApp;

/// <summary>Options perf / simplicité pour l'onglet Carto.</summary>
public static class CartoRuntimeOptions
{
    /// <summary>Panneau 6 capitales (désactivé par défaut — réactiver quand la carte monde est stable).</summary>
    public static bool ShowCapitalMaps { get; set; } = false;

    /// <summary>Liste plate XAML (secours perf). False = roster groupé utilisateur / catégorie.</summary>
    public static bool UseSimpleCharacterList { get; set; } = false;
}
