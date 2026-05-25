using System.IO;
using System.Reflection;
using System.Text.Json;
using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Services;

/// <summary>
/// Rectangles zone : ressource embarquée + surcharge utilisateur (%LocalAppData%\SpecialAzerothService\zone-calibration.json).
/// Format : { "1443": { "left": 0.023, "top": 0.355, "width": 0.052, "height": 0.085 } }
/// </summary>
public static class ZoneMapCalibration
{
    private const string EmbeddedResourceName = "WindowsOrganiserApp.Assets.zone-rects-classic-era.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpecialAzerothService",
        "zone-calibration.json");

    public static Dictionary<int, ClassicEraMapProjection.CartoMapRect> LoadAll(bool sanitize = true)
    {
        var raw = LoadAllRaw();
        if (!sanitize) return raw;

        return ClassicEraMapProjection.SanitizeZoneRects(raw);
    }

    /// <summary>Built-in + JSON embarqué + fichier utilisateur (priorité croissante).</summary>
    public static Dictionary<int, ClassicEraMapProjection.CartoMapRect> LoadAllRaw()
    {
        var result = new Dictionary<int, ClassicEraMapProjection.CartoMapRect>(
            ClassicEraMapProjection.GetBuiltInZoneRects());

        foreach (var (mapId, rect) in LoadEmbeddedResource())
            result[mapId] = rect;

        foreach (var (mapId, rect) in LoadUserFile())
            result[mapId] = rect;

        return result;
    }

    public static void SaveAll(IReadOnlyDictionary<int, ClassicEraMapProjection.CartoMapRect> rects)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var dto = rects
                .OrderBy(kv => kv.Key)
                .ToDictionary(
                    kv => kv.Key.ToString(),
                    kv =>
                    {
                        var rect = ClassicEraMapProjection.SanitizeZoneRect(kv.Value);
                        return new ZoneRectDto
                        {
                            Left = rect.Left,
                            Top = rect.Top,
                            Width = rect.Width,
                            Height = rect.Height
                        };
                    });
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dto, JsonOptions));
        }
        catch
        {
            // Non bloquant
        }
    }

    public static void ApplyOverrides(IDictionary<int, ClassicEraMapProjection.CartoMapRect> byMapId)
    {
        foreach (var (mapId, rect) in LoadAllRaw())
        {
            if (ClassicEraMapProjection.IsContinentMap(mapId))
                continue;
            byMapId[mapId] = ClassicEraMapProjection.SanitizeZoneRect(rect);
        }
    }

    private static Dictionary<int, ClassicEraMapProjection.CartoMapRect> LoadUserFile()
    {
        var result = new Dictionary<int, ClassicEraMapProjection.CartoMapRect>();
        if (!File.Exists(FilePath))
            return result;

        try
        {
            var json = File.ReadAllText(FilePath);
            ParseInto(result, json);
        }
        catch
        {
            // Fichier utilisateur invalide
        }

        return result;
    }

    private static Dictionary<int, ClassicEraMapProjection.CartoMapRect> LoadEmbeddedResource()
    {
        var result = new Dictionary<int, ClassicEraMapProjection.CartoMapRect>();
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(EmbeddedResourceName);
            if (stream == null)
                return result;

            using var reader = new StreamReader(stream);
            ParseInto(result, reader.ReadToEnd());
        }
        catch
        {
            // Ressource absente ou JSON invalide
        }

        return result;
    }

    private static void ParseInto(Dictionary<int, ClassicEraMapProjection.CartoMapRect> result, string json)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, ZoneRectDto>>(json, JsonOptions);
        if (raw == null) return;

        foreach (var (key, dto) in raw)
        {
            if (!int.TryParse(key, out var mapId)) continue;
            if (dto.Width <= 0 || dto.Height <= 0) continue;
            result[mapId] = new ClassicEraMapProjection.CartoMapRect(
                dto.Left, dto.Top, dto.Width, dto.Height);
        }
    }

    private sealed class ZoneRectDto
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
