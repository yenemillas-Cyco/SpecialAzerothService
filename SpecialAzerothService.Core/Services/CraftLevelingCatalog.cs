using System.IO;
using System.Reflection;
using System.Text.Json;
using SpecialAzerothService.Core.Models.Craft;

namespace SpecialAzerothService.Core.Services;

public sealed class CraftLevelingCatalog : ICraftLevelingCatalog
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ICraftCatalogLookup _catalog;
    private readonly Lazy<IReadOnlyList<CraftLevelingGuideInfo>> _guides;

    public CraftLevelingCatalog(ICraftCatalogLookup catalog)
    {
        _catalog = catalog;
        _guides = new Lazy<IReadOnlyList<CraftLevelingGuideInfo>>(LoadGuides);
    }

    public IReadOnlyList<CraftLevelingGuideInfo> Guides => _guides.Value;

    public CraftLevelingGuideInfo? FindByProfessionId(string professionId) =>
        Guides.FirstOrDefault(g => g.ProfessionId.Equals(professionId, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<CraftLevelingStepPreview> BuildStepPreviews(CraftLevelingGuideInfo guide) =>
        guide.Items.Select(item => new CraftLevelingStepPreview(
            item.ItemId,
            item.SpellId,
            item.Quantity,
            _catalog.GetRecipeDisplayName(item.ItemId, item.SpellId))).ToList();

    private IReadOnlyList<CraftLevelingGuideInfo> LoadGuides()
    {
        var seed = LoadEmbeddedSeed();
        if (seed?.Lists == null || seed.Lists.Count == 0)
            return [];

        var guides = new List<CraftLevelingGuideInfo>();
        foreach (var list in seed.Lists)
        {
            list.EnsureItems();
            if (list.Items.Count == 0)
                continue;

            var professionId = list.Items
                .Select(i => i.ProfessionId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id))
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(professionId))
                continue;

            guides.Add(new CraftLevelingGuideInfo(
                professionId,
                list.Name,
                _catalog.GetProfessionLabel(professionId),
                list.Items.Select(CloneItem).ToList()));
        }

        return guides
            .OrderBy(g => g.ListNameFr, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CraftListItem CloneItem(CraftListItem item) => new()
    {
        ItemId = item.ItemId,
        SpellId = item.SpellId,
        Quantity = item.Quantity,
        ProfessionId = item.ProfessionId
    };

    private static CraftListsData? LoadEmbeddedSeed()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            const string resourceName = "SpecialAzerothService.Core.Assets.CraftLevelingLists.seed.json";
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) return null;

            using var reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<CraftListsData>(reader.ReadToEnd(), JsonOpts);
        }
        catch
        {
            return null;
        }
    }
}
