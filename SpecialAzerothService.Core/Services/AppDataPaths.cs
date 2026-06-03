namespace SpecialAzerothService.Core.Services;

/// <summary>Chemin du dossier de données utilisateur (%LocalAppData%\SpecialAzerothService).</summary>
public static class AppDataPaths
{
    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpecialAzerothService");

    public static string SettingsFile => Path.Combine(Directory, "settings.json");
    public static string CartoFile => Path.Combine(Directory, "carto.json");
    public static string BountiesFile => Path.Combine(Directory, "bounties.json");
    public static string CraftListsFile => Path.Combine(Directory, "craft-lists.json");
    public static string PresetsDirectory => Path.Combine(Directory, "presets");
}
