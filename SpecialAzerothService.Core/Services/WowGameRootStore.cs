using System.IO;

namespace SpecialAzerothService.Core.Services;

/// <summary>
/// Chemin WoW persistant (fichier dédié, indépendant de settings.json).
/// </summary>
public static class WowGameRootStore
{
    public static string FilePath => Path.Combine(AppDataPaths.Directory, "wow-game-root.txt");

    public static string? Read()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            var text = File.ReadAllText(FilePath).Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string? gameRoot)
    {
        try
        {
            Directory.CreateDirectory(AppDataPaths.Directory);
            var normalized = WowInstallPaths.NormalizeStoredPath(gameRoot);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
                return;
            }

            File.WriteAllText(FilePath, normalized);
        }
        catch
        {
            // ignore
        }
    }

    public static void TryDelete()
    {
        try
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
        catch
        {
            // ignore
        }
    }
}
