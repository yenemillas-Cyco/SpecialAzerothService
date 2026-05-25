using WindowsOrganiserApp.Models.WowSync;

namespace WindowsOrganiserApp.Services;

/// <summary>Calcule MapX/MapY monde depuis zones (JSON) + coords addon — hors thread UI au warmup.</summary>
public static class CartoMapPositionPrecompute
{
    public sealed record CharacterPlacement(
        string SyncKey,
        double MapX,
        double MapY,
        bool Placed);

    public static IReadOnlyList<CharacterPlacement> ComputeForAccounts(
        IReadOnlyList<WowAccountData> accounts)
    {
        var zoneStamp = CartoMapPositionStore.GetZoneCalibrationStamp();
        var rects = ZoneMapCalibration.LoadAll();
        if (rects.Count > 0)
            ClassicEraMapProjection.ApplyUserRects(rects);

        var cache = CartoMapPositionStore.TryLoad(zoneStamp);
        var cacheOut = new Dictionary<string, CartoMapPositionStore.CacheEntry>(StringComparer.OrdinalIgnoreCase);
        var results = new List<CharacterPlacement>();

        foreach (var account in accounts)
        {
            foreach (var sync in account.Characters)
            {
                if (string.IsNullOrWhiteSpace(sync.Key))
                    continue;

                var sourceStamp = CartoMapPositionStore.BuildSourceStamp(sync);
                if (cache?.Entries.TryGetValue(sync.Key, out var cached) == true
                    && cached.SourceStamp == sourceStamp
                    && cached.Placed)
                {
                    results.Add(new CharacterPlacement(sync.Key, cached.MapX, cached.MapY, true));
                    cacheOut[sync.Key] = cached;
                    continue;
                }

                var placed = false;
                var mapX = 0.0;
                var mapY = 0.0;

                if (sync.X > 0 || sync.Y > 0)
                {
                    placed = ClassicEraMapProjection.TryConvert(sync, out mapX, out mapY)
                             || CartoDungeonMarkerResolver.TryResolve(sync.Zone, sync.SubZone, out mapX, out mapY);
                }

                results.Add(new CharacterPlacement(sync.Key, mapX, mapY, placed));
                cacheOut[sync.Key] = new CartoMapPositionStore.CacheEntry
                {
                    MapX = mapX,
                    MapY = mapY,
                    SourceStamp = sourceStamp,
                    Placed = placed
                };
            }
        }

        CartoMapPositionStore.Save(zoneStamp, cacheOut);
        return results;
    }
}
