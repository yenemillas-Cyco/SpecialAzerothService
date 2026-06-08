namespace SpecialAzerothService.Core.Services;

/// <summary>IDs de sort « métier » Classic (icône de compétence secondaire).</summary>
public static class CraftProfessionIcons
{
    private static readonly Dictionary<string, int> ByProfessionId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alchemy"] = 2259,
        ["Blacksmithing"] = 2018,
        ["Enchanting"] = 7411,
        ["Engineering"] = 4036,
        ["Tailoring"] = 3908,
        ["Leatherworking"] = 2108,
        ["Mining"] = 2575,
        ["Herbalism"] = 2366,
        ["Skinning"] = 8613,
        ["Cooking"] = 2550,
        ["FirstAid"] = 3273,
        ["Fishing"] = 7620,
        ["RoguePoisons"] = 2836,
    };

    public static bool TryGetIconSpellId(string professionId, out int spellId) =>
        ByProfessionId.TryGetValue(professionId, out spellId);
}
