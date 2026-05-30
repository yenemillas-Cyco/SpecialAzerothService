using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

/// <summary>CD métiers suivis selon recettes connues (addon) ou suivi manuel — pas de seuil de niveau fixe.</summary>
public static class CartoProfessionCooldowns
{
    public static bool HasAnyProfession(WowCharacter ch, WowCharacterData? sync = null) =>
        QualifiesForCooldownRoster(ch, sync);

    /// <summary>Visible dans le volet Cooldowns.</summary>
    public static bool QualifiesForCooldownRoster(WowCharacter ch, WowCharacterData? sync = null) =>
        GetTrackedCooldownTypes(ch, sync).Any();

    public static IEnumerable<ProfessionType> ResolveProfessionTypes(WowCharacter ch, WowCharacterData? sync)
    {
        var set = new HashSet<ProfessionType>();

        foreach (var p in ch.Professions)
            set.Add(p.Type);

        if (sync != null)
        {
            foreach (var p in sync.Professions)
            {
                if (CartoCharacterEnricher.TryMapProfessionName(p.Name, out var type))
                    set.Add(type);
            }
        }

        foreach (var cdType in GetTrackedCooldownTypes(ch, sync))
        {
            if (ProfessionForCooldownType(cdType) is { } prof)
                set.Add(prof);
        }

        return set;
    }

    public static ProfessionType? ProfessionForCooldownType(CooldownType type)
    {
        if (CooldownGroups.IsAlchemyTransmute(type))
            return ProfessionType.Alchimie;
        if (type is CooldownType.Mooncloth or CooldownType.Etoffe_lunaire)
            return ProfessionType.Couture;
        if (type == CooldownType.Sel_raffine)
            return ProfessionType.Travail_du_cuir;
        return null;
    }

    public static IEnumerable<CooldownType> GetTrackedCooldownTypes(WowCharacter ch, WowCharacterData? sync)
    {
        var set = new HashSet<CooldownType>();

        foreach (var cd in ch.Cooldowns)
        {
            if (ProfessionForCooldownType(cd.Type) is not null)
                set.Add(NormalizeTrackedType(cd.Type));
        }

        if (sync != null)
        {
            foreach (var key in sync.KnownCooldownKeys)
                AddMappedType(set, CooldownGroups.MapSyncCooldownKey(key, null));

            foreach (var syncCd in sync.Cooldowns)
                AddMappedType(set, CooldownGroups.MapSyncCooldownKey(syncCd.Key, syncCd.Name));
        }

        return set;
    }

    public static bool KnowsCooldownType(CooldownType type, WowCharacter ch, WowCharacterData? sync) =>
        GetTrackedCooldownTypes(ch, sync).Any(t => MatchesCooldownType(t, type));

    public static void EnsureProfessionSlots(
        IDictionary<CooldownType, CooldownEntry> byType,
        WowCharacter ch,
        WowCharacterData? sync)
    {
        foreach (var cdType in GetTrackedCooldownTypes(ch, sync))
        {
            if (CooldownGroups.IsAlchemyTransmute(cdType))
            {
                if (!byType.Keys.Any(CooldownGroups.IsAlchemyTransmute))
                    byType[cdType] = new CooldownEntry { Type = cdType };
            }
            else if (!byType.ContainsKey(cdType))
            {
                byType[cdType] = new CooldownEntry { Type = cdType };
            }
        }
    }

    public static bool IsCooldownEntryVisible(CooldownEntry cd, WowCharacter ch, WowCharacterData? sync)
    {
        if (cd.LastUsed != null || cd.ReadyAtOverride != null)
            return true;

        return KnowsCooldownType(cd.Type, ch, sync);
    }

    private static void AddMappedType(HashSet<CooldownType> set, CooldownType? type)
    {
        if (type == null)
            return;
        set.Add(NormalizeTrackedType(type.Value));
    }

    private static CooldownType NormalizeTrackedType(CooldownType type) =>
        CooldownGroups.IsAlchemyTransmute(type) && type != CooldownType.Arcanite
            ? type
            : CooldownGroups.IsAlchemyTransmute(type)
                ? CooldownType.Arcanite
                : type;

    private static bool MatchesCooldownType(CooldownType a, CooldownType b)
    {
        if (a == b)
            return true;
        return CooldownGroups.IsAlchemyTransmute(a) && CooldownGroups.IsAlchemyTransmute(b);
    }
}
