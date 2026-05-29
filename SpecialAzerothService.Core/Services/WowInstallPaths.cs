using System.IO;

namespace SpecialAzerothService.Core.Services;

/// <summary>Chemins d'installation WoW Classic Era à partir de la racine du jeu.</summary>
public static class WowInstallPaths
{
    public const string ClassicEraFolder = "_classic_era_";

    /// <summary>Racine du jeu (ex. C:\Jeux\World of Warcraft), sans _classic_era_ ni Interface.</summary>
    public static string NormalizeGameRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        var p = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var suffix in new[]
                 {
                     Path.Combine(ClassicEraFolder, "Interface", "AddOns"),
                     Path.Combine(ClassicEraFolder, "Interface"),
                     ClassicEraFolder
                 })
        {
            if (p.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                p = p[..^suffix.Length].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                break;
            }
        }

        return p;
    }

    public static string GetClassicEraRoot(string gameRoot) =>
        Path.Combine(NormalizeGameRoot(gameRoot), ClassicEraFolder);

    public static string GetAddonsDirectory(string gameRoot) =>
        Path.Combine(GetClassicEraRoot(gameRoot), "Interface", "AddOns", "WowSync");

    public static string GetWtfAccountDirectory(string gameRoot) =>
        Path.Combine(GetClassicEraRoot(gameRoot), "WTF", "Account");
}
