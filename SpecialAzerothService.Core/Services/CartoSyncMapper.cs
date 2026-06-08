using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

public static class CartoSyncMapper
{
    private static readonly Dictionary<string, WowClass> ClassByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Guerrier"] = WowClass.Guerrier,
        ["Warrior"] = WowClass.Guerrier,
        ["Paladin"] = WowClass.Paladin,
        ["Chasseur"] = WowClass.Chasseur,
        ["Hunter"] = WowClass.Chasseur,
        ["Voleur"] = WowClass.Voleur,
        ["Rogue"] = WowClass.Voleur,
        ["Prêtre"] = WowClass.Pretre,
        ["Pretre"] = WowClass.Pretre,
        ["Priest"] = WowClass.Pretre,
        ["Chaman"] = WowClass.Chaman,
        ["Shaman"] = WowClass.Chaman,
        ["Mage"] = WowClass.Mage,
        ["Démoniste"] = WowClass.Demoniste,
        ["Demoniste"] = WowClass.Demoniste,
        ["Warlock"] = WowClass.Demoniste,
        ["Druide"] = WowClass.Druide,
        ["Druid"] = WowClass.Druide,
    };

    public static WowClass ParseClass(string? className)
    {
        if (string.IsNullOrWhiteSpace(className)) return WowClass.Guerrier;
        return ClassByName.TryGetValue(className.Trim(), out var wc) ? wc : WowClass.Guerrier;
    }

    public static WowCharacter ToCartoCharacter(
        WowCharacterData sync,
        string? accountId,
        CartoCharacterExtras? extras,
        CartoCharacterProfile? profile,
        int stackIndex)
    {
        _ = stackIndex;
        var isPlaced = extras?.IsPlacedOnMap == true;

        return new WowCharacter
        {
            Id = extras?.Id ?? Guid.NewGuid().ToString(),
            SyncKey = sync.Key,
            Name = sync.Name,
            Race = sync.Race ?? "",
            Class = ParseClass(sync.Class),
            Level = sync.Level > 0 ? sync.Level : 1,
            AccountId = accountId ?? extras?.AccountId,
            Status = profile?.Category ?? extras?.Status ?? CharacterStatus.Reroll,
            IsPlacedOnMap = isPlaced,
            MapX = 0,
            MapY = 0,
            HasCustomMapPosition = false,
            Professions = extras?.Professions != null
                ? [.. extras.Professions]
                : [],
            Cooldowns = extras?.Cooldowns != null
                ? [.. extras.Cooldowns]
                : [],
            QuestItems = extras?.QuestItems != null
                ? [.. extras.QuestItems]
                : [],
            RaidAttunements = extras?.RaidAttunements != null
                ? [.. extras.RaidAttunements]
                : [],
            Note = profile?.Note ?? extras?.Note ?? "",
            ShardCount = extras?.ShardCount ?? 0,
            IsHidden = extras?.IsHidden ?? false,
            IsLocked = extras?.IsLocked ?? false
        };
    }

    public static CartoCharacterProfile ToProfile(WowCharacter ch) => new()
    {
        SyncKey = ch.SyncKey,
        Category = ch.Status,
        Note = ch.Note ?? ""
    };

    /// <summary>Positions carte : uniquement via WowSync en mémoire — pas de persistance locale.</summary>
    public static CartoCharacterExtras ToExtras(WowCharacter ch) => new()
    {
        Id = ch.Id,
        SyncKey = ch.SyncKey,
        AccountId = ch.AccountId,
        Professions = [.. ch.Professions],
        Cooldowns = [.. ch.Cooldowns],
        QuestItems = [.. ch.QuestItems],
        RaidAttunements = [.. ch.RaidAttunements],
        ShardCount = ch.ShardCount,
        IsHidden = ch.IsHidden,
        IsLocked = ch.IsLocked,
        IsPlacedOnMap = ch.IsPlacedOnMap,
        HasCustomMapPosition = ch.HasCustomMapPosition,
        MapX = 0,
        MapY = 0
    };

    public static void ApplyCooldownsFromSync(WowCharacterData sync, WowCharacter carto) =>
        CooldownSyncMerge.ApplySyncCooldowns(sync, carto);

    public static CartoCharacterProfile MigrateLegacyProfile(WowCharacter ch) => new()
    {
        SyncKey = string.IsNullOrEmpty(ch.SyncKey) ? ch.Name : ch.SyncKey,
        Category = ch.Status,
        Note = ch.Note ?? ""
    };

    public static CartoCharacterExtras MigrateLegacyCharacter(WowCharacter ch) => new()
    {
        Id = ch.Id,
        SyncKey = string.IsNullOrEmpty(ch.SyncKey) ? ch.Name : ch.SyncKey,
        AccountId = ch.AccountId,
        Professions = [.. ch.Professions],
        Cooldowns = [.. ch.Cooldowns],
        QuestItems = [.. ch.QuestItems],
        RaidAttunements = [.. ch.RaidAttunements],
        ShardCount = ch.ShardCount,
        IsHidden = ch.IsHidden,
        IsLocked = ch.IsLocked,
        IsPlacedOnMap = ch.IsPlacedOnMap,
        HasCustomMapPosition = false,
        MapX = 0,
        MapY = 0
    };
}
