using System.IO;
using System.Text.Json;
using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Services;

/// <summary>Repères donjons : %LocalAppData%\SpecialAzerothService\dungeon-markers.json</summary>
public static class DungeonMarkerStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpecialAzerothService",
        "dungeon-markers.json");

    public static List<CartoDungeonMarker> LoadAll()
    {
        if (!File.Exists(FilePath))
            return [];

        try
        {
            var json = File.ReadAllText(FilePath);
            var raw = JsonSerializer.Deserialize<List<DungeonMarkerDto>>(json, JsonOptions);
            if (raw == null)
                return [];

            return raw
                .Where(d => !string.IsNullOrWhiteSpace(d.Key))
                .Select(d =>
                {
                    var key = CartoDungeonMarkerResolver.NormalizeMarkerKey(d.Key!.Trim());
                    CartoDungeonCatalog.TryGet(key, out var entry);
                    return new CartoDungeonMarker
                    {
                        Id = string.IsNullOrWhiteSpace(d.Id) ? Guid.NewGuid().ToString("N") : d.Id!,
                        Key = key,
                        NameFr = string.IsNullOrWhiteSpace(d.NameFr) ? entry.NameFr : d.NameFr!.Trim(),
                        MapX = d.MapX,
                        MapY = d.MapY
                    };
                })
                .Where(m => m.MapX > 0 || m.MapY > 0)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static void SaveAll(IEnumerable<CartoDungeonMarker> markers)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var dto = markers
                .Select(m => new DungeonMarkerDto
                {
                    Id = m.Id,
                    Key = m.Key,
                    NameFr = m.NameFr,
                    MapX = Math.Clamp(m.MapX, 0, 1),
                    MapY = Math.Clamp(m.MapY, 0, 1)
                })
                .ToList();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dto, JsonOptions));
        }
        catch
        {
            // Non bloquant
        }
    }

    private sealed class DungeonMarkerDto
    {
        public string? Id { get; set; }
        public string? Key { get; set; }
        public string? NameFr { get; set; }
        public double MapX { get; set; }
        public double MapY { get; set; }
    }
}
