namespace WindowsOrganiserApp.Models.Craft;

public static class CraftListItemKey
{
    public static bool IsValid(CraftListItem item) =>
        item.ItemId > 0 || item.SpellId > 0;

    public static bool Matches(CraftListItem a, CraftListItem b) =>
        a.ItemId > 0 && b.ItemId > 0
            ? a.ItemId == b.ItemId
            : a.SpellId > 0 && b.SpellId > 0 && a.SpellId == b.SpellId;

    public static bool Matches(CraftListItem item, int itemId, int spellId) =>
        itemId > 0 ? item.ItemId == itemId
        : spellId > 0 && item.SpellId == spellId;
}
