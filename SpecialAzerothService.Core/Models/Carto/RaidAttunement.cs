namespace SpecialAzerothService.Core.Models.Carto;

public enum RaidAttunementType
{
    MoltenCore,
    BlackwingLair,
    Onyxia,
    Naxxramas
}

public sealed class RaidAttunementEntry
{
    public RaidAttunementType Type { get; set; }
    public bool IsAttuned { get; set; }
}

/// <summary>
/// Quête d'accès raid HL. <see cref="SharedQuestId"/> si identique des deux côtés ;
/// sinon <see cref="HordeQuestId"/> / <see cref="AllianceQuestId"/> (IDs Ally à compléter au fil du temps).
/// </summary>
public sealed record RaidAttunementDefinition(
    RaidAttunementType Type,
    string ShortLabel,
    string NameFr,
    int IconItemId,
    int? SharedQuestId = null,
    int? HordeQuestId = null,
    int? AllianceQuestId = null)
{
    public int ResolveQuestId(Faction? faction)
    {
        if (SharedQuestId is > 0)
            return SharedQuestId.Value;

        if (faction == Faction.Alliance && AllianceQuestId is > 0)
            return AllianceQuestId.Value;

        if (faction == Faction.Horde && HordeQuestId is > 0)
            return HordeQuestId.Value;

        return HordeQuestId ?? AllianceQuestId ?? 0;
    }

    public bool IsFactionSpecific =>
        SharedQuestId is null or <= 0
        && HordeQuestId is > 0
        && AllianceQuestId is > 0;
}

public static class RaidAttunementCatalog
{
    public static IReadOnlyList<RaidAttunementDefinition> All { get; } =
    [
        new(RaidAttunementType.MoltenCore, "MC", "Cœur de Magma", 17182, SharedQuestId: 7848),
        new(RaidAttunementType.BlackwingLair, "BWL", "Repaire de l'Aile noire", 19364, SharedQuestId: 7761),
        new(RaidAttunementType.Onyxia, "Ony", "Repaire d'Onyxia", 18422,
            HordeQuestId: 6602, AllianceQuestId: 6502),
        new(RaidAttunementType.Naxxramas, "Naxx", "Naxxramas", 22809, SharedQuestId: 9122)
    ];

    public static RaidAttunementDefinition? Find(RaidAttunementType type) =>
        All.FirstOrDefault(d => d.Type == type);
}
