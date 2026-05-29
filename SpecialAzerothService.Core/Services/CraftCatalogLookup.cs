using SpecialAzerothService.Core.Models.Craft;

namespace SpecialAzerothService.Core.Services;

public sealed class CraftCatalogLookup : ICraftCatalogLookup
{
    private readonly Dictionary<int, CraftLookupResult> _byCreatedItemId = [];
    private readonly Dictionary<int, CraftLookupResult> _bySpellId = [];
    private readonly Dictionary<string, string> _professionLabels = new(StringComparer.OrdinalIgnoreCase);

    public CraftCatalogLookup(ICraftService craftService)
    {
        foreach (var profession in craftService.Database.Professions)
        {
            _professionLabels[profession.Id] = profession.NameFr;

            foreach (var category in profession.Categories)
            {
                foreach (var entry in category.Entries)
                {
                    if (entry.IsItemEntry) continue;

                    var isTransmute = IsTransmuteCategory(category.Name)
                        || entry.Label.Contains("Transmute", StringComparison.OrdinalIgnoreCase);

                    var lookup = new CraftLookupResult
                    {
                        Profession = profession,
                        Entry = entry,
                        CategoryName = category.Name,
                        IsTransmute = isTransmute
                    };

                    if (entry.CreatedItemId > 0)
                        _byCreatedItemId.TryAdd(entry.CreatedItemId, lookup);

                    if (entry.SpellId > 0)
                        _bySpellId.TryAdd(entry.SpellId, lookup);
                }
            }
        }
    }

    public bool TryGetByCreatedItemId(int itemId, out CraftLookupResult result) =>
        _byCreatedItemId.TryGetValue(itemId, out result!);

    public bool TryGetBySpellId(int spellId, out CraftLookupResult result) =>
        _bySpellId.TryGetValue(spellId, out result!);

    public string GetProfessionLabel(string professionId) =>
        _professionLabels.TryGetValue(professionId, out var label) ? label : professionId;

    public string GetItemDisplayName(int itemId) =>
        TryGetByCreatedItemId(itemId, out var lookup) && !string.IsNullOrWhiteSpace(lookup.Entry.DisplayLabel)
            ? lookup.Entry.DisplayLabel
            : $"#{itemId}";

    public string GetRecipeDisplayName(int itemId, int spellId)
    {
        if (itemId > 0 && TryGetByCreatedItemId(itemId, out var byItem)
            && !string.IsNullOrWhiteSpace(byItem.Entry.DisplayLabel))
            return byItem.Entry.DisplayLabel;

        if (spellId > 0 && TryGetBySpellId(spellId, out var bySpell)
            && !string.IsNullOrWhiteSpace(bySpell.Entry.DisplayLabel))
            return bySpell.Entry.DisplayLabel;

        if (spellId > 0) return $"Sort #{spellId}";
        if (itemId > 0) return $"#{itemId}";
        return "?";
    }

    private static bool IsTransmuteCategory(string categoryName) =>
        categoryName.Equals("Transmutes", StringComparison.OrdinalIgnoreCase)
        || categoryName.Contains("Transmute", StringComparison.OrdinalIgnoreCase);
}
