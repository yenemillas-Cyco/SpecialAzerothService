using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

/// <summary>Détecte les faux positifs : marqué placé mais coords addon / projection incohérents.</summary>
public static class CartoPlacementQuality
{
    private const double RectMargin = 0.012;

    public static bool TryGetWarning(
        WowCharacterData sync,
        double mapX,
        double mapY,
        out CartoPlacementWarningKind kind,
        out string detail)
    {
        kind = default;
        detail = "";

        if (sync.X <= 0 && sync.Y <= 0)
            return false;

        if (CartoDungeonMarkerResolver.TryResolve(sync.Zone, sync.SubZone, out _, out _))
            return false;

        var effectiveId = ClassicEraMapProjection.ResolveEffectiveMapId(sync.MapId, sync.Zone, sync.SubZone);

        if (ClassicEraMapProjection.IsContinentMap(sync.MapId)
            && HasSpecificZoneLabel(sync.Zone)
            && !ClassicEraMapProjection.IsContinentMap(effectiveId))
        {
            kind = CartoPlacementWarningKind.ContinentMapWithZoneCoords;
            detail = $"map addon {sync.MapId} (continent) + zone « {sync.Zone} » → coords souvent fausses (mer)";
            return true;
        }

        if (ClassicEraMapProjection.IsContinentMap(effectiveId)
            && ClassicEraMapProjection.TryConvert(sync, out _, out _))
        {
            kind = CartoPlacementWarningKind.ProjectedOnContinent;
            detail = "projeté sur toute la carte continent (Kalimdor / Royaumes de l'Est)";
            return true;
        }

        if (effectiveId > 0
            && !ClassicEraMapProjection.IsContinentMap(effectiveId)
            && ClassicEraMapProjection.TryGetRect(effectiveId, sync.Zone, sync.SubZone, out var rect)
            && mapX > 0
            && mapY > 0
            && !IsInsideRect(mapX, mapY, rect, RectMargin))
        {
            kind = CartoPlacementWarningKind.OutsideZoneRect;
            var label = ClassicEraMapProjection.GetZoneLabel(effectiveId) ?? $"map {effectiveId}";
            detail = $"position hors rectangle {label} ({mapX * 100:F0}%, {mapY * 100:F0}% carte)";
            return true;
        }

        return false;
    }

    public static IReadOnlyList<CartoUnplacedCharacterItem> CollectSuspicious(
        IEnumerable<(string SyncKey, string Name, string AccountName, WowCharacterData Sync)> characters)
    {
        var list = new List<CartoUnplacedCharacterItem>();
        foreach (var (syncKey, name, accountName, sync) in characters)
        {
            if (sync.X <= 0 && sync.Y <= 0)
                continue;

            if (!TryGetProjectedPosition(sync, out var mapX, out var mapY))
                continue;

            if (!TryGetWarning(sync, mapX, mapY, out var warnKind, out var warnDetail))
                continue;

            list.Add(new CartoUnplacedCharacterItem
            {
                SyncKey = syncKey,
                Name = name,
                AccountName = accountName,
                Zone = sync.Zone ?? "",
                SubZone = sync.SubZone ?? "",
                MapId = sync.MapId,
                CoordsDisplay = $"{sync.X * 100:F1}, {sync.Y * 100:F1}",
                Reason = warnKind.ToUnplacedReason(),
                WarningDetail = warnDetail
            });
        }

        return list
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool TryGetProjectedPosition(WowCharacterData sync, out double mapX, out double mapY)
    {
        mapX = 0;
        mapY = 0;
        if (CartoDungeonMarkerResolver.TryResolve(sync.Zone, sync.SubZone, out mapX, out mapY))
            return true;
        return ClassicEraMapProjection.TryConvert(sync, out mapX, out mapY);
    }

    private static bool HasSpecificZoneLabel(string? zone)
    {
        if (string.IsNullOrWhiteSpace(zone))
            return false;

        var n = zone.Trim().ToLowerInvariant();
        return n is not ("kalimdor" or "royaumes de l'est" or "eastern kingdoms" or "azeroth");
    }

    private static bool IsInsideRect(double x, double y, ClassicEraMapProjection.CartoMapRect rect, double margin)
    {
        var left = rect.Left - margin;
        var top = rect.Top - margin;
        var right = rect.Left + rect.Width + margin;
        var bottom = rect.Top + rect.Height + margin;
        return x >= left && x <= right && y >= top && y <= bottom;
    }
}

public enum CartoPlacementWarningKind
{
    ContinentMapWithZoneCoords,
    ProjectedOnContinent,
    OutsideZoneRect
}

public static class CartoPlacementWarningKindExtensions
{
    public static Models.Carto.CartoUnplacedReason ToUnplacedReason(this CartoPlacementWarningKind kind) =>
        kind switch
        {
            CartoPlacementWarningKind.ContinentMapWithZoneCoords => Models.Carto.CartoUnplacedReason.SuspiciousContinentCoords,
            CartoPlacementWarningKind.ProjectedOnContinent => Models.Carto.CartoUnplacedReason.SuspiciousOceanProjection,
            CartoPlacementWarningKind.OutsideZoneRect => Models.Carto.CartoUnplacedReason.SuspiciousOutsideZone,
            _ => Models.Carto.CartoUnplacedReason.ZoneNotCalibrated
        };
}
