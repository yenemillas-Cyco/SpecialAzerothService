using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpecialAzerothService.Core.Models.Craft;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;

namespace WindowsOrganiserApp.ViewModels;

public sealed class CraftProfessionNode : ObservableObject
{
    public CraftProfession Profession { get; }
    public string Label => Profession.NameFr;
    public CraftProfessionNode(CraftProfession profession) => Profession = profession;
}

public sealed class CraftCategoryNode : ObservableObject
{
    public CraftCategory Category { get; }
    public string Label => CraftDisplayNames.CategoryFr(Category.Name);
    public int EntryCount => Category.Entries.Count;
    public CraftCategoryNode(CraftCategory category) => Category = category;
}

public sealed class CraftReagentChip : ObservableObject
{
    public int ItemId { get; }
    public int Count { get; }
    public WowItem Item { get; }

    private string _displayName = "";
    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
                OnPropertyChanged(nameof(LabelLine));
        }
    }

    /// <summary>Nom seul — la quantité est sur l’icône.</summary>
    public string LabelLine => string.IsNullOrWhiteSpace(DisplayName)
        ? $"#{ItemId}"
        : DisplayName;

    public string NameQualityColor => Item.QualityColor;

    public void ApplyQuality(int quality)
    {
        Item.Quality = quality;
        OnPropertyChanged(nameof(NameQualityColor));
    }

    public CraftReagentChip(int itemId, int count)
    {
        ItemId = itemId;
        Count = count;
        Item = new WowItem { ItemId = itemId, Count = count };
        DisplayName = $"#{itemId}";
    }
}

public sealed class CraftRecipeRow : ObservableObject
{
    public CraftEntry Entry { get; }
    public WowItem IconItem { get; }
    public ObservableCollection<CraftReagentChip> Reagents { get; } = [];
    public string ProfessionId { get; }

    public bool CanAddToList => !Entry.IsItemEntry && (Entry.CreatedItemId > 0 || Entry.SpellId > 0);

    private string _displayName = "";
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    private string _skillText = "";
    public string SkillText
    {
        get => _skillText;
        set => SetProperty(ref _skillText, value);
    }

    public bool IsGathering => Entry.IsItemEntry;
    public bool HasReagents => !IsGathering && Reagents.Count > 0;
    public bool HasBonusDrops => BonusDrops.Count > 0;

    public ObservableCollection<CraftReagentChip> BonusDrops { get; } = [];

    public CraftRecipeRow(CraftEntry entry, string professionId)
    {
        Entry = entry;
        ProfessionId = professionId;
        IconItem = new WowItem
        {
            ItemId = entry.CreatedItemId > 0 ? entry.CreatedItemId : 0,
            SpellId = entry.CreatedItemId > 0 ? 0 : entry.SpellId,
            Count = 0
        };
        DisplayName = entry.DisplayLabel;
        SkillText = FormatSkill(entry);

        if (!entry.IsItemEntry)
        {
            foreach (var reagent in entry.Reagents)
                Reagents.Add(new CraftReagentChip(reagent.ItemId, reagent.Count));
        }
        else
        {
            foreach (var bonusId in entry.BonusItemIds)
                BonusDrops.Add(new CraftReagentChip(bonusId, 1));
        }
    }

    private static string FormatSkill(CraftEntry e)
    {
        if (e.SkillHigh <= 0) return "";
        if (e.SkillMin > 0)
            return $"{e.SkillMin} · {e.SkillLow} · {e.SkillHigh}";
        return $"{e.SkillLow} · {e.SkillHigh}";
    }

    public string NameQualityColor => IconItem.QualityColor;

    public void ApplyQuality(int quality)
    {
        IconItem.Quality = quality;
        OnPropertyChanged(nameof(NameQualityColor));
    }
}

public partial class CraftViewModel : ObservableObject
{
    private readonly ICraftService _craftService;
    private readonly IWowItemLookupService _itemLookup;
    private readonly ICraftListsService _lists;
    private readonly ICraftPlanningContext _planning;
    private CancellationTokenSource? _loadCts;

    public CraftViewModel(
        ICraftService craftService,
        IWowItemLookupService itemLookup,
        ICraftListsService lists,
        ICraftPlanningContext planning)
    {
        _craftService = craftService;
        _itemLookup = itemLookup;
        _lists = lists;
        _planning = planning;
        _planning.ActiveListChanged += () =>
        {
            OnPropertyChanged(nameof(CanAddToFarmList));
            AddToFarmListCommand.NotifyCanExecuteChanged();
        };

        foreach (var ct in _craftService.Database.ContentTypes
                     .Where(ct => !string.Equals(ct, "Gathering", StringComparison.OrdinalIgnoreCase)))
            ContentTypes.Add(new CraftContentTypeOption(ct, CraftDisplayNames.ContentTypeFr(ct)));

        _selectedContentType = ContentTypes.FirstOrDefault();
        ReloadProfessions();
        if (ProfessionNodes.Count > 0)
            SelectedProfession = ProfessionNodes[0];
    }

    public ObservableCollection<CraftContentTypeOption> ContentTypes { get; } = [];
    public ObservableCollection<CraftProfessionNode> ProfessionNodes { get; } = [];
    public ObservableCollection<CraftCategoryNode> CategoryNodes { get; } = [];
    public ObservableCollection<CraftRecipeRow> RecipeRows { get; } = [];

    [ObservableProperty]
    private CraftContentTypeOption? _selectedContentType;

    [ObservableProperty]
    private CraftProfessionNode? _selectedProfession;

    [ObservableProperty]
    private CraftCategoryNode? _selectedCategory;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _statusText = "";

    public bool CanAddToFarmList => true;

    [RelayCommand]
    private void AddToFarmList(CraftRecipeRow? row)
    {
        if (row == null || !row.CanAddToList) return;

        var listId = EnsureActiveFarmList();
        if (listId == null) return;

        var list = _lists.GetById(listId);
        if (list?.EnsureItems().Any(i => CraftListItemKey.Matches(i, row.Entry.CreatedItemId, row.Entry.SpellId)) == true)
        {
            StatusText = "Déjà dans la liste de farm — règle la quantité dans Crafting.";
            _planning.NotifyListItemsChanged();
            return;
        }

        _lists.AddItem(listId, new CraftListItem
        {
            ItemId = row.Entry.CreatedItemId,
            Quantity = 1,
            ProfessionId = row.ProfessionId,
            SpellId = row.Entry.SpellId
        });
        _planning.NotifyListItemsChanged();
        StatusText = "Ajouté à la liste — règle la quantité avec ＋ / −.";
    }

    public string SelectedProfessionTitle =>
        SelectedProfession != null
            ? $"{SelectedProfession.Label} — {SelectedCategory?.Label ?? "…"}"
            : "Craft";

    partial void OnSelectedContentTypeChanged(CraftContentTypeOption? value) => ReloadProfessions();

    partial void OnSelectedProfessionChanged(CraftProfessionNode? value)
    {
        CategoryNodes.Clear();
        SelectedCategory = null;
        if (value == null)
        {
            RecipeRows.Clear();
            OnPropertyChanged(nameof(SelectedProfessionTitle));
            return;
        }

        foreach (var cat in value.Profession.Categories)
            CategoryNodes.Add(new CraftCategoryNode(cat));

        SelectedCategory = CategoryNodes.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedProfessionTitle));
    }

    partial void OnSelectedCategoryChanged(CraftCategoryNode? value)
    {
        RebuildRecipeList();
        OnPropertyChanged(nameof(SelectedProfessionTitle));
    }

    partial void OnSearchTextChanged(string value) => ApplySearchFilter();

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    private void ReloadProfessions()
    {
        ProfessionNodes.Clear();
        SelectedProfession = null;
        var filter = SelectedContentType?.Key;
        foreach (var p in _craftService.GetProfessions(filter))
            ProfessionNodes.Add(new CraftProfessionNode(p));

        SelectedProfession = ProfessionNodes.FirstOrDefault();
        StatusText = $"{ProfessionNodes.Count} métiers · {_craftService.Database.Professions.Sum(p => p.Categories.Count)} catégories";
    }

    private void ApplySearchFilter()
    {
        var q = SearchText.Trim();
        if (string.IsNullOrEmpty(q))
        {
            ReloadProfessions();
            return;
        }

        ProfessionNodes.Clear();
        SelectedProfession = null;
        var filter = SelectedContentType?.Key;

        foreach (var prof in _craftService.GetProfessions(filter))
        {
            var matchProf = prof.NameFr.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || prof.Name.Contains(q, StringComparison.OrdinalIgnoreCase);
            var matchingCats = prof.Categories
                .Where(c => CraftDisplayNames.CategoryFr(c.Name).Contains(q, StringComparison.OrdinalIgnoreCase)
                            || c.Entries.Any(e => e.DisplayLabel.Contains(q, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (!matchProf && matchingCats.Count == 0)
                continue;

            var clone = new CraftProfession
            {
                Id = prof.Id,
                Name = prof.Name,
                NameFr = prof.NameFr,
                ContentType = prof.ContentType,
                Categories = matchProf ? prof.Categories : matchingCats
            };
            ProfessionNodes.Add(new CraftProfessionNode(clone));
        }

        SelectedProfession = ProfessionNodes.FirstOrDefault();
        StatusText = $"{ProfessionNodes.Count} résultat(s)";
    }

    private void RebuildRecipeList()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        RecipeRows.Clear();
        var entries = SelectedCategory?.Category.Entries ?? [];
        var professionId = SelectedProfession?.Profession.Id ?? "";

        foreach (var entry in entries.OrderBy(e => e.Slot))
            RecipeRows.Add(new CraftRecipeRow(entry, professionId));

        if (SelectedCategory != null)
            StatusText = $"{RecipeRows.Count} recette(s)";

        _ = LoadRecipeDetailsAsync(token);
    }

    /// <summary>Utilise la liste active, ou la première disponible, ou en crée une.</summary>
    private string? EnsureActiveFarmList()
    {
        if (!string.IsNullOrEmpty(_planning.ActiveListId))
            return _planning.ActiveListId;

        var existing = _lists.Lists.FirstOrDefault();
        if (existing != null)
        {
            _planning.SetActiveList(existing.Id);
            return existing.Id;
        }

        var created = _lists.CreateList("Ma liste");
        _planning.SetActiveList(created.Id);
        _planning.NotifyListItemsChanged();
        return created.Id;
    }

    private async Task LoadRecipeDetailsAsync(CancellationToken token)
    {
        var rows = RecipeRows.ToList();
        foreach (var row in rows)
        {
            if (token.IsCancellationRequested) return;

            var itemId = row.Entry.IconItemId;
            if (itemId > 0)
            {
                try
                {
                    var details = await _itemLookup.GetDetailsAsync(new WowItem { ItemId = itemId }, token);
                    if (details != null && !string.IsNullOrWhiteSpace(details.Name))
                    {
                        row.DisplayName = details.Name;
                        row.IconItem.Name = details.Name;
                        row.ApplyQuality(details.Quality);
                    }
                }
                catch
                {
                    // keep fallback label
                }
            }
            else if (row.Entry.SpellId > 0)
            {
                try
                {
                    var details = await _itemLookup.GetSpellDetailsAsync(row.Entry.SpellId, token);
                    if (details != null && !string.IsNullOrWhiteSpace(details.Name))
                    {
                        row.DisplayName = details.Name;
                        row.IconItem.Name = details.Name;
                        row.ApplyQuality(details.Quality);
                    }
                }
                catch
                {
                    // keep fallback label
                }
            }

            await LoadChipsAsync(row.Reagents, token).ConfigureAwait(true);
            await LoadChipsAsync(row.BonusDrops, token).ConfigureAwait(true);
        }
    }

    private async Task LoadChipsAsync(IEnumerable<CraftReagentChip> chips, CancellationToken token)
    {
        foreach (var chip in chips)
        {
            if (token.IsCancellationRequested) return;
            try
            {
                var details = await _itemLookup.GetDetailsAsync(new WowItem { ItemId = chip.ItemId }, token)
                    .ConfigureAwait(true);
                if (token.IsCancellationRequested) return;

                if (details != null && !string.IsNullOrWhiteSpace(details.Name))
                {
                    chip.DisplayName = details.Name;
                    chip.Item.Name = details.Name;
                    chip.ApplyQuality(details.Quality);
                }
            }
            catch
            {
                // garde le libellé de secours
            }
        }
    }
}

public sealed class CraftContentTypeOption
{
    public string Key { get; }
    public string Label { get; }
    public CraftContentTypeOption(string key, string label) { Key = key; Label = label; }
}
