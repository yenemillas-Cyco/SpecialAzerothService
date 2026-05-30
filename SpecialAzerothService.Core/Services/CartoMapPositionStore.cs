using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

/// <summary>
/// Positions absolues carte monde (0–1), dérivées de WowSync + zone-calibration.json.
/// Fichier séparé de carto.json — recalcul si zones ou coords addon changent.
/// </summary>
public static class CartoMapPositionStore
{
    /// <summary>Incrémenter quand la logique de projection zone change (invalide le cache positions).</summary>
    public const string ProjectionLogicVersion = "12";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpecialAzerothService",
        "map-positions-cache.json");

    public static string BuildSourceStamp(WowCharacterData sync) =>
        $"{sync.MapId}|{sync.X:F4}|{sync.Y:F4}|{sync.Zone}|{sync.SubZone}";

    public static string GetZoneCalibrationStamp()
    {
        try
        {
            var zoneHash = File.Exists(ZoneMapCalibration.FilePath)
                ? HashFilePrefix(ZoneMapCalibration.FilePath)
                : "nozone";
            var dungeonHash = File.Exists(DungeonMarkerStore.FilePath)
                ? HashFilePrefix(DungeonMarkerStore.FilePath)
                : "nodungeon";
            return $"{ProjectionLogicVersion}-{zoneHash}-{dungeonHash}";
        }
        catch
        {
            return $"{ProjectionLogicVersion}-builtin";
        }
    }

    private static string HashFilePrefix(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }

    public static CacheFile? TryLoad(string zoneStamp)
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            var json = File.ReadAllText(FilePath);
            var file = JsonSerializer.Deserialize<CacheFile>(json, JsonOptions);
            if (file == null || !zoneStamp.Equals(file.ZoneCalibrationStamp, StringComparison.Ordinal))
                return null;

            return file;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string zoneStamp, IReadOnlyDictionary<string, CacheEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var file = new CacheFile
            {
                ZoneCalibrationStamp = zoneStamp,
                Entries = entries.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value,
                    StringComparer.OrdinalIgnoreCase)
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(file, JsonOptions));
        }
        catch
        {
            // Non bloquant
        }
    }

    public sealed class CacheFile
    {
        public string ZoneCalibrationStamp { get; set; } = "";
        public Dictionary<string, CacheEntry> Entries { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class CacheEntry
    {
        public double MapX { get; set; }
        public double MapY { get; set; }
        public string SourceStamp { get; set; } = "";
        public bool Placed { get; set; }
    }
}
