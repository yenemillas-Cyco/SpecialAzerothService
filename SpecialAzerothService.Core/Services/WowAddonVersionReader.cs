using System.IO;
using System.Text.RegularExpressions;

namespace SpecialAzerothService.Core.Services;

public static partial class WowAddonVersionReader
{
    public static string? ReadInstalledVersion(string? addonsDirectory)
    {
        if (string.IsNullOrWhiteSpace(addonsDirectory))
            return null;

        var tocPath = Path.Combine(addonsDirectory, "WowSync.toc");
        if (!File.Exists(tocPath))
            return null;

        try
        {
            foreach (var line in File.ReadLines(tocPath))
            {
                var m = VersionLine().Match(line);
                if (m.Success)
                    return m.Groups[1].Value.Trim();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    public static string FormatAddonStatus(string expectedVersion, string? installedVersion)
    {
        if (string.IsNullOrWhiteSpace(installedVersion))
            return $"Addon : absent (attendu v{expectedVersion})";

        return string.Equals(installedVersion, expectedVersion, StringComparison.OrdinalIgnoreCase)
            ? $"Addon : v{installedVersion} (à jour)"
            : $"Addon : v{installedVersion} (mise à jour → v{expectedVersion})";
    }

    [GeneratedRegex(@"^##\s*Version:\s*(.+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionLine();
}
