using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SpecialAzerothService.Core.Models.Craft;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;
using WindowsOrganiserApp.Views;

namespace WindowsOrganiserApp.ViewModels;

public sealed class CraftListRow : ObservableObject
{
    public const int MinQuantity = 0;
    public const int MaxQuantity = 9999;

    public CraftListItem Item { get; }
    public WowItem WowItem { get; }
    public string ProfessionId { get; }
    public string ProfessionLabel { get; }

    private string _displayName = "";
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    private int _quantity = 1;
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (value < MinQuantity) value = MinQuantity;
            if (value > MaxQuantity) value = MaxQuantity;
            if (!SetProperty(ref _quantity, value)) return;

            Item.Quantity = value;
            WowItem.Count = value;
            QuantityChanged?.Invoke();
        }
    }

    public event Action? QuantityChanged;

    private bool _isEditingQuantity;
    public bool IsEditingQuantity
    {
        get => _isEditingQuantity;
        set => SetProperty(ref _isEditingQuantity, value);
    }

    private string _editQuantityText = "";
    public string EditQuantityText
    {
        get => _editQuantityText;
        set => SetProperty(ref _editQuantityText, value);
    }

    public void BeginEditQuantity()
    {
        EditQuantityText = Quantity.ToString();
        IsEditingQuantity = true;
    }

    public void CommitEditQuantity()
    {
        if (!IsEditingQuantity) return;

        if (int.TryParse(EditQuantityText.Trim(), out var parsed))
            Quantity = parsed;

        IsEditingQuantity = false;
    }

    public void CancelEditQuantity() => IsEditingQuantity = false;

    public CraftListRow(CraftListItem item, string professionLabel)
    {
        Item = item;
        ProfessionId = item.ProfessionId ?? "";
        ProfessionLabel = professionLabel;
        WowItem = new WowItem
        {
            ItemId = item.ItemId,
            SpellId = item.ItemId > 0 ? 0 : item.SpellId,
            Count = item.Quantity
        };
        _quantity = Math.Clamp(item.Quantity, MinQuantity, MaxQuantity);
        if (item.Quantity < MinQuantity)
            item.Quantity = _quantity;
        _displayName = item.ItemId > 0 ? $"#{item.ItemId}" : item.SpellId > 0 ? $"Sort #{item.SpellId}" : "?";
    }

    public string NameQualityColor => WowItem.QualityColor;

    public void ApplyQuality(int quality)
    {
        WowItem.Quality = quality;
        OnPropertyChanged(nameof(NameQualityColor));
    }
}

public sealed class CraftListProfessionGroup : ObservableObject
{
    public string ProfessionLabel { get; }
    public ObservableCollection<CraftListRow> Items { get; } = [];

    public CraftListProfessionGroup(string professionLabel) => ProfessionLabel = professionLabel;
}

public sealed class CraftMaterialRow : ObservableObject
{
    public int ItemId { get; }
    /// <summary>Quantité encore à obtenir après utilisation du stock (décomposition intelligente).</summary>
    public int TotalNeeded { get; }
    public int GrossNeeded { get; }
    public int StockUsedInPlan => Math.Max(0, GrossNeeded - TotalNeeded);
    public bool HasStockUsedInPlan => StockUsedInPlan > 0;
    public WowItem WowItem { get; }
    public ObservableCollection<CraftStockCharacterHold> StockBreakdown { get; } = [];

    private int _ownedTotal;
    public int OwnedTotal
    {
        get => _ownedTotal;
        set
        {
            if (SetProperty(ref _ownedTotal, value))
            {
                OnPropertyChanged(nameof(StockSummary));
            }
        }
    }

    public int ToFarm => TotalNeeded;

    public string StockSummary
    {
        get
        {
            if (StockUsedInPlan > 0 && OwnedTotal > 0)
                return $"Stock {OwnedTotal} · utilisé {StockUsedInPlan} · farm {ToFarm}";
            if (OwnedTotal > 0)
                return $"Stock {OwnedTotal} · farm {ToFarm}";
            return ToFarm > 0 ? $"À farm {ToFarm}" : "OK";
        }
    }

    public string BreakdownText => StockBreakdown.Count == 0
        ? ""
        : string.Join(" · ", StockBreakdown.Select(b => b.Label));

    public bool HasStockBreakdown => StockBreakdown.Count > 0;

    private string _displayName = "";
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public CraftMaterialRow(int itemId, int netNeeded, int grossNeeded)
    {
        ItemId = itemId;
        TotalNeeded = netNeeded;
        GrossNeeded = grossNeeded;
        WowItem = new WowItem { ItemId = itemId, Count = netNeeded };
        _displayName = $"#{itemId}";
    }

    public string NameQualityColor => WowItem.QualityColor;

    public void ApplyQuality(int quality)
    {
        WowItem.Quality = quality;
        OnPropertyChanged(nameof(NameQualityColor));
    }

    private int _vendorUnitPriceCopper;
    public int VendorUnitPriceCopper
    {
        get => _vendorUnitPriceCopper;
        private set
        {
            if (!SetProperty(ref _vendorUnitPriceCopper, value)) return;
            OnPropertyChanged(nameof(VendorLineTotalCopper));
            OnPropertyChanged(nameof(HasVendorPrice));
        }
    }

    public long VendorLineTotalCopper => HasVendorPrice ? (long)VendorUnitPriceCopper * ToFarm : 0;

    public bool HasVendorPrice => VendorUnitPriceCopper > 0;

    public void SetVendorPricing(int unitPriceCopper) => VendorUnitPriceCopper = unitPriceCopper;
}

public sealed class CraftPickupRow : ObservableObject
{
    public int ItemId { get; }
    /// <summary>Quantité à prendre sur ce perso (icône).</summary>
    public int PickupQuantity { get; }
    /// <summary>Somme sac + banque + courrier sur ce perso.</summary>
    public int TotalOnCharacter { get; }
    public WowItem WowItem { get; }
    public string TotalForegroundHex => TotalOnCharacter < PickupQuantity ? "#FF7070" : "#C0A060";

    private string _displayName = "";
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public CraftPickupRow(int itemId, int pickupQuantity, int totalOnCharacter)
    {
        ItemId = itemId;
        PickupQuantity = pickupQuantity;
        TotalOnCharacter = totalOnCharacter;
        WowItem = new WowItem { ItemId = itemId, Count = pickupQuantity };
        _displayName = $"#{itemId}";
    }

    public string NameQualityColor => WowItem.QualityColor;

    public void ApplyQuality(int quality)
    {
        WowItem.Quality = quality;
        OnPropertyChanged(nameof(NameQualityColor));
    }
}

public sealed class CraftCharacterPickupGroup : ObservableObject
{
    public string CharacterName { get; }
    public string AccountName { get; }
    public string HeaderText { get; }
    public ObservableCollection<CraftPickupRow> Items { get; } = [];

    public int ItemCount => Items.Sum(i => i.PickupQuantity);

    public CraftCharacterPickupGroup(string characterName, string accountName)
    {
        CharacterName = characterName;
        AccountName = accountName;
        HeaderText = characterName;
        SubheaderText = string.IsNullOrEmpty(accountName) ? "" : accountName;
    }

    public string SubheaderText { get; }
    public bool HasSubheader => !string.IsNullOrEmpty(SubheaderText);
}

public sealed class BoundMaterialCharacterLineViewModel : ObservableObject
{
    public string CharacterName { get; }
    public string AccountName { get; }
    public int PickupQuantity { get; }
    public int TotalOnCharacter { get; }
    public long GoldCopper { get; }
    public string TotalForegroundHex => TotalOnCharacter < PickupQuantity ? "#FF7070" : "#C0A060";
    public WowItem? WowItem { get; }

    public string HeaderText => CharacterName;
    public string SubheaderText => AccountName;
    public bool HasSubheader => !string.IsNullOrWhiteSpace(AccountName);

    public BoundMaterialCharacterLineViewModel(BoundMaterialCharacterHold hold)
    {
        CharacterName = hold.CharacterName;
        AccountName = hold.AccountName;
        PickupQuantity = hold.PickupQuantity;
        TotalOnCharacter = hold.TotalOnCharacter;
        GoldCopper = hold.GoldCopper;
        WowItem = new WowItem { ItemId = 0, Count = hold.PickupQuantity };
    }

    public void SetItemId(int itemId) => WowItem!.ItemId = itemId;
}

public sealed class BoundMaterialNeedViewModel : ObservableObject
{
    public int ItemId { get; }
    public int RequiredCount { get; }
    public WowItem HeaderWowItem { get; }
    public ObservableCollection<BoundMaterialCharacterLineViewModel> Characters { get; } = [];
    public bool HasCharacters => Characters.Count > 0;
    public bool HasNoCharacters => Characters.Count == 0;

    private string _displayName = "";
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public BoundMaterialNeedViewModel(BoundMaterialNeed need)
    {
        ItemId = need.ItemId;
        RequiredCount = need.RequiredCount;
        _displayName = need.DisplayNameFr ?? $"#{need.ItemId}";
        HeaderWowItem = new WowItem { ItemId = need.ItemId, Count = need.RequiredCount };

        foreach (var hold in need.Characters)
        {
            var line = new BoundMaterialCharacterLineViewModel(hold);
            line.SetItemId(need.ItemId);
            Characters.Add(line);
        }
    }
}

public sealed class ArcanumCraftGroupViewModel : ObservableObject
{
    public int ResultItemId { get; }
    public int Quantity { get; }
    public int QuestGoldCostCopper { get; }
    public bool HasQuestGoldCost => QuestGoldCostCopper > 0;
    public ObservableCollection<BoundMaterialNeedViewModel> BoundMaterials { get; } = [];

    private string _displayName = "";
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string QuantityLabel => Quantity > 1 ? $"×{Quantity}" : "";
    public bool HasNoBoundHolders => BoundMaterials.All(b => b.HasNoCharacters);

    public ArcanumCraftGroupViewModel(ArcanumQuestAssignment assignment)
    {
        ResultItemId = assignment.Demand.ResultItemId;
        Quantity = assignment.Demand.Quantity;
        QuestGoldCostCopper = assignment.QuestGoldCostCopper;
        _displayName = $"#{ResultItemId}";

        foreach (var need in assignment.BoundNeeds)
            BoundMaterials.Add(new BoundMaterialNeedViewModel(need));
    }
}

public sealed class CraftListSummary : ObservableObject
{
    public CraftListDefinition Source { get; }
    public string Name => Source.Name;
    public int ItemCount => Source.EnsureItems().Count;
    public int TotalQuantity => Source.EnsureItems().Sum(i => i.Quantity);

    private bool _isRenaming;
    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetProperty(ref _isRenaming, value);
    }

    private string _editName = "";
    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public CraftListSummary(CraftListDefinition source) => Source = source;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(TotalQuantity));
    }
}

public sealed class CraftStockOwnerOption : ObservableObject
{
    private readonly Action _onSelectionChanged;

    public string UserId { get; }
    public string OwnerName { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value)) return;
            _onSelectionChanged();
        }
    }

    public CraftStockOwnerOption(string userId, string ownerName, bool isSelected, Action onSelectionChanged)
    {
        UserId = userId;
        OwnerName = ownerName;
        _isSelected = isSelected;
        _onSelectionChanged = onSelectionChanged;
    }
}

public partial class CraftCraftingViewModel : ObservableObject
{
    private readonly ICraftListsService _lists;
    private readonly ICraftLevelingCatalog _leveling;
    private readonly ICraftCatalogLookup _catalog;
    private readonly ICraftPickupPlanner _pickupPlanner;
    private readonly ICraftPlanningContext _planning;
    private readonly ICraftStockService _stock;
    private readonly IArcanumPlanningService _arcanumPlanner;
    private readonly IWowItemLookupService _itemLookup;
    private readonly CraftViewModel _professionsVm;
    private readonly CartoViewModel _cartoVm;
    private CancellationTokenSource? _computeCts;
    private bool _suppressListSelectionCallback;
    private bool _suppressStockAccountSelectionCallback;
    /// <summary>Après le 1er chargement ou toute action utilisateur, ne plus recocher « Moi » automatiquement.</summary>
    private bool _stockOwnerDefaultsApplied;

    public CraftCraftingViewModel(
        ICraftListsService lists,
        ICraftLevelingCatalog leveling,
        ICraftCatalogLookup catalog,
        ICraftPickupPlanner pickupPlanner,
        ICraftPlanningContext planning,
        ICraftStockService stock,
        IArcanumPlanningService arcanumPlanner,
        IWowItemLookupService itemLookup,
        CraftViewModel professionsVm,
        CartoViewModel cartoVm)
    {
        _lists = lists;
        _leveling = leveling;
        _catalog = catalog;
        _pickupPlanner = pickupPlanner;
        _planning = planning;
        _stock = stock;
        _arcanumPlanner = arcanumPlanner;
        _itemLookup = itemLookup;
        _professionsVm = professionsVm;
        _cartoVm = cartoVm;

        _planning.ListItemsChanged += OnExternalListItemsChanged;
        _cartoVm.PropertyChanged += OnCartoPropertyChanged;

        ReloadListSummaries();
        SelectedList = null;
    }

    private void OnCartoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == CartoViewModel.CharactersLoadedPropertyName && _cartoVm.CharactersLoaded)
            RefreshStockAccounts();
    }

    /// <summary>Précharge la liste des comptes au splash (après WowSync).</summary>
    public async Task WarmupStockAccountsAsync(CancellationToken cancellationToken = default)
    {
        var owners = await Task.Run(_stock.GetAvailableOwners, cancellationToken).ConfigureAwait(false);
        var dispatcher = Application.Current?.Dispatcher
                         ?? throw new InvalidOperationException("Application WPF non initialisée.");
        await dispatcher.InvokeAsync(() => SyncStockOwners(owners), DispatcherPriority.Background);
    }

    public void RefreshStockAccounts()
    {
        try
        {
            SyncStockOwners(_stock.GetAvailableOwners());
        }
        catch
        {
            // carto indisponible — laisser la sélection actuelle
        }
    }

    public ObservableCollection<CraftListSummary> ListSummaries { get; } = [];
    public ObservableCollection<CraftListProfessionGroup> ProfessionGroups { get; } = [];
    public ObservableCollection<CraftMaterialRow> MaterialRows { get; } = [];
    public ObservableCollection<CraftMaterialRow> ToFarmRows { get; } = [];
    public ObservableCollection<CraftMaterialRow> VendorBuyRows { get; } = [];
    public ObservableCollection<CraftCharacterPickupGroup> CharacterPickupGroups { get; } = [];
    public ObservableCollection<ArcanumCraftGroupViewModel> ArcanumCraftGroups { get; } = [];
    public ObservableCollection<CraftStockOwnerOption> StockAccounts { get; } = [];

    [ObservableProperty]
    private CraftListSummary? _selectedList;

    [ObservableProperty]
    private CraftListSummary? _renamingList;

    [ObservableProperty]
    private string _statusText = "Créez ou sélectionnez une liste de farm.";

    [ObservableProperty]
    private bool _isComputing;

    [ObservableProperty]
    private double _computeProgress;

    [ObservableProperty]
    private string _computeStatus = "";

    [ObservableProperty]
    private bool _forceCraftOutputs;

    public string SelectedStockAccountsText
    {
        get
        {
            var selected = StockAccounts
                .Where(a => a.IsSelected)
                .Select(a => a.OwnerName)
                .ToList();

            if (selected.Count == 0) return "Aucun stock";
            if (selected.Count <= 2) return string.Join(", ", selected);
            return $"{selected[0]}, {selected[1]} +{selected.Count - 2}";
        }
    }

    public bool CanDeleteList => SelectedList != null;

    public bool HasToFarmItems => ToFarmRows.Count > 0;
    public bool HasNoToFarmItems => ToFarmRows.Count == 0;
    public bool HasVendorBuyItems => VendorBuyRows.Count > 0;
    public bool HasNoVendorBuyItems => VendorBuyRows.Count == 0;
    public bool HasCharacterPickupGroups => CharacterPickupGroups.Count > 0;

    public long VendorBuyTotalCopper => VendorBuyRows.Sum(r => r.VendorLineTotalCopper);

    public bool HasVendorBuyTotal => VendorBuyTotalCopper > 0;
    public bool HasNoCharacterPickupGroups => CharacterPickupGroups.Count == 0;
    public bool HasArcanumCraftGroups => ArcanumCraftGroups.Count > 0;
    public bool HasNoArcanumCraftGroups => ArcanumCraftGroups.Count == 0;

    partial void OnForceCraftOutputsChanged(bool value) => RecomputeFromPlanningOptions();

    partial void OnSelectedListChanged(CraftListSummary? value)
    {
        if (_suppressListSelectionCallback) return;

        if (RenamingList != null && RenamingList != value)
            CommitRenameList();

        OnPropertyChanged(nameof(CanDeleteList));
        DeleteListCommand.NotifyCanExecuteChanged();

        _planning.SetActiveList(value?.Source.Id);
        ReloadListItems();
    }

    public void Refresh()
    {
        RefreshStockAccounts();
        ReloadListSummaries();
        ReloadListItems();
    }

    public bool TryAddItem(CraftListItem item)
    {
        if (SelectedList == null || !CraftListItemKey.IsValid(item)) return false;

        if (SelectedList.Source.EnsureItems().Any(i => CraftListItemKey.Matches(i, item)))
        {
            StatusText = $"Déjà dans « {SelectedList.Name} ».";
            return false;
        }

        if (item.Quantity <= 0)
            item.Quantity = 1;
        _lists.AddItem(SelectedList.Source.Id, item);
        ReloadListItems();
        _planning.NotifyListItemsChanged();
        StatusText = $"Ajouté à « {SelectedList.Name} » — règle la quantité avec ＋.";
        return true;
    }

    private void OnExternalListItemsChanged()
    {
        if (SelectedList == null) return;
        ReloadListSummaries();
        ReloadListItems();
    }

    [RelayCommand]
    private void CreateList()
    {
        var list = _lists.CreateList("");
        ReloadListSummaries();
        var summary = ListSummaries.FirstOrDefault(s => s.Source.Id == list.Id);
        SelectedList = summary;
        if (summary != null)
            BeginRenameList(summary);
        StatusText = "Nouvelle liste — saisissez le nom.";
    }

    public void BeginRenameList(CraftListSummary summary)
    {
        if (RenamingList != null && RenamingList != summary)
            EndRename();

        RenamingList = summary;
        summary.EditName = summary.Name;
        summary.IsRenaming = true;
    }

    public void CommitRenameList()
    {
        if (RenamingList == null) return;

        var id = RenamingList.Source.Id;
        var name = RenamingList.EditName.Trim();
        if (!string.IsNullOrEmpty(name))
            _lists.RenameList(id, name);

        EndRename();
        ReloadListSummaries();
        SelectedList = ListSummaries.FirstOrDefault(s => s.Source.Id == id);
        StatusText = SelectedList != null
            ? $"Liste « {SelectedList.Name} »."
            : "Liste renommée.";
    }

    public void CancelRenameList() => EndRename();

    private void EndRename()
    {
        if (RenamingList != null)
            RenamingList.IsRenaming = false;
        RenamingList = null;
    }

    [RelayCommand(CanExecute = nameof(CanDeleteList))]
    private void DeleteList()
    {
        if (SelectedList == null) return;

        var name = SelectedList.Name;
        var id = SelectedList.Source.Id;
        EndRename();
        _lists.DeleteList(id);
        ReloadListSummaries();
        SelectedList = ListSummaries.FirstOrDefault();
        OnPropertyChanged(nameof(CanDeleteList));
        DeleteListCommand.NotifyCanExecuteChanged();
        StatusText = ListSummaries.Count == 0
            ? "Aucune liste — créez-en une avec ＋."
            : $"Liste « {name} » supprimée.";
    }

    [RelayCommand]
    private void OpenProfessionPicker()
    {
        if (SelectedList == null)
        {
            StatusText = "Sélectionnez ou créez une liste avant d'ajouter des crafts.";
            return;
        }

        _planning.SetActiveList(SelectedList.Source.Id);

        var owner = Application.Current?.MainWindow;
        var picker = new CraftPickerWindow(_professionsVm);
        if (owner != null && owner.IsLoaded)
            picker.Owner = owner;

        picker.ShowDialog();
        ReloadListItems();
    }

    [RelayCommand]
    private void OpenQuestPicker()
    {
        if (SelectedList == null)
        {
            StatusText = "Sélectionnez ou créez une liste avant d'ajouter des quêtes T3.";
            return;
        }

        _planning.SetActiveList(SelectedList.Source.Id);

        var owner = Application.Current?.MainWindow;
        var vm = new CraftQuestPickerViewModel(SelectedList.Source.Id, _lists, _planning, _itemLookup);
        var picker = new CraftQuestPickerWindow(vm);
        if (owner != null && owner.IsLoaded)
            picker.Owner = owner;

        picker.ShowDialog();
        ReloadListItems();
        _planning.NotifyListItemsChanged();
    }

    [RelayCommand]
    private void OpenLevelingPicker()
    {
        if (SelectedList == null)
        {
            StatusText = "Sélectionnez ou créez une liste avant de charger un guide 1-300.";
            return;
        }

        _planning.SetActiveList(SelectedList.Source.Id);

        var owner = Application.Current?.MainWindow;
        var vm = new CraftLevelingPickerViewModel(
            SelectedList.Source.Id, _lists, _leveling, _planning, _itemLookup);
        var picker = new CraftLevelingPickerWindow(vm);
        if (owner != null && owner.IsLoaded)
            picker.Owner = owner;

        picker.ShowDialog();
        ReloadListItems();
        _planning.NotifyListItemsChanged();
    }

    [RelayCommand]
    private void RemoveItem(CraftListRow? row)
    {
        if (SelectedList == null || row == null) return;

        _lists.RemoveItem(SelectedList.Source.Id, row.Item.ItemId, row.Item.SpellId);
        ReloadListItems();
        _planning.NotifyListItemsChanged();
    }

    [RelayCommand]
    private void IncrementItem(CraftListRow? row)
    {
        if (row == null) return;
        if (row.Quantity < CraftListRow.MaxQuantity)
            row.Quantity++;
    }

    [RelayCommand]
    private void DecrementItem(CraftListRow? row)
    {
        if (row == null) return;
        if (row.Quantity > 0)
            row.Quantity--;
    }

    [RelayCommand]
    private void ImportList()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Importer une liste de farm",
            Filter = "Liste Craft JSON|*.json"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var imported = _lists.ImportList(dlg.FileName);
            ReloadListSummaries();
            SelectedList = ListSummaries.FirstOrDefault(s => s.Source.Id == imported.Id);
            StatusText = $"Liste « {imported.Name} » importée.";
        }
        catch (Exception ex)
        {
            StatusText = $"Import impossible : {ex.Message}";
        }
    }

    private void ReloadListSummaries()
    {
        var selectedId = SelectedList?.Source.Id ?? _planning.ActiveListId;
        ListSummaries.Clear();
        foreach (var list in _lists.Lists.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
            ListSummaries.Add(new CraftListSummary(list));

        _suppressListSelectionCallback = true;
        SelectedList = selectedId != null
            ? ListSummaries.FirstOrDefault(s => s.Source.Id == selectedId)
            : ListSummaries.FirstOrDefault();
        _suppressListSelectionCallback = false;

        if (SelectedList != null)
            _planning.SetActiveList(SelectedList.Source.Id);

        OnPropertyChanged(nameof(CanDeleteList));
        DeleteListCommand.NotifyCanExecuteChanged();
    }

    private void ReloadListItems()
    {
        CancelCompute();

        foreach (var group in ProfessionGroups)
        {
            foreach (var row in group.Items)
                row.QuantityChanged -= OnRowQuantityChanged;
        }

        ProfessionGroups.Clear();
        MaterialRows.Clear();

        if (SelectedList == null)
        {
            RefreshStockAccounts();
            FinishCompute();
            StatusText = ListSummaries.Count == 0
                ? "Créez une liste avec ＋, puis cliquez son nom pour la renommer."
                : "Sélectionnez une liste.";
            return;
        }

        try
        {
        var rows = SelectedList.Source.EnsureItems()
            .Select(item =>
            {
                var profLabel = ResolveProfessionLabel(item.ProfessionId);
                var row = new CraftListRow(item, profLabel);
                row.DisplayName = _catalog.GetRecipeDisplayName(item.ItemId, item.SpellId);
                return row;
            })
            .OrderBy(r => r.ProfessionLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Item.ItemId)
            .ToList();

        foreach (var row in rows)
            row.QuantityChanged += OnRowQuantityChanged;

        foreach (var grp in rows.GroupBy(r => r.ProfessionLabel, StringComparer.OrdinalIgnoreCase))
        {
            var group = new CraftListProfessionGroup(grp.Key);
            foreach (var row in grp.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase))
                group.Items.Add(row);
            ProfessionGroups.Add(group);
        }

        BeginCompute("Plan des matériaux…");
        ReloadMaterials();

        var total = rows.Count;
        StatusText = total == 0
            ? $"« {SelectedList.Name} » — vide. ＋ métiers ou 📜 quêtes T3."
            : $"« {SelectedList.Name} » — {total} objet(s) — calcul en cours…";

        StartEnrich(includeCrafts: true);
        }
        catch (Exception ex)
        {
            StatusText = $"Erreur liste : {ex.Message}";
        }
    }

    private string ResolveProfessionLabel(string? professionId)
    {
        if (string.IsNullOrEmpty(professionId))
            return "Divers";

        if (Tier3QuestCatalog.TryParseEkoProfessionId(professionId, out var ekoItemId))
            return Tier3QuestCatalog.ProfessionLabelEko(ekoItemId);

        if (Tier3QuestCatalog.TryParseBlProfessionId(professionId, out var blItemId))
            return Tier3QuestCatalog.ProfessionLabelBl(blItemId);

        if (Tier3QuestCatalog.TryParseArcanumProfessionId(professionId, out var arcanumItemId))
            return Tier3QuestCatalog.ProfessionLabelArcanum(arcanumItemId);

        if (Tier3QuestCatalog.TryParseArgentDawnProfessionId(professionId, out var argentDawnItemId))
            return Tier3QuestCatalog.ProfessionLabelArgentDawn(argentDawnItemId);

        if (Tier3QuestCatalog.TryParseProfessionId(professionId, out var t3Class, out var t3Slot))
            return Tier3QuestCatalog.ProfessionLabel(t3Class, t3Slot);

        var label = _catalog.GetProfessionLabel(professionId);
        return CraftDisplayNames.ProfessionGroupFr(professionId, label);
    }

    private void ReloadMaterials()
    {
        MaterialRows.Clear();
        ToFarmRows.Clear();
        VendorBuyRows.Clear();
        CharacterPickupGroups.Clear();
        ArcanumCraftGroups.Clear();
        if (SelectedList == null) return;

        try
        {
            var rows = ProfessionGroups
                .SelectMany(g => g.Items)
                .Where(r => r.Quantity > 0)
                .ToList();

            var outputs = rows
                .Select(r => (r.Item.ItemId, r.Item.SpellId, r.Quantity))
                .ToList();

            var arcanumDemands = BuildArcanumDemands(rows);
            var excludeFromMaterialRows = CollectArcanumBoundItemIds(arcanumDemands);

            RefreshStockAccounts();
            var selectedUserIds = StockAccounts
                .Where(a => a.IsSelected)
                .Select(a => a.UserId)
                .ToList();
            var stock = _stock.ReadStockForOwners(selectedUserIds);
            var plan = _pickupPlanner.Plan(outputs, stock, new CraftPlanningOptions
            {
                ForceCraftOutputs = ForceCraftOutputs,
                UseMuleStockForComponents = StockAccounts.Any(a => a.IsSelected)
            });

            var pickupIndex = IndexPickupQuantities(plan.Pickups);
            BuildCharacterPickupGroups(pickupIndex, stock);
            BuildArcanumCraftGroups(arcanumDemands, stock);

            foreach (var (itemId, req) in plan.Materials.OrderBy(kv => kv.Key))
            {
                if (req.GrossNeeded <= 0 && req.NetNeeded <= 0) continue;
                if (excludeFromMaterialRows.Contains(itemId)) continue;

                var row = new CraftMaterialRow(itemId, req.NetNeeded, req.GrossNeeded)
                {
                    OwnedTotal = stock.GetTotal(itemId)
                };

                foreach (var hold in stock.GetBreakdown(itemId).OrderBy(h => h.CharacterName))
                    row.StockBreakdown.Add(hold);

                MaterialRows.Add(row);
            }

            NotifyMissingCollectionsChanged();
        }
        catch (Exception ex)
        {
            StatusText = $"Erreur matériaux : {ex.Message}";
            FinishCompute();
        }
    }

    private static List<ArcanumQuestDemand> BuildArcanumDemands(IReadOnlyList<CraftListRow> rows)
    {
        var demands = new List<ArcanumQuestDemand>();
        foreach (var row in rows)
        {
            if (!row.ProfessionId.StartsWith(Tier3QuestCatalog.ProfessionIdPrefixArcanum, StringComparison.OrdinalIgnoreCase))
                continue;
            if (row.Item.ItemId <= 0) continue;

            demands.Add(new ArcanumQuestDemand
            {
                ResultItemId = row.Item.ItemId,
                Quantity = row.Quantity
            });
        }

        return demands;
    }

    private static HashSet<int> CollectArcanumBoundItemIds(IReadOnlyList<ArcanumQuestDemand> demands)
    {
        var ids = new HashSet<int>();
        foreach (var demand in demands)
        {
            if (!Tier3QuestCatalog.TryFindPieceByResultItemId(demand.ResultItemId, out var recipe, out _))
                continue;

            foreach (var mat in QuestBoundMaterialHelper.GetBoundMaterials(recipe!))
                ids.Add(mat.ItemId);
        }

        return ids;
    }

    private void BuildArcanumCraftGroups(IReadOnlyList<ArcanumQuestDemand> demands, CraftStockSnapshot stock)
    {
        if (demands.Count == 0) return;

        var characters = stock.Characters.Select(c => c.ToArcanumStock()).ToList();
        var result = _arcanumPlanner.Plan(demands, characters);

        foreach (var assignment in result.Assignments)
        {
            if (!assignment.HasBoundMaterials) continue;
            if (!string.IsNullOrEmpty(assignment.ErrorMessage)) continue;

            ArcanumCraftGroups.Add(new ArcanumCraftGroupViewModel(assignment));
        }
    }

    private static Dictionary<(string Account, string Character, int ItemId), int> IndexPickupQuantities(
        IReadOnlyList<CraftPickupLine> pickups)
    {
        var pickupTotals = new Dictionary<(string Account, string Character, int ItemId), int>(
            new AccountCharacterItemComparer());

        foreach (var line in pickups)
        {
            var itemKey = (line.AccountName, line.CharacterName, line.ItemId);
            pickupTotals[itemKey] = pickupTotals.GetValueOrDefault(itemKey) + line.Quantity;
        }

        return pickupTotals;
    }

    private void CancelCompute()
    {
        _computeCts?.Cancel();
        _computeCts?.Dispose();
        _computeCts = null;
    }

    private void RecomputeFromPlanningOptions()
    {
        if (SelectedList == null) return;
        ReloadMaterials();
        StartEnrich(includeCrafts: false);
    }

    private void SyncStockOwners(IReadOnlyList<CraftStockOwnerInfo> owners)
    {
        var selectedUserIds = StockAccounts
            .Where(a => a.IsSelected)
            .Select(a => a.UserId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _suppressStockAccountSelectionCallback = true;
        StockAccounts.Clear();
        foreach (var owner in owners)
        {
            var isSelected = selectedUserIds.Count > 0
                ? selectedUserIds.Contains(owner.UserId)
                : !_stockOwnerDefaultsApplied
                  && owner.OwnerName.Equals(CartoUserMigration.DefaultUserName, StringComparison.OrdinalIgnoreCase);
            StockAccounts.Add(new CraftStockOwnerOption(
                owner.UserId, owner.OwnerName, isSelected, OnStockAccountSelectionChanged));
        }
        _suppressStockAccountSelectionCallback = false;

        if (!_stockOwnerDefaultsApplied && StockAccounts.Any(a => a.IsSelected))
            _stockOwnerDefaultsApplied = true;

        OnPropertyChanged(nameof(SelectedStockAccountsText));
    }

    private void OnStockAccountSelectionChanged()
    {
        if (_suppressStockAccountSelectionCallback) return;
        _stockOwnerDefaultsApplied = true;
        OnPropertyChanged(nameof(SelectedStockAccountsText));
        RecomputeFromPlanningOptions();
    }

    private void StartEnrich(bool includeCrafts)
    {
        CancelCompute();
        _computeCts = new CancellationTokenSource();
        var token = _computeCts.Token;
        _ = EnrichListDataAsync(includeCrafts, token);
    }

    private void BeginCompute(string label)
    {
        IsComputing = true;
        ComputeProgress = 0;
        ComputeStatus = label;
    }

    private void ReportCompute(int done, int total, string phase)
    {
        ComputeProgress = total > 0 ? done * 100.0 / total : 100;
        ComputeStatus = total > 0 ? $"{phase} ({done}/{total})" : phase;
    }

    private void FinishCompute()
    {
        IsComputing = false;
        ComputeProgress = 0;
        ComputeStatus = "";
    }

    private void UpdateSummaryStatus()
    {
        if (SelectedList == null) return;

        var total = ProfessionGroups.Sum(g => g.Items.Count);
        StatusText = total == 0
            ? $"« {SelectedList.Name} » — vide. ＋ métiers ou 📜 quêtes T3."
            : $"« {SelectedList.Name} » — {total} objet(s), {ArcanumCraftGroups.Count} arcanum(s) lié(s), {CharacterPickupGroups.Count} perso(s), {ToFarmRows.Count} à farmer, {VendorBuyRows.Count} chez marchand.";
    }

    private async Task EnrichListDataAsync(bool includeCrafts, CancellationToken token)
    {
        var craftRows = includeCrafts
            ? ProfessionGroups.SelectMany(g => g.Items).ToList()
            : [];
        var materialRows = MaterialRows.ToList();
        var pickupRows = CharacterPickupGroups.SelectMany(g => g.Items).ToList();
        var arcanumGroups = ArcanumCraftGroups.ToList();
        var arcanumBoundRows = arcanumGroups
            .SelectMany(g => g.BoundMaterials)
            .ToList();
        var classifyCandidates = materialRows.Where(r => r.ToFarm > 0).ToList();

        var total = craftRows.Count + materialRows.Count + pickupRows.Count
                    + arcanumGroups.Count + arcanumBoundRows.Count
                    + classifyCandidates.Count;
        if (total == 0)
        {
            FinishCompute();
            UpdateSummaryStatus();
            return;
        }

        BeginCompute("Calcul des besoins…");
        var done = 0;

        try
        {
            foreach (var row in craftRows)
            {
                if (token.IsCancellationRequested) return;

                ReportCompute(done, total, "Objets à crafter");

                if (row.Item.ItemId > 0)
                {
                    await LoadItemDetailsAsync(row.Item.ItemId, (n, quality) =>
                    {
                        if (!string.IsNullOrWhiteSpace(n))
                        {
                            row.DisplayName = n;
                            row.WowItem.Name = n;
                        }

                        if (quality.HasValue)
                            row.ApplyQuality(quality.Value);
                    }).ConfigureAwait(true);
                }
                else if (row.Item.SpellId > 0)
                {
                    await LoadSpellNameAsync(row.Item.SpellId, n =>
                    {
                        if (!string.IsNullOrWhiteSpace(n))
                        {
                            row.DisplayName = n;
                            row.WowItem.Name = n;
                        }
                    }).ConfigureAwait(true);
                }

                done++;
                ReportCompute(done, total, "Objets à crafter");
            }

            foreach (var row in materialRows)
            {
                if (token.IsCancellationRequested) return;

                ReportCompute(done, total, "Matériaux");

                row.DisplayName = _catalog.GetItemDisplayName(row.ItemId);
                await LoadItemDetailsAsync(row.ItemId, (name, quality) =>
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        row.DisplayName = name;
                        row.WowItem.Name = name;
                    }

                    if (quality.HasValue)
                        row.ApplyQuality(quality.Value);
                }).ConfigureAwait(true);

                done++;
                ReportCompute(done, total, "Matériaux");
            }

            foreach (var group in arcanumGroups)
            {
                if (token.IsCancellationRequested) return;

                ReportCompute(done, total, "Composants liés");

                group.DisplayName = _catalog.GetItemDisplayName(group.ResultItemId);
                await LoadItemDetailsAsync(group.ResultItemId, (name, _) =>
                {
                    if (!string.IsNullOrWhiteSpace(name))
                        group.DisplayName = name;
                }).ConfigureAwait(true);

                done++;
                ReportCompute(done, total, "Composants liés");

                foreach (var bound in group.BoundMaterials)
                {
                    if (token.IsCancellationRequested) return;

                    ReportCompute(done, total, "Composants liés");

                    bound.DisplayName = _catalog.GetItemDisplayName(bound.ItemId);
                    await LoadItemDetailsAsync(bound.ItemId, (name, quality) =>
                    {
                        if (!string.IsNullOrWhiteSpace(name))
                            bound.DisplayName = name;
                        if (!quality.HasValue) return;

                        bound.HeaderWowItem.Quality = quality.Value;
                        foreach (var line in bound.Characters)
                        {
                            if (line.WowItem == null) continue;
                            line.WowItem.Quality = quality.Value;
                            if (!string.IsNullOrWhiteSpace(name))
                                line.WowItem.Name = name;
                        }
                    }).ConfigureAwait(true);

                    done++;
                    ReportCompute(done, total, "Composants liés");
                }
            }

            foreach (var row in pickupRows)
            {
                if (token.IsCancellationRequested) return;

                ReportCompute(done, total, "Stock par perso");

                row.DisplayName = _catalog.GetItemDisplayName(row.ItemId);
                await LoadItemDetailsAsync(row.ItemId, (name, quality) =>
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        row.DisplayName = name;
                        row.WowItem.Name = name;
                    }

                    if (quality.HasValue)
                        row.ApplyQuality(quality.Value);
                }).ConfigureAwait(true);

                done++;
                ReportCompute(done, total, "Stock par perso");
            }

            ToFarmRows.Clear();
            VendorBuyRows.Clear();

            foreach (var row in classifyCandidates)
            {
                if (token.IsCancellationRequested) return;

                ReportCompute(done, total, "Marchands");

                var vendorInfo = await _itemLookup
                    .GetVendorPurchaseInfoAsync(row.ItemId, token)
                    .ConfigureAwait(true);

                if (token.IsCancellationRequested) return;

                if (vendorInfo.IsInfiniteStock)
                {
                    row.SetVendorPricing(vendorInfo.UnitPriceCopper);
                    VendorBuyRows.Add(row);
                }
                else
                {
                    ToFarmRows.Add(row);
                }

                done++;
                ReportCompute(done, total, "Marchands");
            }

            NotifyMissingCollectionsChanged();
            RefreshVendorBuyTotals();
            ComputeProgress = 100;
            ComputeStatus = "Terminé";
            UpdateSummaryStatus();
        }
        catch (Exception ex)
        {
            StatusText = $"Erreur calcul : {ex.Message}";
        }
        finally
        {
            if (!token.IsCancellationRequested)
                FinishCompute();
        }
    }

    private void BuildCharacterPickupGroups(
        IReadOnlyDictionary<(string Account, string Character, int ItemId), int> pickupTotals,
        CraftStockSnapshot stock)
    {
        var groups = new Dictionary<(string Account, string Character), CraftCharacterPickupGroup>(
            new AccountCharacterComparer());

        foreach (var ((accountName, characterName, itemId), pickupQty) in pickupTotals)
        {
            var groupKey = (accountName, characterName);
            if (!groups.TryGetValue(groupKey, out var group))
            {
                group = new CraftCharacterPickupGroup(characterName, accountName);
                groups[groupKey] = group;
            }

            var charStock = stock.FindCharacter(accountName, characterName);
            var totalOnChar = charStock?.GetTotalOnCharacter(itemId) ?? 0;
            group.Items.Add(new CraftPickupRow(itemId, pickupQty, totalOnChar));
        }

        foreach (var group in groups.Values
                     .Where(g => g.Items.Count > 0)
                     .OrderBy(g => g.CharacterName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(g => g.AccountName, StringComparer.OrdinalIgnoreCase))
        {
            var sorted = group.Items
                .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ItemId)
                .ToList();
            group.Items.Clear();
            foreach (var row in sorted)
                group.Items.Add(row);

            CharacterPickupGroups.Add(group);
        }
    }

    private sealed class AccountCharacterComparer : IEqualityComparer<(string Account, string Character)>
    {
        public bool Equals((string Account, string Character) x, (string Account, string Character) y) =>
            x.Account.Equals(y.Account, StringComparison.OrdinalIgnoreCase)
            && x.Character.Equals(y.Character, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Account, string Character) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Account),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Character));
    }

    private sealed class AccountCharacterItemComparer : IEqualityComparer<(string Account, string Character, int ItemId)>
    {
        public bool Equals(
            (string Account, string Character, int ItemId) x,
            (string Account, string Character, int ItemId) y) =>
            x.ItemId == y.ItemId
            && x.Account.Equals(y.Account, StringComparison.OrdinalIgnoreCase)
            && x.Character.Equals(y.Character, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Account, string Character, int ItemId) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Account),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Character),
                obj.ItemId);
    }

    private void OnRowQuantityChanged()
    {
        if (SelectedList == null) return;

        try
        {
            foreach (var group in ProfessionGroups)
            {
                foreach (var row in group.Items)
                    _lists.SetQuantity(SelectedList.Source.Id, row.Item.ItemId, row.Quantity, row.Item.SpellId);
            }

            SelectedList.Refresh();
            ReloadMaterials();
            StartEnrich(includeCrafts: false);
        }
        catch (Exception ex)
        {
            StatusText = $"Erreur quantité : {ex.Message}";
            FinishCompute();
        }
    }

    private void NotifyMissingCollectionsChanged()
    {
        OnPropertyChanged(nameof(HasToFarmItems));
        OnPropertyChanged(nameof(HasNoToFarmItems));
        OnPropertyChanged(nameof(HasVendorBuyItems));
        OnPropertyChanged(nameof(HasNoVendorBuyItems));
        OnPropertyChanged(nameof(HasCharacterPickupGroups));
        OnPropertyChanged(nameof(HasNoCharacterPickupGroups));
        OnPropertyChanged(nameof(HasArcanumCraftGroups));
        OnPropertyChanged(nameof(HasNoArcanumCraftGroups));
        RefreshVendorBuyTotals();
    }

    private void RefreshVendorBuyTotals()
    {
        OnPropertyChanged(nameof(VendorBuyTotalCopper));
        OnPropertyChanged(nameof(HasVendorBuyTotal));
    }

    private async Task LoadItemDetailsAsync(int itemId, Action<string, int?> apply)
    {
        try
        {
            var details = await _itemLookup.GetDetailsAsync(new WowItem { ItemId = itemId }).ConfigureAwait(true);
            if (details == null) return;

            apply(details.Name, details.Quality);
        }
        catch
        {
            // garde le libellé de secours
        }
    }

    private async Task LoadItemNameAsync(int itemId, Action<string> setName)
    {
        await LoadItemDetailsAsync(itemId, (name, _) =>
        {
            if (!string.IsNullOrWhiteSpace(name))
                setName(name);
        }).ConfigureAwait(true);
    }

    private async Task LoadSpellNameAsync(int spellId, Action<string> setName)
    {
        try
        {
            var details = await _itemLookup.GetSpellDetailsAsync(spellId).ConfigureAwait(true);
            if (details != null && !string.IsNullOrWhiteSpace(details.Name))
                setName(details.Name);
        }
        catch
        {
            // garde le libellé de secours
        }
    }
}
