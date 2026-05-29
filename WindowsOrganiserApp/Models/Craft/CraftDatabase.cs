namespace WindowsOrganiserApp.Models.Craft;

public sealed class CraftDatabase
{
    public int Version { get; set; }
    public string Game { get; set; } = "";
    public string Source { get; set; } = "";
    public List<string> ContentTypes { get; set; } = [];
    public List<CraftProfession> Professions { get; set; } = [];
}

public sealed class CraftProfession
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameFr { get; set; } = "";
    public string ContentType { get; set; } = "";
    public List<CraftCategory> Categories { get; set; } = [];
}

public sealed class CraftCategory
{
    public string Name { get; set; } = "";
    public List<CraftEntry> Entries { get; set; } = [];
}

public sealed class CraftReagent
{
    public int ItemId { get; set; }
    public int Count { get; set; }
}

public sealed class CraftEntry
{
    public int Slot { get; set; }
    public bool IsItemEntry { get; set; }
    public int SpellId { get; set; }
    public int CreatedItemId { get; set; }
    public List<int> ItemIds { get; set; } = [];
    public string Label { get; set; } = "";
    public int SkillMin { get; set; }
    public int SkillLow { get; set; }
    public int SkillHigh { get; set; }
    public List<CraftReagent> Reagents { get; set; } = [];

    public int IconItemId => IsItemEntry
        ? ItemIds.Count > 0 ? ItemIds[0] : 0
        : CreatedItemId > 0 ? CreatedItemId : ItemIds.Count > 0 ? ItemIds[0] : 0;

    public IReadOnlyList<int> BonusItemIds =>
        IsItemEntry && ItemIds.Count > 1 ? ItemIds.Skip(1).ToList() : [];

    public string DisplayLabel =>
        !string.IsNullOrWhiteSpace(Label)
            ? Label
            : IconItemId > 0
                ? $"Objet #{IconItemId}"
                : SpellId > 0
                    ? $"Sort #{SpellId}"
                    : "?";
}
