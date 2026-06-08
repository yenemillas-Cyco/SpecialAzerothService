using SpecialAzerothService.Core.Models.Carto;

namespace SpecialAzerothService.Core.Services;

/// <summary>IDs des pièces T3 finales (Classic Naxxramas).</summary>
internal static class Tier3ResultItemIds
{
    // Ordre itemset Classic : poignets, torse, mains, ceinture, tête, jambes, épaules, pieds.
    private static readonly Tier3ArmorSlot[] SetOrder =
    [
        Tier3ArmorSlot.Wrist,
        Tier3ArmorSlot.Chest,
        Tier3ArmorSlot.Hands,
        Tier3ArmorSlot.Belt,
        Tier3ArmorSlot.Head,
        Tier3ArmorSlot.Legs,
        Tier3ArmorSlot.Shoulders,
        Tier3ArmorSlot.Feet
    ];

    private static readonly Dictionary<WowClass, int> SequentialSetStart = new()
    {
        [WowClass.Guerrier] = 22416,
        [WowClass.Paladin] = 22424,
        [WowClass.Chasseur] = 22436,
        [WowClass.Chaman] = 22464,
        [WowClass.Druide] = 22488,
        [WowClass.Mage] = 22496,
        [WowClass.Demoniste] = 22504,
        [WowClass.Pretre] = 22512
    };

    private static readonly Dictionary<(WowClass, Tier3ArmorSlot), int> RogueIds = new()
    {
        [(WowClass.Voleur, Tier3ArmorSlot.Wrist)] = 22483,
        [(WowClass.Voleur, Tier3ArmorSlot.Chest)] = 22476,
        [(WowClass.Voleur, Tier3ArmorSlot.Hands)] = 22481,
        [(WowClass.Voleur, Tier3ArmorSlot.Belt)] = 22482,
        [(WowClass.Voleur, Tier3ArmorSlot.Head)] = 22478,
        [(WowClass.Voleur, Tier3ArmorSlot.Legs)] = 22477,
        [(WowClass.Voleur, Tier3ArmorSlot.Shoulders)] = 22479,
        [(WowClass.Voleur, Tier3ArmorSlot.Feet)] = 22480
    };

    public static int Get(WowClass wowClass, Tier3ArmorSlot slot)
    {
        if (wowClass == WowClass.Voleur)
            return RogueIds[(wowClass, slot)];

        if (!SequentialSetStart.TryGetValue(wowClass, out var start))
            throw new ArgumentOutOfRangeException(nameof(wowClass), wowClass, "Classe T3 inconnue.");

        var index = Array.IndexOf(SetOrder, slot);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Emplacement T3 inconnu.");

        return start + index;
    }
}
