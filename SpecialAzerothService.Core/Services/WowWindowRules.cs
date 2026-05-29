using System.Diagnostics;
using System.IO;

namespace SpecialAzerothService.Core.Services;

/// <summary>Filtre les fenêtres du client WoW (pas navigateurs / onglets Wowhead, etc.).</summary>
public static class WowWindowRules
{
    private static readonly HashSet<string> WowProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Wow", "WowClassic", "WowT", "WowB"
    };

    private static readonly HashSet<string> BrowserProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "msedge", "chrome", "firefox", "brave", "opera", "vivaldi", "iexplore",
        "msedgewebview2", "chromium", "waterfox", "zen"
    };

    private static readonly string[] WowExecutableFileNames =
    [
        "Wow.exe", "WowClassic.exe", "WowT.exe", "WowB.exe"
    ];

    private static readonly string[] BrowserInstallPathMarkers =
    [
        @"\Google\Chrome\",
        @"\Microsoft\Edge\",
        @"\Mozilla Firefox\",
        @"\BraveSoftware\",
        @"\Opera Software\",
        @"\Vivaldi\",
        @"\Chromium\"
    ];

    public static bool IsWowGameProcess(string processName, uint processId)
    {
        if (BrowserProcessNames.Contains(processName))
            return false;

        if (!WowProcessNames.Contains(processName))
            return false;

        return ValidateExecutablePath(processId);
    }

    private static bool ValidateExecutablePath(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            string? exePath;
            try
            {
                exePath = process.MainModule?.FileName;
            }
            catch
            {
                // Accès refusé (autre session / élévation) : on garde le nom de processus Wow*.
                return true;
            }

            if (string.IsNullOrWhiteSpace(exePath))
                return false;

            var fileName = Path.GetFileName(exePath);
            if (!WowExecutableFileNames.Any(f => fileName.Equals(f, StringComparison.OrdinalIgnoreCase)))
                return false;

            var normalized = exePath.Replace('/', '\\');
            if (BrowserInstallPathMarkers.Any(marker =>
                    normalized.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }
}
