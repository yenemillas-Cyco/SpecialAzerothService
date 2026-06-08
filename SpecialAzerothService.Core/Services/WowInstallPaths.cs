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

    /// <summary>Résultat d'une lecture réelle du dossier WTF (pas seulement Directory.Exists).</summary>
    public readonly record struct WowWtfAccessProbe(
        bool WtfFolderExists,
        bool Readable,
        bool AccessDenied,
        int AccountFolderCount,
        string? ErrorMessage);

    /// <summary>
    /// Vérifie que <c>WTF\Account</c> est lisible (évite le faux positif Program Files :
    /// le dossier existe mais l'énumération est bloquée).
    /// </summary>
    public static WowWtfAccessProbe ProbeWtfAccountAccess(string? wtfAccountPath)
    {
        if (string.IsNullOrWhiteSpace(wtfAccountPath))
            return new WowWtfAccessProbe(false, false, false, 0, null);

        if (!Directory.Exists(wtfAccountPath))
            return new WowWtfAccessProbe(false, false, false, 0, null);

        try
        {
            var count = Directory.GetDirectories(wtfAccountPath).Length;
            return new WowWtfAccessProbe(true, true, false, count, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new WowWtfAccessProbe(true, false, true, 0, wtfAccountPath);
        }
        catch (IOException ex)
        {
            return new WowWtfAccessProbe(true, false, false, 0, ex.Message);
        }
    }

    private static bool HasClassicEraInstall(WowPathResolution resolution)
    {
        if (string.IsNullOrWhiteSpace(resolution.GameRoot)
            || !Directory.Exists(Path.Combine(resolution.GameRoot, ClassicEraFolder)))
            return false;

        var probe = ProbeWtfAccountAccess(resolution.WtfAccountPath);
        return probe.WtfFolderExists && probe.Readable;
    }

    private static string FormatWtfAccessDeniedMessage(string wtfPath, string gameRoot)
    {
        var underProgramFiles = gameRoot.Contains("Program Files", StringComparison.OrdinalIgnoreCase);
        var hint = underProgramFiles
            ? "WoW est sous Program Files : Windows peut bloquer la lecture des comptes.\n"
              + "• Relancez Special Azeroth Service **en administrateur** (clic droit → Exécuter en tant qu'administrateur)\n"
              + "• Ou réinstallez WoW dans un dossier utilisateur (ex. D:\\Jeux\\World of Warcraft)"
            : "Vérifiez les droits sur ce dossier ou relancez l'application en administrateur.";

        return "Accès refusé au dossier des comptes WoW.\n\n"
               + $"Chemin : {wtfPath}\n\n"
               + hint;
    }

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

    /// <summary>Message court pour validation immédiate (dialogue 📁).</summary>
    public static string GetValidationError(string? selectedPath) => GetDetailedSetupError(selectedPath);

    /// <summary>Diagnostic détaillé : chemin WoW, Classic Era, comptes WTF, droits d'accès.</summary>
    public static string GetDetailedSetupError(string? selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return "Aucun dossier WoW enregistré.\n\n"
                   + "⚙ Paramètres → 📁 sélectionnez le dossier « World of Warcraft » "
                   + "(ex. C:\\Program Files (x86)\\World of Warcraft), puis Rescanner.";
        }

        var selected = selectedPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var expected = GetExpectedGameRootHint(selected);

        if (!Directory.Exists(selected))
        {
            return "Le dossier enregistré est introuvable ou inaccessible.\n\n"
                   + $"Chemin : {selected}\n\n"
                   + "Vérifiez que WoW est bien installé sur ce PC, puis choisissez à nouveau le dossier "
                   + "« World of Warcraft » dans ⚙ Paramètres.";
        }

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
                   + $"Sélectionnez le dossier « {GameRootFolderName} » (racine Battle.net sur ce PC).\n\n"
                   + $"Choisi : {selected}\n"
                   + $"Attendu : {expected}";
        }

        var classicEraPath = Path.Combine(selected, ClassicEraFolder);
        var wtfPath = Path.Combine(classicEraPath, "WTF", "Account");
        var retailPath = Path.Combine(selected, "_retail_");

        if (!Directory.Exists(classicEraPath))
        {
            if (Directory.Exists(retailPath))
            {
                return "Installation Retail détectée (_retail_), mais pas WoW Classic Era.\n\n"
                       + $"Dossier : {selected}\n"
                       + $"Manquant : {classicEraPath}\n\n"
                       + "Cette application lit uniquement les personnages **WoW Classic Era**.\n"
                       + "Dans Battle.net : installez « World of Warcraft Classic » (version Era), "
                       + "lancez-le une fois sur ce PC, puis ⚙ Paramètres → Rescanner.";
            }

            return $"Dossier « {GameRootFolderName} » reconnu, mais Classic Era absent.\n\n"
                   + $"Manquant : {classicEraPath}\n\n"
                   + "Installez et lancez **WoW Classic Era** au moins une fois, puis resélectionnez ce dossier.";
        }

        if (!Directory.Exists(wtfPath))
        {
            return "Classic Era trouvé, mais aucun compte WTF pour l'instant.\n\n"
                   + $"Manquant : {wtfPath}\n\n"
                   + "Étapes :\n"
                   + "1. Lancez **WoW Classic Era** et connectez-vous au moins une fois\n"
                   + "2. ⚙ Paramètres → **Rescanner**\n"
                   + "3. **Déployer l'addon** WowSync, /reload en jeu, tapez /wowsync, puis déconnectez-vous";
        }

        var probe = ProbeWtfAccountAccess(wtfPath);
        if (probe.AccessDenied)
            return FormatWtfAccessDeniedMessage(wtfPath, selected);

        if (!probe.Readable)
        {
            return $"Impossible de lire le dossier WTF ({probe.ErrorMessage ?? "erreur inconnue"}).\n\n"
                   + $"Chemin : {wtfPath}";
        }

        if (probe.AccountFolderCount == 0)
        {
            return "Dossier WTF présent, mais aucun compte Battle.net détecté.\n\n"
                   + $"Chemin : {wtfPath}\n\n"
                   + "Connectez-vous une fois en Classic Era (le dossier compte se crée à la première connexion), "
                   + "puis Rescanner.";
        }

        return "";
    }

    /// <summary>Racine « World of Warcraft » avec sous-dossier <c>_classic_era_</c> (WTF optionnel).</summary>
    public static bool TryGetClassicEraGameRoot(string? selectedPath, out string gameRoot)
    {
        gameRoot = "";
        if (string.IsNullOrWhiteSpace(selectedPath))
            return false;

        var selected = selectedPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!IsDirectGameRoot(selected))
            return false;

        gameRoot = selected;
        return Directory.Exists(Path.Combine(gameRoot, ClassicEraFolder));
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
            return GetDetailedSetupError(gameRoot);

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
