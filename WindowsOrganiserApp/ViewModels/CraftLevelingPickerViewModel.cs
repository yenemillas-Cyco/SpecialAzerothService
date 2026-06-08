using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpecialAzerothService.Core.Models.Craft;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.ViewModels;

public sealed partial class CraftLevelingPickerViewModel : ObservableObject
{
    private readonly string _listId;
    private readonly ICraftListsService _lists;
    private readonly ICraftLevelingCatalog _leveling;
    private readonly ICraftPlanningContext _planning;
    private readonly IWowItemLookupService _itemLookup;
    private CancellationTokenSource? _stepLoadCts;

    public CraftLevelingPickerViewModel(
        string listId,
        ICraftListsService lists,
        ICraftLevelingCatalog leveling,
        ICraftPlanningContext planning,
        IWowItemLookupService itemLookup)
    {
        _listId = listId;
        _lists = lists;
        _leveling = leveling;
        _planning = planning;
        _itemLookup = itemLookup;

        foreach (var guide in _leveling.Guides)
            Guides.Add(new CraftLevelingGuideRow(guide));

        SelectedGuide = Guides.FirstOrDefault();
    }

    public ObservableCollection<CraftLevelingGuideRow> Guides { get; } = [];

    [ObservableProperty]
    private CraftLevelingGuideRow? _selectedGuide;

    public ObservableCollection<CraftLevelingStepRow> StepRows { get; } = [];

    [ObservableProperty]
    private string _statusText = "Choisissez un métier, puis chargez le guide dans la liste active.";

    [ObservableProperty]
    private string _previewTitle = "Recettes";

    public bool CanLoad => SelectedGuide != null;

    partial void OnSelectedGuideChanged(CraftLevelingGuideRow? value)
    {
        _stepLoadCts?.Cancel();
        _stepLoadCts = new CancellationTokenSource();
        var token = _stepLoadCts.Token;

        StepRows.Clear();
        if (value == null)
        {
            PreviewTitle = "Recettes";
            OnPropertyChanged(nameof(CanLoad));
            LoadGuideCommand.NotifyCanExecuteChanged();
            return;
        }

        PreviewTitle = $"{value.Guide.ListNameFr} — {value.Guide.Items.Count} recettes";
        StatusText = $"{value.Guide.ListNameFr} — chargement des icônes…";

        foreach (var step in _leveling.BuildStepPreviews(value.Guide))
            StepRows.Add(new CraftLevelingStepRow(step));

        OnPropertyChanged(nameof(CanLoad));
        LoadGuideCommand.NotifyCanExecuteChanged();
        _ = LoadStepDetailsAsync(token);
    }

    private async Task LoadStepDetailsAsync(CancellationToken token)
    {
        foreach (var row in StepRows.ToList())
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                if (row.ItemId > 0)
                {
                    var details = await _itemLookup.GetDetailsAsync(new WowItem { ItemId = row.ItemId }, token)
                        .ConfigureAwait(true);
                    if (token.IsCancellationRequested)
                        return;

                    if (details != null)
                    {
                        if (!string.IsNullOrWhiteSpace(details.Name))
                        {
                            row.DisplayName = details.Name;
                            row.IconItem.Name = details.Name;
                        }

                        row.ApplyQuality(details.Quality);
                    }
                }
                else if (row.SpellId > 0)
                {
                    var details = await _itemLookup.GetSpellDetailsAsync(row.SpellId, token)
                        .ConfigureAwait(true);
                    if (token.IsCancellationRequested)
                        return;

                    if (details != null && !string.IsNullOrWhiteSpace(details.Name))
                    {
                        row.DisplayName = details.Name;
                        row.IconItem.Name = details.Name;
                        row.ApplyQuality(details.Quality);
                    }
                }
            }
            catch
            {
                // garde le libellé catalogue
            }
        }

        if (!token.IsCancellationRequested && SelectedGuide != null)
            StatusText = $"{SelectedGuide.Guide.ListNameFr} — {StepRows.Count} recettes (WowIsClassic).";
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private void LoadGuide()
    {
        if (SelectedGuide == null)
            return;

        var list = _lists.GetById(_listId);
        if (list == null)
        {
            StatusText = "Liste introuvable.";
            return;
        }

        var items = list.EnsureItems();
        var added = 0;
        var updated = 0;

        foreach (var source in SelectedGuide.Guide.Items)
        {
            if (!CraftListItemKey.IsValid(source))
                continue;

            var exists = items.Any(i => CraftListItemKey.Matches(i, source));
            if (exists)
            {
                _lists.SetQuantity(_listId, source.ItemId, source.Quantity, source.SpellId);
                updated++;
            }
            else
            {
                _lists.AddItem(_listId, new CraftListItem
                {
                    ItemId = source.ItemId,
                    SpellId = source.SpellId,
                    Quantity = source.Quantity,
                    ProfessionId = source.ProfessionId
                });
                added++;
            }
        }

        _planning.NotifyListItemsChanged();
        StatusText = $"{SelectedGuide.Guide.ListNameFr} chargé — {added} ajouté(s), {updated} mis à jour.";
    }
}

public sealed class CraftLevelingGuideRow
{
    public CraftLevelingGuideRow(CraftLevelingGuideInfo guide)
    {
        Guide = guide;
        IconItem = new WowItem { Name = guide.ProfessionNameFr };
        if (CraftProfessionIcons.TryGetIconSpellId(guide.ProfessionId, out var spellId))
            IconItem.SpellId = spellId;
    }

    public CraftLevelingGuideInfo Guide { get; }
    public WowItem IconItem { get; }
    public string Title => Guide.ListNameFr;
    public string Subtitle => $"{Guide.Items.Count} recettes · {Guide.ProfessionNameFr}";
}

public sealed class CraftLevelingStepRow : ObservableObject
{
    public CraftLevelingStepRow(CraftLevelingStepPreview preview)
    {
        ItemId = preview.ItemId;
        SpellId = preview.SpellId;
        Quantity = preview.Quantity;
        IconItem = new WowItem
        {
            ItemId = preview.ItemId > 0 ? preview.ItemId : 0,
            SpellId = preview.ItemId > 0 ? 0 : preview.SpellId,
            Count = preview.Quantity
        };
        _displayName = preview.DisplayName;
    }

    public int ItemId { get; }
    public int SpellId { get; }
    public int Quantity { get; }
    public WowItem IconItem { get; }

    private string _displayName;
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string QuantityLabel => $"×{Quantity}";
    public string NameQualityColor => IconItem.QualityColor;

    public void ApplyQuality(int quality)
    {
        IconItem.Quality = quality;
        OnPropertyChanged(nameof(NameQualityColor));
    }
}
