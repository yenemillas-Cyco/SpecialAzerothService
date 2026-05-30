namespace SpecialAzerothService.Core.Models.Carto;

public sealed class DungeonCatalogListItem
{
    public string Key { get; init; } = "";
    public string NameFr { get; init; } = "";
    public string ParentZoneFr { get; init; } = "";
    public bool IsLieuDit { get; init; }

    public string DisplayLabel
    {
        get
        {
            var kind = IsLieuDit ? "[Lieu-dit] " : "";
            return string.IsNullOrWhiteSpace(ParentZoneFr)
                ? $"{kind}{NameFr}"
                : $"{kind}{NameFr} — {ParentZoneFr}";
        }
    }
}
