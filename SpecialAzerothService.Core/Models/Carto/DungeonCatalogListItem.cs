namespace SpecialAzerothService.Core.Models.Carto;

public sealed class DungeonCatalogListItem
{
    public string Key { get; init; } = "";
    public string NameFr { get; init; } = "";
    public string ParentZoneFr { get; init; } = "";

    public string DisplayLabel => string.IsNullOrWhiteSpace(ParentZoneFr)
        ? NameFr
        : $"{NameFr} — {ParentZoneFr}";
}
