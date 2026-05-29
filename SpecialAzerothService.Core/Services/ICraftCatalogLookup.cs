using SpecialAzerothService.Core.Models.Craft;

namespace SpecialAzerothService.Core.Services;

public sealed class CraftLookupResult
{
    public required CraftProfession Profession { get; init; }
    public required CraftEntry Entry { get; init; }
    public required string CategoryName { get; init; }
    public bool IsTransmute { get; init; }
}

public interface ICraftCatalogLookup
{
    bool TryGetByCreatedItemId(int itemId, out CraftLookupResult result);
    bool TryGetBySpellId(int spellId, out CraftLookupResult result);
    string GetProfessionLabel(string professionId);
    string GetItemDisplayName(int itemId);
    string GetRecipeDisplayName(int itemId, int spellId);
}
