using System.IO;

namespace SpecialAzerothService.Core.Services;

/// <summary>
/// Cherche WowSync.lua sous <c>WTF\Account</c> (racine compte ou sous-dossiers royaume / perso).</summary>
public static class WowWtfAccountScanner
{
    public readonly record struct WowSyncLuaFile(string AccountFolder, string FilePath);

    /// <summary>Tous les <c>WowSync.lua</c> sous le dossier WTF Account (récursif).</summary>
    public static IReadOnlyList<WowSyncLuaFile> FindWowSyncLuaFiles(string wtfAccountPath)
    {
        var results = new List<WowSyncLuaFile>();
        if (string.IsNullOrWhiteSpace(wtfAccountPath) || !Directory.Exists(wtfAccountPath))
            return results;

        try
        {
            foreach (var file in Directory.EnumerateFiles(
                         wtfAccountPath, "WowSync.lua", SearchOption.AllDirectories))
            {
                if (!file.Contains($"{Path.DirectorySeparatorChar}SavedVariables{Path.DirectorySeparatorChar}",
                        StringComparison.OrdinalIgnoreCase)
                    && !file.Contains($"{Path.AltDirectorySeparatorChar}SavedVariables{Path.AltDirectorySeparatorChar}",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                var rel = Path.GetRelativePath(wtfAccountPath, file);
                var accountFolder = rel.Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries)[0];

                if (string.IsNullOrWhiteSpace(accountFolder))
                    continue;

                results.Add(new WowSyncLuaFile(accountFolder, file));
            }
        }
        catch
        {
            // ignore
        }

        return results;
    }

    public static IReadOnlyList<string> ListAccountFolderNames(string wtfAccountPath)
    {
        if (string.IsNullOrWhiteSpace(wtfAccountPath) || !Directory.Exists(wtfAccountPath))
            return [];

        var fromLua = FindWowSyncLuaFiles(wtfAccountPath)
            .Select(f => f.AccountFolder)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var dir in Directory.GetDirectories(wtfAccountPath))
            {
                var name = Path.GetFileName(dir);
                if (!string.IsNullOrWhiteSpace(name))
                    fromLua.Add(name);
            }
        }
        catch
        {
            // ignore
        }

        return fromLua.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
