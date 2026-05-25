namespace WindowsOrganiserApp.Models.WowSync;

public sealed class WowheadItemDetails
{
    public string Name { get; init; } = "";
    public int Quality { get; init; }
    public string IconSlug { get; init; } = "";
    public int? ItemLevel { get; init; }
    public int? MaxStack { get; init; }
    public int SellGold { get; init; }
    public int SellSilver { get; init; }
    public int SellCopper { get; init; }
    public List<string> ExtraLines { get; init; } = [];
}

public sealed class WowItemSearchResult
{
    public string ItemName { get; init; } = "";
    public int ItemId { get; init; }
    public int Count { get; init; }
    public string AccountName { get; init; } = "";
    public string CharacterName { get; init; } = "";
    public string Location { get; init; } = "";
    public WowCharacterData Character { get; init; } = null!;

    public string DisplayLine => $"{CharacterName} — {Location} (x{Count})";
}
