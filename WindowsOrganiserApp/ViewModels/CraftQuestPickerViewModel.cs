using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.Craft;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.ViewModels;

public sealed partial class CraftQuestPickerViewModel : ObservableObject
{
    private readonly string _listId;
    private readonly ICraftListsService _lists;
    private readonly ICraftPlanningContext _planning;
    private readonly IWowItemLookupService _itemLookup;
    private CancellationTokenSource? _pieceLoadCts;

    public CraftQuestPickerViewModel(
        string listId,
        ICraftListsService lists,
        ICraftPlanningContext planning,
        IWowItemLookupService itemLookup)
    {
        _listId = listId;
        _lists = lists;
        _planning = planning;
        _itemLookup = itemLookup;

        foreach (var category in Tier3QuestCatalog.Categories)
            QuestCategories.Add(new QuestCategoryRow(category));

        SelectedQuestCategory = QuestCategories.FirstOrDefault();
    }

    public ObservableCollection<QuestCategoryRow> QuestCategories { get; } = [];

    [ObservableProperty]
    private QuestCategoryRow? _selectedQuestCategory;

    public ObservableCollection<QuestClassRow> Classes { get; } = [];

    [ObservableProperty]
    private QuestClassRow? _selectedClass;

    public ObservableCollection<QuestPieceRow> Pieces { get; } = [];

    [ObservableProperty]
    private string _statusText = "Choisissez une quête, puis ajoutez les objectifs souhaités.";

    partial void OnSelectedQuestCategoryChanged(QuestCategoryRow? value)
    {
        Classes.Clear();
        SelectedClass = null;
        Pieces.Clear();

        if (value == null)
            return;

        foreach (var cls in value.Definition.Classes)
            Classes.Add(new QuestClassRow(cls));

        SelectedClass = Classes.FirstOrDefault();
        StatusText = Tier3QuestCatalog.QuestCategoryHint(value.Definition.Id);
    }

    partial void OnSelectedClassChanged(QuestClassRow? value)
    {
        _pieceLoadCts?.Cancel();
        _pieceLoadCts = new CancellationTokenSource();
        var token = _pieceLoadCts.Token;

        Pieces.Clear();
        if (value == null || SelectedQuestCategory == null)
            return;

        var categoryId = SelectedQuestCategory.Definition.Id;
        foreach (var piece in value.Definition.Pieces)
            Pieces.Add(new QuestPieceRow(piece, value.Definition.Class, categoryId));

        _ = LoadPieceDetailsAsync(token);
    }

    private async Task LoadPieceDetailsAsync(CancellationToken token)
    {
        foreach (var row in Pieces.ToList())
        {
            if (token.IsCancellationRequested)
                return;

            if (row.Recipe.ResultItemId > 0)
            {
                try
                {
                    var details = await _itemLookup
                        .GetDetailsAsync(new WowItem { ItemId = row.Recipe.ResultItemId }, token)
                        .ConfigureAwait(true);
                    if (token.IsCancellationRequested)
                        return;

                    if (details != null)
                    {
                        if (!string.IsNullOrWhiteSpace(details.Name))
                        {
                            row.PieceName = details.Name;
                            row.IconItem.Name = details.Name;
                        }

                        row.ApplyPieceQuality(details.Quality);
                    }
                }
                catch
                {
                    // garde le libellé catalogue
                }
            }

            foreach (var chip in row.MaterialChips)
            {
                if (token.IsCancellationRequested)
                    return;

                try
                {
                    var details = await _itemLookup
                        .GetDetailsAsync(new WowItem { ItemId = chip.ItemId }, token)
                        .ConfigureAwait(true);
                    if (token.IsCancellationRequested)
                        return;

                    if (details != null && !string.IsNullOrWhiteSpace(details.Name))
                    {
                        chip.DisplayName = details.Name;
                        chip.Item.Name = details.Name;
                    }

                    if (details != null)
                        chip.ApplyQuality(details.Quality);
                }
                catch
                {
                    // garde le libellé catalogue
                }
            }
        }
    }

    [RelayCommand]
    private void AddPiece(QuestPieceRow? row)
    {
        if (row == null)
            return;

        var itemId = row.Recipe.ResultItemId;
        if (itemId <= 0)
        {
            StatusText = "Objet sans ID — impossible d'ajouter.";
            return;
        }

        var list = _lists.GetById(_listId);
        if (list?.EnsureItems().Any(i => CraftListItemKey.Matches(i, itemId, 0)) == true)
        {
            StatusText = $"{row.Recipe.PieceNameFr} — déjà dans la liste.";
            return;
        }

        var professionId = Tier3QuestCatalog.ProfessionIdForQuestPiece(
            row.QuestCategoryId, row.Class, row.Recipe.Slot, itemId);

        _lists.AddItem(_listId, new CraftListItem
        {
            ItemId = itemId,
            Quantity = 1,
            ProfessionId = professionId,
            SpellId = 0
        });

        StatusText = Tier3QuestCatalog.QuestAddStatusMessage(row.QuestCategoryId, row.Recipe.PieceNameFr);
        _planning.NotifyListItemsChanged();
    }
}

public sealed class QuestCategoryRow
{
    public QuestCategoryRow(QuestCategoryDefinition definition) => Definition = definition;
    public QuestCategoryDefinition Definition { get; }
    public string ShortTitle => Definition.ShortTitleFr;
    public string Title => Definition.TitleFr;
    public string Description => Definition.DescriptionFr;
}

public sealed class QuestClassRow
{
    public QuestClassRow(QuestClassSet definition)
    {
        Definition = definition;

        if (definition.Class is WowClass wowClass)
        {
            ClassName = Tier3QuestCatalog.GetClassNameFr(wowClass);
            ClassBrush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(WowClassColors.GetHexColor(wowClass)));
        }
        else
        {
            ClassName = definition.GroupTitleFr ?? definition.SetNameFr;
            ClassBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD4A017"));
        }

        ClassBrush.Freeze();
    }

    public QuestClassSet Definition { get; }
    public string ClassName { get; }
    public string SetName => Definition.SetNameFr;
    public string TitleLine => Definition.Class == null
        ? SetName
        : $"{ClassName} — {SetName}";
    public Brush ClassBrush { get; }
}

public sealed partial class QuestPieceRow : ObservableObject
{
    public QuestPieceRow(QuestPieceRecipe recipe, WowClass? wowClass, string questCategoryId)
    {
        Recipe = recipe;
        Class = wowClass;
        QuestCategoryId = questCategoryId;
        _pieceName = recipe.PieceNameFr;
        IconItem = new WowItem { ItemId = recipe.ResultItemId, Count = 1 };

        foreach (var mat in recipe.Materials)
            MaterialChips.Add(new QuestMaterialChip(mat.ItemId, mat.Quantity, mat.DisplayNameFr));
    }

    public QuestPieceRecipe Recipe { get; }
    public WowClass? Class { get; }
    public string QuestCategoryId { get; }
    public WowItem IconItem { get; }
    public ObservableCollection<QuestMaterialChip> MaterialChips { get; } = [];

    public string SlotLabel => Recipe.SlotLabelFr;

    private string _pieceName;
    public string PieceName
    {
        get => _pieceName;
        set => SetProperty(ref _pieceName, value);
    }

    public string TokenHint => Recipe.DesecratedTokenFr;
    public string MaterialsSummary => Recipe.MaterialsSummary;
    public string DisplayDescription => Recipe.DisplayDescription;
    public bool HasEffectDescription => !string.IsNullOrWhiteSpace(Recipe.EffectDescriptionFr);
    public bool HasMaterialChips => MaterialChips.Count > 0;
    public string PieceNameQualityColor => IconItem.QualityColor;

    public void ApplyPieceQuality(int quality)
    {
        IconItem.Quality = quality;
        OnPropertyChanged(nameof(PieceNameQualityColor));
    }
}

public sealed partial class QuestMaterialChip : ObservableObject
{
    public QuestMaterialChip(int itemId, int count, string displayName)
    {
        ItemId = itemId;
        Count = count;
        Item = new WowItem { ItemId = itemId, Count = count };
        _displayName = displayName;
    }

    public int ItemId { get; }
    public int Count { get; }
    public WowItem Item { get; }

    private string _displayName;
    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
                OnPropertyChanged(nameof(LabelLine));
        }
    }

    public string LabelLine => string.IsNullOrWhiteSpace(DisplayName)
        ? $"#{ItemId}"
        : $"{DisplayName} ×{Count}";

    public string NameQualityColor => Item.QualityColor;

    public void ApplyQuality(int quality)
    {
        Item.Quality = quality;
        OnPropertyChanged(nameof(NameQualityColor));
    }
}
