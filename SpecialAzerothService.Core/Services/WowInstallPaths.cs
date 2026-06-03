using System.IO;

namespace SpecialAzerothService.Core.Services;

/// <summary>
/// WoW Classic Era uniquement. Le joueur indique la racine « World of Warcraft » ;
/// l'app dérive <c>_classic_era_\WTF\Account</c> et <c>_classic_era_\Interface\AddOns\WowSync</c>.
/// </summary>
public static class WowInstallPaths
{
    public const string ClassicEraFolder = "_classic_era_";
    public const string GameRootFolderName = "World of Warcraft";
    public const string AddonFolderName = "WowSync";

    public readonly record struct WowPathResolution(
        string GameRoot,
        string InstallFolder,
        string WtfAccountPath,
        string AddonsDirectory)
    {
        public bool IsValid =>
            Directory.Exists(InstallFolder) && Directory.Exists(WtfAccountPath);
    }

    /// <summary>Racine WoW : dossier qui contient directement <c>_classic_era_</c>.</summary>
    public static string NormalizeGameRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        var p = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (Directory.Exists(Path.Combine(p, ClassicEraFolder)))
            return p;

        foreach (var suffix in new[]
                 {
                     Path.Combine(ClassicEraFolder, "Interface", "AddOns", AddonFolderName),
                     Path.Combine(ClassicEraFolder, "Interface", "AddOns"),
                     Path.Combine(ClassicEraFolder, "Interface"),
                     Path.Combine(ClassicEraFolder, "WTF", "Account"),
                     Path.Combine(ClassicEraFolder, "WTF"),
                     ClassicEraFolder
                 })
        {
            if (!p.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            p = p[..^suffix.Length].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Directory.Exists(Path.Combine(p, ClassicEraFolder)))
                return p;
            break;
        }

        if (Path.GetFileName(p).Equals(ClassicEraFolder, StringComparison.OrdinalIgnoreCase))
        {
            var parent = Directory.GetParent(p)?.FullName ?? "";
            if (Directory.Exists(Path.Combine(parent, ClassicEraFolder)))
                return parent;
        }

        return p;
    }

    public static WowPathResolution Resolve(string? userSelectedPath)
    {
        var gameRoot = NormalizeGameRoot(userSelectedPath);
        if (string.IsNullOrWhiteSpace(gameRoot))
            return default;

        var install = Path.Combine(gameRoot, ClassicEraFolder);
        var wtf = Path.Combine(install, "WTF", "Account");
        var addons = Path.Combine(install, "Interface", "AddOns", AddonFolderName);

        return new WowPathResolution(gameRoot, install, wtf, addons);
    }

    public static bool TryResolve(string? userSelectedPath, out WowPathResolution resolution)
    {
        resolution = Resolve(userSelectedPath);
        return resolution.IsValid;
    }

    /// <summary>
    /// Dossier choisi par le joueur : exactement « World of Warcraft » avec <c>_classic_era_</c> dedans
    /// (pas <c>_classic_era_</c>, pas WTF, pas un parent type <c>D:\Jeux</c>).
    /// </summary>
    public static bool IsDirectGameRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var p = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Path.GetFileName(p).Equals(GameRootFolderName, StringComparison.OrdinalIgnoreCase))
            return false;

        return Directory.Exists(Path.Combine(p, ClassicEraFolder));
    }

    public static bool IsInstallSubfolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var p = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Path.GetFileName(p).Equals(ClassicEraFolder, StringComparison.OrdinalIgnoreCase))
            return true;

        var marker = ClassicEraFolder + Path.DirectorySeparatorChar;
        var markerAlt = ClassicEraFolder + Path.AltDirectorySeparatorChar;
        return p.Contains(marker, StringComparison.OrdinalIgnoreCase)
               || p.Contains(markerAlt, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Complète le chemin à partir du dossier choisi (sans chercher ailleurs sur le PC).
    /// Accepte « World of Warcraft » ou un parent/enfant contenant <c>_classic_era_</c>.
    /// </summary>
    public static bool TryCompleteUserFolder(string? userSelectedPath, out WowPathResolution resolution)
    {
        resolution = default;
        if (string.IsNullOrWhiteSpace(userSelectedPath))
            return false;

        var selected = userSelectedPath.Trim().TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (IsDirectGameRoot(selected))
        {
            resolution = Resolve(selected);
            return HasClassicEraInstall(resolution);
        }

        var completed = FindWorldOfWarcraftInstall(selected);
        if (string.IsNullOrEmpty(completed))
            return false;

        resolution = Resolve(completed);
        return HasClassicEraInstall(resolution);
    }

    /// <summary>Dossier addon WowSync dérivé du chemin WoW (création autorisée si absent).</summary>
    public static bool TryGetAddonDeployDirectory(string? gameRoot, out string addonsDirectory)
    {
        addonsDirectory = "";
        var resolution = Resolve(gameRoot);
        if (string.IsNullOrWhiteSpace(resolution.GameRoot)
            || string.IsNullOrWhiteSpace(resolution.AddonsDirectory))
            return false;

        var install = Path.Combine(resolution.GameRoot, ClassicEraFolder);
        if (!Directory.Exists(install))
            return false;

        addonsDirectory = resolution.AddonsDirectory;
        return true;
    }

    private static bool HasClassicEraInstall(WowPathResolution resolution) =>
        !string.IsNullOrWhiteSpace(resolution.GameRoot)
        && Directory.Exists(Path.Combine(resolution.GameRoot, ClassicEraFolder))
        && Directory.Exists(resolution.WtfAccountPath);

    /// <summary>Chemin du dossier « World of Warcraft » à proposer dans les messages d'erreur.</summary>
    public static string GetExpectedGameRootHint(string? selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
            return GameRootFolderName;

        var selected = selectedPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (Path.GetFileName(selected).Equals(GameRootFolderName, StringComparison.OrdinalIgnoreCase))
            return selected;

        var fromWalk = FindWorldOfWarcraftInstall(selected);
        if (!string.IsNullOrEmpty(fromWalk))
            return fromWalk;

        if (Path.GetFileName(selected).Equals(ClassicEraFolder, StringComparison.OrdinalIgnoreCase))
        {
            var parent = Directory.GetParent(selected)?.FullName;
            if (!string.IsNullOrEmpty(parent))
                return Path.Combine(parent, GameRootFolderName);
        }

        return Path.Combine(selected, GameRootFolderName);
    }

    private static string? FindWorldOfWarcraftInstall(string fromPath)
    {
        try
        {
            var dir = new DirectoryInfo(fromPath);
            while (dir != null)
            {
                if (dir.Name.Equals(GameRootFolderName, StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(Path.Combine(dir.FullName, ClassicEraFolder)))
                    return dir.FullName;

                var sibling = Path.Combine(dir.FullName, GameRootFolderName);
                if (Directory.Exists(Path.Combine(sibling, ClassicEraFolder)))
                    return sibling;

                dir = dir.Parent;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    /// <summary>Message d'erreur si le dossier choisi n'est pas « World of Warcraft ».</summary>
    public static string GetValidationError(string? selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
            return "Aucun dossier sélectionné.";

        var selected = selectedPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var expected = GetExpectedGameRootHint(selected);

        if (!IsDirectGameRoot(selected))
        {
            if (IsInstallSubfolder(selected))
            {
                return "Dossier incorrect.\n\n"
                       + $"Ne sélectionnez pas « {ClassicEraFolder} » ni un sous-dossier du jeu.\n\n"
                       + $"Choisi : {selected}\n"
                       + $"Sélectionnez : {expected}";
            }

            return "Dossier incorrect.\n\n"
                   + $"Sélectionnez le dossier « {GameRootFolderName} » (installation du jeu sur ce PC).\n\n"
                   + $"Choisi : {selected}\n"
                   + $"Sélectionnez : {expected}";
        }

        var wtfPath = Path.Combine(selected, ClassicEraFolder, "WTF", "Account");
        if (!Directory.Exists(wtfPath))
        {
            return $"{GameRootFolderName} trouvé, mais pas le dossier des comptes.\n\n"
                   + $"Manquant : {wtfPath}\n\n"
                   + "Lancez le jeu Classic Era au moins une fois sur ce PC.";
        }

        return "";
    }

    public static string GetWtfAccountDirectory(string gameRoot) =>
        Resolve(gameRoot).WtfAccountPath;

    public static string GetAddonsDirectory(string gameRoot) =>
        Resolve(gameRoot).AddonsDirectory;

    public static bool TryGetWtfAccountDirectory(string? gameRoot, out string wtfPath)
    {
        wtfPath = "";
        if (!TryCompleteUserFolder(gameRoot, out var resolution))
            return false;

        wtfPath = resolution.WtfAccountPath;
        return Directory.Exists(wtfPath);
    }

    public static IReadOnlyList<string> ListWtfAccountFolderNames(string? gameRoot)
    {
        if (!TryGetWtfAccountDirectory(gameRoot, out var wtfPath))
            return [];

        return WowWtfAccountScanner.ListAccountFolderNames(wtfPath);
    }

    public static string DescribeResolution(string? gameRoot, string? expectedAddonVersion = null)
    {
        if (!TryCompleteUserFolder(gameRoot, out var r))
            return GetValidationError(gameRoot);

        var addonLine = string.IsNullOrWhiteSpace(expectedAddonVersion)
            ? $"Addon : {r.AddonsDirectory}"
            : WowAddonVersionReader.FormatAddonStatus(
                expectedAddonVersion,
                WowAddonVersionReader.ReadInstalledVersion(r.AddonsDirectory));

        return $"WoW : {r.GameRoot}\n"
               + $"Comptes : {r.WtfAccountPath}\n"
               + addonLine;
    }

    public static string NormalizeStoredPath(string? path) => NormalizeGameRoot(path);
}
