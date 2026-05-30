using SpecialAzerothService.Core.Models.Carto;

namespace SpecialAzerothService.Core.Services;

/// <summary>Positionne un perso sur un repère lieu-dit / instance si la zone in-game correspond.</summary>
public static class CartoDungeonMarkerResolver
{
    private static readonly (string[] Needles, string Key)[] ZoneHints =
    [
        (["taverne des pendus"], "stratholme"),
        (["scholomance"], "scholomance"),
        (["mont blackrock", "blackrock", "viaduc du magma", "profondeurs de rochenoire", "blackrock depths", "brd",
            "pic de rochenoire inferieur", "pic de rochenoire superieur", "lower blackrock", "upper blackrock"], "mont_blackrock"),
        (["coeur du magma", "molten core"], "molten_core"),
        (["portes d'ahn", "gates of ahn"], "gates_aq"),
        (["ruines d'ahn"], "ruins_aq"),
        (["temple d'ahn"], "temple_aq"),
        (["mortemines", "deadmines"], "deadmines"),
        (["ombrecroc", "shadowfang"], "shadowfang_keep"),
        (["lamentations", "wailing caverns"], "wailing_caverns"),
        (["ragefeu", "ragefire"], "ragefire"),
        (["maraudon"], "maraudon"),
        (["uldaman"], "uldaman"),
        (["zul'farrak", "zul farrak"], "zul_farrak"),
        (["hache-tripes", "haches-tripes", "hache tripes", "haches tripes", "dire maul", "communs en ruine"], "dire_maul"),
        (["tranchebauge", "razorfen downs"], "razorfen_downs"),
        (["kraal de tranchebauge", "razorfen kraul"], "razorfen_kraul"),
        (["brassenoire", "blackfathom"], "blackfathom_deeps"),
        (["prison", "stockades"], "stockades"),
        (["monastere ecarlate", "scarlet monastery"], "scarlet_monastery"),
        (["gnomeregan"], "gnomeregan"),
        (["onyxia", "repaire d'onyxia"], "onyxia"),
        (["aile noire", "blackwing"], "blackwing_lair"),
        (["naxxramas", "naxx"], "naxxramas"),
        (["zul'gurub", "zul gurub"], "zul_gurub"),
        (["atal'hakkar", "sunken temple"], "sunken_temple"),
        (["chapelle de l espoir", "espoir de lumiere", "light's hope chapel", "lights hope chapel"], "lights_hope_chapel"),
    ];

    /// <summary>Même point carte monde (ex. Portes AQ dehors ≈ repère Ruines).</summary>
    private static readonly Dictionary<string, string[]> MarkerFallbackKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gates_aq"] = ["ruins_aq"],
            ["mont_blackrock"] =
            [
                "blackrock_depths", "blackrock_spire_lower", "blackrock_spire_upper", "blackrock_spire"
            ],
        };

    public static bool TryResolve(string? zone, string? subZone, out double mapX, out double mapY)
    {
        mapX = 0;
        mapY = 0;
        var blob = Normalize($"{zone} {subZone}");
        if (string.IsNullOrWhiteSpace(blob))
            return false;

        string? key = null;
        foreach (var (needles, dungeonKey) in ZoneHints)
        {
            if (needles.Any(n => blob.Contains(Normalize(n), StringComparison.Ordinal)))
            {
                key = dungeonKey;
                break;
            }
        }

        if (key == null)
            return false;

        key = NormalizeMarkerKey(key);
        return TryResolveMarkerKey(key, out mapX, out mapY);
    }

    private static bool TryResolveMarkerKey(string key, out double mapX, out double mapY)
    {
        if (TryLoadPlacedMarker(key, out mapX, out mapY))
            return true;

        if (MarkerFallbackKeys.TryGetValue(key, out var fallbacks))
        {
            foreach (var alt in fallbacks)
            {
                if (TryLoadPlacedMarker(alt, out mapX, out mapY))
                    return true;
            }
        }

        mapX = 0;
        mapY = 0;
        return false;
    }

    private static bool TryLoadPlacedMarker(string key, out double mapX, out double mapY)
    {
        mapX = 0;
        mapY = 0;
        key = NormalizeMarkerKey(key);

        foreach (var marker in DungeonMarkerStore.LoadAll())
        {
            if (!NormalizeMarkerKey(marker.Key).Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;
            if (marker.MapX <= 0 && marker.MapY <= 0)
                continue;

            mapX = marker.MapX;
            mapY = marker.MapY;
            return true;
        }

        return false;
    }

    public static string NormalizeMarkerKey(string key) =>
        key switch
        {
            "shadowfang" => "shadowfang_keep",
            "blackfathom" => "blackfathom_deeps",
            "razorfen" => "razorfen_kraul",
            "blackrock_depths" => "mont_blackrock",
            "blackrock_spire" => "mont_blackrock",
            "blackrock_spire_lower" => "mont_blackrock",
            "blackrock_spire_upper" => "mont_blackrock",
            _ => key.Trim().ToLowerInvariant()
        };

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return text.Trim()
            .Replace("'", "'", StringComparison.Ordinal)
            .Replace("'", "'", StringComparison.Ordinal)
            .Replace("'", " ", StringComparison.Ordinal)
            .Replace("  ", " ", StringComparison.Ordinal)
            .Replace("é", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("è", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("ê", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("à", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("ô", "o", StringComparison.OrdinalIgnoreCase)
            .Replace("û", "u", StringComparison.OrdinalIgnoreCase)
            .Replace("î", "i", StringComparison.OrdinalIgnoreCase)
            .Replace("ç", "c", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
    }
}
