namespace WindowsOrganiserApp.Models.Craft;

public sealed class CraftListsData
{
    public int Version { get; set; } = 1;
    public List<CraftListDefinition> Lists { get; set; } = [];
}

public sealed class CraftListDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<CraftListItem> Items { get; set; } = [];

    public List<CraftListItem> EnsureItems() => Items ??= [];
}

public sealed class CraftListItem
{
    public int ItemId { get; set; }
    public int Quantity { get; set; }
    public string? ProfessionId { get; set; }
    public int SpellId { get; set; }
}
