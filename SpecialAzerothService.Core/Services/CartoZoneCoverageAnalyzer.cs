using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

/// <summary>
/// Compare les zones WowSync des personnages aux rectangles de calibration disponibles.
/// </summary>
public static class CartoZoneCoverageAnalyzer
{
    public sealed class Report
    {
        public required IReadOnlyList<CartoMissingZoneItem> MissingZones { get; init; }
        public required IReadOnlyList<CartoCalibratedZoneSummary> CalibratedZones { get; init; }
        public required IReadOnlyList<CartoUnplacedCharacterItem> UnplacedCharacters { get; init; }
    }

    public sealed class CharacterSyncRef
    {
        public required string SyncKey { get; init; }
        public required string Name { get; init; }
        public string AccountName { get; init; } = "";
        public required WowCharacterData Sync { get; init; }
        public bool IsOnStack { get; init; }
    }

    public static Report Analyze(IReadOnlyList<CharacterSyncRef> characters)
    {
        var userMapIds = ZoneMapCalibration.LoadUserOverrides().Keys.ToHashSet();
        var builtInMapIds = ClassicEraMapProjection.GetBuiltInZoneRects().Keys.ToHashSet();
        var embeddedMapIds = GetEmbeddedMapIds();
        var allRects = ZoneMapCalibration.LoadAllRaw();

        var unplaced = new List<CartoUnplacedCharacterItem>();
        var missingByKey = new Dictionary<string, (CartoMissingZoneItem Item, HashSet<string> Names)>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in characters)
        {
            var sync = entry.Sync;
            if (CanPlaceOnWorldMap(sync))
                continue;

            var reason = ClassifyReason(sync);
            var coords = sync.X > 0 || sync.Y > 0
                ? $"{sync.X * 100:F1}, {sync.Y * 100:F1}"
                : "0, 0";

            unplaced.Add(new CartoUnplacedCharacterItem
            {
                SyncKey = entry.SyncKey,
                Name = entry.Name,
                AccountName = entry.AccountName,
                Zone = sync.Zone ?? "",
                SubZone = sync.SubZone ?? "",
                MapId = sync.MapId,
                CoordsDisplay = coords,
                Reason = reason,
                IsOnStack = entry.IsOnStack
            });

            if (reason != CartoUnplacedReason.ZoneNotCalibrated)
                continue;

            var effectiveId = ClassicEraMapProjection.ResolveEffectiveMapId(sync.MapId, sync.Zone, sync.SubZone);
            var zoneKey = BuildZoneKey(effectiveId, sync.Zone, sync.SubZone);
            var hasRect = allRects.ContainsKey(effectiveId)
                          || ClassicEraMapProjection.TryGetRect(sync.MapId, sync.Zone, sync.SubZone, out _);

            if (!missingByKey.TryGetValue(zoneKey, out var group))
            {
                ClassicEraMapProjection.TryGetCatalogEntry(effectiveId, out var catalog);
                group = (
                    new CartoMissingZoneItem
                    {
                        ZoneKey = zoneKey,
                        EffectiveMapId = effectiveId,
                        RawMapId = sync.MapId,
                        Zone = sync.Zone ?? "",
                        SubZone = sync.SubZone ?? "",
                        CatalogDisplayName = catalog.MapId == effectiveId ? catalog.DisplayName : null,
                        HasMapRectangle = hasRect,
                        IsUserCalibrated = userMapIds.Contains(effectiveId),
                        Reason = reason,
                        CharacterCount = 0,
                        CharacterNames = ""
                    },
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                missingByKey[zoneKey] = group;
            }

            group.Names.Add(entry.Name);
        }

        var missingZones = missingByKey.Values
            .Select(g => new CartoMissingZoneItem
            {
                ZoneKey = g.Item.ZoneKey,
                EffectiveMapId = g.Item.EffectiveMapId,
                RawMapId = g.Item.RawMapId,
                Zone = g.Item.Zone,
                SubZone = g.Item.SubZone,
                CatalogDisplayName = g.Item.CatalogDisplayName,
                HasMapRectangle = g.Item.HasMapRectangle,
                IsUserCalibrated = g.Item.IsUserCalibrated,
                Reason = g.Item.Reason,
                CharacterCount = g.Names.Count,
                CharacterNames = string.Join(", ", g.Names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            })
            .OrderBy(z => z.ZoneDisplay, StringComparer.OrdinalIgnoreCase)
            .ThenBy(z => z.EffectiveMapId)
            .ToList();

        var calibrated = BuildCalibratedSummaries(allRects, userMapIds, builtInMapIds, embeddedMapIds);

        var orderedUnplaced = unplaced
            .OrderBy(i => i.AccountName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new Report
        {
            MissingZones = missingZones,
            CalibratedZones = calibrated,
            UnplacedCharacters = orderedUnplaced
        };
    }

    private static List<CartoCalibratedZoneSummary> BuildCalibratedSummaries(
        Dictionary<int, ClassicEraMapProjection.CartoMapRect> allRects,
        HashSet<int> userMapIds,
        HashSet<int> builtInMapIds,
        HashSet<int> embeddedMapIds)
    {
        var list = new List<CartoCalibratedZoneSummary>();
        foreach (var mapId in allRects.Keys.OrderBy(id => id))
        {
            if (ClassicEraMapProjection.IsContinentMap(mapId))
                continue;

            string display;
            if (ClassicEraMapProjection.TryGetCatalogEntry(mapId, out var catalog))
                display = catalog.DisplayName;
            else
                display = ClassicEraMapProjection.GetZoneLabel(mapId) ?? $"Map {mapId}";

            list.Add(new CartoCalibratedZoneSummary
            {
                MapId = mapId,
                DisplayName = display,
                IsUserPlaced = userMapIds.Contains(mapId),
                IsBuiltIn = builtInMapIds.Contains(mapId),
                IsEmbedded = embeddedMapIds.Contains(mapId) && !builtInMapIds.Contains(mapId) && !userMapIds.Contains(mapId)
            });
        }

        return list
            .OrderBy(z => z.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<int> GetEmbeddedMapIds()
    {
        var all = ZoneMapCalibration.LoadAllRaw();
        var builtIn = ClassicEraMapProjection.GetBuiltInZoneRects();
        var user = ZoneMapCalibration.LoadUserOverrides();
        var embedded = new HashSet<int>();
        foreach (var id in all.Keys)
        {
            if (!builtIn.ContainsKey(id) && !user.ContainsKey(id))
                embedded.Add(id);
        }

        return embedded;
    }

    private static bool CanPlaceOnWorldMap(WowCharacterData sync)
    {
        if (sync.X <= 0 && sync.Y <= 0)
            return false;

        if (ClassicEraMapProjection.TryConvert(sync, out _, out _))
            return true;

        return CartoDungeonMarkerResolver.TryResolve(sync.Zone, sync.SubZone, out _, out _);
    }

    private static CartoUnplacedReason ClassifyReason(WowCharacterData sync)
    {
        if (sync.X <= 0 && sync.Y <= 0)
            return CartoUnplacedReason.CoordsZero;
        if (LooksLikeInstance(sync))
            return CartoUnplacedReason.InInstance;
        return CartoUnplacedReason.ZoneNotCalibrated;
    }

    private static bool LooksLikeInstance(WowCharacterData sync)
    {
        if (CartoDungeonMarkerResolver.TryResolve(sync.Zone, sync.SubZone, out _, out _))
            return false;

        var blob = $"{sync.Zone} {sync.SubZone}".ToLowerInvariant();
        foreach (var entry in CartoDungeonCatalog.All)
        {
            if (blob.Contains(entry.NameFr.ToLowerInvariant(), StringComparison.Ordinal))
                return true;
        }

        return sync.MapId > 0
               && !ClassicEraMapProjection.IsCapitalMap(sync.MapId)
               && !ClassicEraMapProjection.TryGetCatalogEntry(sync.MapId, out _);
    }

    private static string BuildZoneKey(int effectiveMapId, string? zone, string? subZone) =>
        $"{effectiveMapId}|{Normalize(zone)}|{Normalize(subZone)}";

    private static string Normalize(string? text) =>
        string.IsNullOrWhiteSpace(text) ? "" : text.Trim().ToLowerInvariant();
}
