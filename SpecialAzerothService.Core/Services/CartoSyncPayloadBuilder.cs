using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

public sealed class CartoSyncBuildInput
{
    public required string OwnerGuid { get; init; }
    public required IReadOnlyList<WowAccount> Accounts { get; init; }
    public required IReadOnlyList<WowCharacter> LocalCharacters { get; init; }
    public required Func<WowCharacter, string?> ResolveAccountDisplayName { get; init; }
    public Func<WowCharacter, WowCharacterData?>? FindWowSyncCharacter { get; init; }
}

public static class CartoSyncPayloadBuilder
{
    public static FriendSyncPayload BuildFriend(CartoSyncBuildInput input)
    {
        var chars = input.LocalCharacters
            .Where(c => !c.IsExternal && !c.ExcludeFromSync)
            .Select(CloneForNetwork)
            .ToList();

        var revision = CartoSyncRevision.ComputeFriendRevision(input.Accounts, input.LocalCharacters);

        return new FriendSyncPayload
        {
            Revision = revision,
            SentAt = DateTimeOffset.UtcNow,
            Accounts = input.Accounts.Select(CloneAccount).ToList(),
            Characters = chars
        };
    }

    public static TpBoyPublicPayload BuildTpBoyPublic(CartoSyncBuildInput input)
    {
        var entries = new List<TpBoyPublicEntry>();

        foreach (var ch in input.LocalCharacters.Where(c => !c.IsExternal && c.Status == CharacterStatus.TpBoy))
        {
            var sync = input.FindWowSyncCharacter?.Invoke(ch);
            var lastUpdate = sync?.LastUpdate ?? "";
            var shardCount = sync != null
                ? CartoCharacterEnricher.CountSoulShards(sync)
                : ch.ShardCount;

            entries.Add(new TpBoyPublicEntry
            {
                SyncKey = ch.SyncKey,
                Name = ch.Name,
                AccountDisplayName = input.ResolveAccountDisplayName(ch) ?? "",
                Class = ch.Class,
                Level = ch.Level,
                MapX = ch.MapX,
                MapY = ch.MapY,
                TerrainZoneSlug = ch.TerrainZoneSlug,
                TerrainZoneX = ch.TerrainZoneX,
                TerrainZoneY = ch.TerrainZoneY,
                IsPlacedOnMap = ch.IsPlacedOnMap,
                ShardCount = shardCount,
                LastUpdate = lastUpdate
            });
        }

        var revision = CartoSyncRevision.ComputeTpBoyRevision(entries);

        return new TpBoyPublicPayload
        {
            Revision = revision,
            SentAt = DateTimeOffset.UtcNow,
            OwnerGuid = input.OwnerGuid,
            Entries = entries
        };
    }

    public static WowCharacter ToExternalTpBoyCharacter(TpBoyPublicEntry entry, string ownerGuid)
    {
        return new WowCharacter
        {
            Id = $"tp:{ownerGuid}:{entry.SyncKey}",
            SyncKey = entry.SyncKey,
            Name = entry.Name,
            Class = entry.Class,
            Level = entry.Level,
            MapX = entry.MapX,
            MapY = entry.MapY,
            TerrainZoneSlug = entry.TerrainZoneSlug,
            TerrainZoneX = entry.TerrainZoneX,
            TerrainZoneY = entry.TerrainZoneY,
            IsPlacedOnMap = entry.IsPlacedOnMap,
            ShardCount = entry.ShardCount,
            Status = CharacterStatus.TpBoy,
            IsExternal = true,
            IsLocked = true,
            ExternalSource = CartoExternalSource.TpBoyPublic(ownerGuid),
            ExternalAccountDisplayName = entry.AccountDisplayName
        };
    }

    private static WowAccount CloneAccount(WowAccount a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        SourceFolder = a.SourceFolder,
        IsHidden = a.IsHidden
    };

    private static WowCharacter CloneForNetwork(WowCharacter ch) => new()
    {
        Id = ch.Id,
        SyncKey = ch.SyncKey,
        Name = ch.Name,
        Race = ch.Race,
        Class = ch.Class,
        Level = ch.Level,
        AccountId = ch.AccountId,
        Status = ch.Status,
        MapX = ch.MapX,
        MapY = ch.MapY,
        IsPlacedOnMap = ch.IsPlacedOnMap,
        HasCustomMapPosition = ch.HasCustomMapPosition,
        TerrainZoneSlug = ch.TerrainZoneSlug,
        TerrainZoneX = ch.TerrainZoneX,
        TerrainZoneY = ch.TerrainZoneY,
        Professions = [.. ch.Professions],
        Cooldowns = [.. ch.Cooldowns],
        QuestItems = [.. ch.QuestItems],
        Note = ch.Note,
        ShardCount = ch.ShardCount,
        IsHidden = ch.IsHidden,
        IsLocked = ch.IsLocked,
        ExcludeFromSync = ch.ExcludeFromSync,
        IsExternal = false
    };
}
