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

        RegisterTier3QuestPieces();
    }

    private void RegisterTier3QuestPieces()
    {
        foreach (var category in Tier3QuestCatalog.Categories)
        {
            foreach (var cls in category.Classes)
            {
                foreach (var piece in cls.Pieces)
                {
                    if (piece.ResultItemId <= 0)
                        continue;

                    var entry = new CraftEntry
                    {
                        CreatedItemId = piece.ResultItemId,
                        Label = piece.PieceNameFr,
                        Reagents = piece.Materials
                            .Select(m => new CraftReagent { ItemId = m.ItemId, Count = m.Quantity })
                            .ToList()
                    };

                    var professionId = category.Id switch
                    {
                        _ when category.Id == Tier3QuestCatalog.QuestIdEko =>
                            Tier3QuestCatalog.ProfessionIdEko(piece.ResultItemId),
                        _ when category.Id == Tier3QuestCatalog.QuestIdBl =>
                            Tier3QuestCatalog.ProfessionIdBl(piece.ResultItemId),
                        _ when category.Id == Tier3QuestCatalog.QuestIdArcanum =>
                            Tier3QuestCatalog.ProfessionIdArcanum(piece.ResultItemId),
                        _ when category.Id == Tier3QuestCatalog.QuestIdArgentDawn =>
                            Tier3QuestCatalog.ProfessionIdArgentDawn(piece.ResultItemId),
                        _ => Tier3QuestCatalog.ProfessionId(cls.Class!.Value, piece.Slot!.Value)
                    };
                    var professionLabel = category.Id switch
                    {
                        _ when category.Id == Tier3QuestCatalog.QuestIdEko =>
                            Tier3QuestCatalog.ProfessionLabelEko(piece.ResultItemId),
                        _ when category.Id == Tier3QuestCatalog.QuestIdBl =>
                            Tier3QuestCatalog.ProfessionLabelBl(piece.ResultItemId),
                        _ when category.Id == Tier3QuestCatalog.QuestIdArcanum =>
                            Tier3QuestCatalog.ProfessionLabelArcanum(piece.ResultItemId),
                        _ when category.Id == Tier3QuestCatalog.QuestIdArgentDawn =>
                            Tier3QuestCatalog.ProfessionLabelArgentDawn(piece.ResultItemId),
                        _ => Tier3QuestCatalog.ProfessionLabel(cls.Class!.Value, piece.Slot!.Value)
                    };

                    var profession = new CraftProfession
                    {
                        Id = professionId,
                        NameFr = professionLabel
                    };

                    _byCreatedItemId.TryAdd(piece.ResultItemId, new CraftLookupResult
                    {
                        Entry = entry,
                        Profession = profession,
                        CategoryName = category.ShortTitleFr,
                        IsTransmute = false
                    });
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

    public string GetItemDisplayName(int itemId)
    {
        var t3Name = Tier3QuestCatalog.FindMaterialDisplayName(itemId);
        if (!string.IsNullOrEmpty(t3Name))
            return t3Name;

        return TryGetByCreatedItemId(itemId, out var lookup) && !string.IsNullOrWhiteSpace(lookup.Entry.DisplayLabel)
            ? lookup.Entry.DisplayLabel
            : $"#{itemId}";
    }

    public string GetRecipeDisplayName(int itemId, int spellId)
    {
        var t3PieceName = Tier3QuestCatalog.FindResultDisplayName(itemId);
        if (!string.IsNullOrEmpty(t3PieceName))
            return t3PieceName;

        if (itemId > 0 && TryGetByCreatedItemId(itemId, out var byItem)
            && !string.IsNullOrWhiteSpace(byItem.Entry.DisplayLabel))
            return byItem.Entry.DisplayLabel;

        if (spellId > 0 && TryGetBySpellId(spellId, out var bySpell)
            && !string.IsNullOrWhiteSpace(bySpell.Entry.DisplayLabel))
            return bySpell.Entry.DisplayLabel;

        var t3Name = Tier3QuestCatalog.FindMaterialDisplayName(itemId);
        if (!string.IsNullOrEmpty(t3Name))
            return t3Name;

        if (spellId > 0) return $"Sort #{spellId}";
        if (itemId > 0) return $"#{itemId}";
        return "?";
    }

    private static bool IsTransmuteCategory(string categoryName) =>
        categoryName.Equals("Transmutes", StringComparison.OrdinalIgnoreCase)
        || categoryName.Contains("Transmute", StringComparison.OrdinalIgnoreCase);
}
