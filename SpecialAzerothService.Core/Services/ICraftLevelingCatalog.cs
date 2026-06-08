using SpecialAzerothService.Core.Models.Craft;

namespace SpecialAzerothService.Core.Services;

public sealed record CraftLevelingGuideInfo(
    string ProfessionId,
    string ListNameFr,
    string ProfessionNameFr,
    IReadOnlyList<CraftListItem> Items);

public sealed record CraftLevelingStepPreview(
    int ItemId,
    int SpellId,
    int Quantity,
    string DisplayName);

public interface ICraftLevelingCatalog
{
    IReadOnlyList<CraftLevelingGuideInfo> Guides { get; }
    CraftLevelingGuideInfo? FindByProfessionId(string professionId);
    IReadOnlyList<CraftLevelingStepPreview> BuildStepPreviews(CraftLevelingGuideInfo guide);
}
