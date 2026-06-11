using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SpecialAzerothService.Core.Models.Craft;
using SpecialAzerothService.Core.Models.Reputation;
using SpecialAzerothService.Core.Services;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.ViewModels;

public sealed partial class ReputationViewModel : ObservableObject
{
    private readonly IReputationCalculatorService _calculator;
    private readonly ICraftStockService _stock;
    private readonly CartoViewModel _cartoVm;
    private bool _suppressStockAccountSelectionCallback;
    private bool _suppressFarmOptionSync;
    private bool _stockOwnerDefaultsApplied;

    public ReputationViewModel(
        IReputationCalculatorService calculator,
        ICraftStockService stock,
        CartoViewModel cartoVm)
    {
        _calculator = calculator;
        _stock = stock;
        _cartoVm = cartoVm;

        foreach (var farm in ReputationTurnInCatalog.All)
        {
            if (farm == null) continue;
            Farms.Add(new ReputationFarmRow(farm));
        }

        SelectedFarm = Farms.FirstOrDefault(f => f.IsImplemented)
                       ?? Farms.FirstOrDefault();
        SyncFarmOptions();
        _cartoVm.PropertyChanged += OnCartoPropertyChanged;
        RefreshStockAccounts();
        Recalculate();
    }

    public ObservableCollection<ReputationFarmRow> Farms { get; } = [];
    public ObservableCollection<CraftStockOwnerOption> StockAccounts { get; } = [];
    public ObservableCollection<ReputationCharacterStockGroup> CharacterStockGroups { get; } = [];
    public ObservableCollection<ReputationItemRow> AcceptedItems { get; } = [];
    public ObservableCollection<ReputationTierRow> TierOptions { get; } = [];
    public ObservableCollection<ReputationRouteRow> RouteOptions { get; } = [];
    public ObservableCollection<ReputationItemNeedRow> ItemsNeededBreakdown { get; } = [];
    public ObservableCollection<ReputationTierMaterialGroupRow> TierMaterialGroups { get; } = [];

    [ObservableProperty]
    private ReputationFarmRow? _selectedFarm;

    [ObservableProperty]
    private string _targetReputationText = "1000";

    [ObservableProperty]
    private bool _useBijoux = true;

    [ObservableProperty]
    private bool _useCoins;

    [ObservableProperty]
    private int _itemsNeeded;

    [ObservableProperty]
    private int _reputationGained;

    [ObservableProperty]
    private int _reputationPerTurnIn;

    [ObservableProperty]
    private string _summaryText = "";

    [ObservableProperty]
    private string _methodDetailText = "";

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string _statusText = "Choisissez une réputation, un mode d'échange et un objectif.";

    [ObservableProperty]
    private int _totalStockFound;

    [ObservableProperty]
    private int _totalStockReputation;

    [ObservableProperty]
    private int _stockShortfall;

    [ObservableProperty]
    private string _stockSummaryText = "";

    [ObservableProperty]
    private int _stockCharacterCount;

    [ObservableProperty]
    private ReputationTierRow? _selectedTier;

    [ObservableProperty]
    private string? _selectedVariantRouteId;

    [ObservableProperty]
    private int _totalStockTurnIns;

    [ObservableProperty]
    private string _zandalarCoinSellGoldText = "0";

    [ObservableProperty]
    private string _zandalarCoinSellSilverText = "0";

    [ObservableProperty]
    private string _zandalarCoinSellCopperText = "0";

    public string StockItemUnitLabel => GetActiveRoute()?.ItemUnitLabelFr ?? "objets";

    public bool ShowZandalarExchangeMode => SelectedFarm?.Definition.Id == "ZandalarTribe";

    public long ZandalarCoinSellPriceCopper =>
        ParseWowCurrencyText(ZandalarCoinSellGoldText, ZandalarCoinSellSilverText, ZandalarCoinSellCopperText);

    public bool ShowZandalarGoldEstimates =>
        ShowZandalarExchangeMode && UseCoins && ZandalarCoinSellPriceCopper > 0;

    public bool ShowZandalarItemsNeededGold => ShowZandalarGoldEstimates && HasResult && ItemsNeeded > 0;

    public bool ShowZandalarGoldSummaryRow => ShowZandalarGoldEstimates;

    public long TotalStockGoldCopper =>
        ShowZandalarGoldEstimates ? TotalStockFound * ZandalarCoinSellPriceCopper : 0;

    public long ItemsNeededGoldCopper =>
        ShowZandalarGoldEstimates && HasResult ? (long)ItemsNeeded * ZandalarCoinSellPriceCopper : 0;

    public long StockShortfallGoldCopper =>
        ShowZandalarGoldEstimates && HasStockShortfall ? (long)StockShortfall * ZandalarCoinSellPriceCopper : 0;

    public bool UsesTierSelection => SelectedFarm?.Definition.UsesTierSelection == true;

    public bool ShowTierPicker => UsesTierSelection;

    public bool ShowTierMaterials => UsesTierSelection && TierMaterialGroups.Count > 0;

    public bool ShowStandardCalculator => !UsesTierSelection;

    public bool ShowRoutePicker => !UsesTierSelection && RouteOptions.Count > 1 && !ShowZandalarExchangeMode;

    public bool ShowSingleItemsNeeded => ShowStandardCalculator && ItemsNeededBreakdown.Count <= 1;

    public bool ShowItemsNeededBreakdown => ShowStandardCalculator && ItemsNeededBreakdown.Count > 1;

    public bool ShowStandardObjectsPanel => ShowStandardCalculator;

    public string ItemsNeededLabel
    {
        get
        {
            var unit = StockItemUnitLabel;
            if (string.IsNullOrWhiteSpace(unit)) return "Objets nécessaires";
            return char.ToUpper(unit[0]) + unit[1..] + " nécessaires";
        }
    }

    public string StockTotalItemsCaption
    {
        get
        {
            if (UsesFixedRequirements) return "Remises (total)";
            var unit = StockItemUnitLabel;
            if (string.IsNullOrWhiteSpace(unit)) return "Objets (total)";
            return char.ToUpper(unit[0]) + unit[1..] + " (total)";
        }
    }

    public int TotalStockDisplay => UsesFixedRequirements ? TotalStockTurnIns : TotalStockFound;

    public string AcceptedItemsHint => GetSelectedTier()?.DescriptionFr
                                       ?? GetActiveRoute()?.DescriptionFr
                                       ?? "";

    public bool HasStockTotals => StockCharacterCount > 0;

    public bool UsesFixedRequirements =>
        UsesTierSelection || GetActiveRoute()?.UsesFixedRequirements == true;

    public string SelectedStockAccountsText
    {
        get
        {
            var selected = StockAccounts
                .Where(a => a.IsSelected)
                .Select(a => a.OwnerName)
                .ToList();

            if (selected.Count == 0) return "Aucun compte";
            if (selected.Count <= 2) return string.Join(", ", selected);
            return $"{selected[0]}, {selected[1]} +{selected.Count - 2}";
        }
    }

    public bool HasCharacterStockGroups => CharacterStockGroups.Count > 0;
    public bool HasNoCharacterStockGroups => CharacterStockGroups.Count == 0;
    public bool HasStockShortfall => HasResult && StockShortfall > 0;

    public bool IsFarmImplemented => SelectedFarm?.Definition.IsImplemented == true;

    public bool IsFarmPending => SelectedFarm != null && !IsFarmImplemented;

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
            // carto indisponible
        }
    }

    partial void OnSelectedFarmChanged(ReputationFarmRow? value)
    {
        if (value?.Definition.Id == "ZandalarTribe")
            ApplyZandalarDefaults();

        OnPropertyChanged(nameof(IsFarmImplemented));
        OnPropertyChanged(nameof(IsFarmPending));
        OnPropertyChanged(nameof(ShowZandalarExchangeMode));
        NotifyZandalarGoldEstimates();
        OnPropertyChanged(nameof(UsesTierSelection));
        OnPropertyChanged(nameof(ShowTierPicker));
        OnPropertyChanged(nameof(ShowRoutePicker));
        OnPropertyChanged(nameof(ShowStandardCalculator));
        OnPropertyChanged(nameof(ShowStandardObjectsPanel));
        SyncFarmOptions();
        NotifyRouteLabels();
        RefreshMethodDetail();
        Recalculate();
    }

    partial void OnSelectedTierChanged(ReputationTierRow? value)
    {
        if (_suppressFarmOptionSync) return;

        if (value != null)
            TargetReputationText = value.Tier.ReputationNeeded.ToString();

        NotifyRouteLabels();
        RefreshMethodDetail();
        Recalculate();
    }

    partial void OnSelectedVariantRouteIdChanged(string? value)
    {
        if (_suppressFarmOptionSync || UsesTierSelection) return;

        NotifyRouteLabels();
        RefreshMethodDetail();
        Recalculate();
    }

    partial void OnTargetReputationTextChanged(string value) => Recalculate();

    partial void OnUseBijouxChanged(bool value)
    {
        if (value && UseCoins)
            UseCoins = false;
        else if (!value && !UseCoins)
            UseCoins = true;

        NotifyZandalarGoldEstimates();
        NotifyRouteLabels();
        RefreshMethodDetail();
        Recalculate();
    }

    partial void OnUseCoinsChanged(bool value)
    {
        if (value && UseBijoux)
            UseBijoux = false;
        else if (!value && !UseBijoux)
            UseBijoux = true;

        NotifyZandalarGoldEstimates();
        NotifyRouteLabels();
        RefreshMethodDetail();
        Recalculate();
    }

    partial void OnZandalarCoinSellGoldTextChanged(string value) => OnZandalarCoinPriceChanged();

    partial void OnZandalarCoinSellSilverTextChanged(string value) => OnZandalarCoinPriceChanged();

    partial void OnZandalarCoinSellCopperTextChanged(string value) => OnZandalarCoinPriceChanged();

    private void OnZandalarCoinPriceChanged()
    {
        NotifyZandalarGoldEstimates();
        RefreshStockFindings();
    }

    private void ApplyZandalarDefaults()
    {
        TargetReputationText = "12000";
        ZandalarCoinSellGoldText = "5";
        ZandalarCoinSellSilverText = "0";
        ZandalarCoinSellCopperText = "0";
        UseCoins = true;
    }

    private void NotifyZandalarGoldEstimates()
    {
        OnPropertyChanged(nameof(ZandalarCoinSellPriceCopper));
        OnPropertyChanged(nameof(ShowZandalarGoldEstimates));
        OnPropertyChanged(nameof(ShowZandalarItemsNeededGold));
        OnPropertyChanged(nameof(ShowZandalarGoldSummaryRow));
        OnPropertyChanged(nameof(TotalStockGoldCopper));
        OnPropertyChanged(nameof(ItemsNeededGoldCopper));
        OnPropertyChanged(nameof(StockShortfallGoldCopper));
    }

    private static long ParseWowCurrencyText(string goldText, string silverText, string copperText)
    {
        _ = int.TryParse(goldText.Trim(), out var gold);
        _ = int.TryParse(silverText.Trim(), out var silver);
        _ = int.TryParse(copperText.Trim(), out var copper);

        gold = Math.Max(0, gold);
        silver = Math.Clamp(silver, 0, 99);
        copper = Math.Clamp(copper, 0, 99);

        return gold * 10000L + silver * 100L + copper;
    }

    private void OnCartoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == CartoViewModel.CharactersLoadedPropertyName && _cartoVm.CharactersLoaded)
            RefreshStockAccounts();
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
        RefreshStockFindings();
    }

    private void OnStockAccountSelectionChanged()
    {
        if (_suppressStockAccountSelectionCallback) return;
        _stockOwnerDefaultsApplied = true;
        OnPropertyChanged(nameof(SelectedStockAccountsText));
        RefreshStockFindings();
    }

    private void SyncFarmOptions()
    {
        _suppressFarmOptionSync = true;
        try
        {
            TierOptions.Clear();
            RouteOptions.Clear();
            TierMaterialGroups.Clear();
            SelectedTier = null;
            SelectedVariantRouteId = null;

            if (SelectedFarm == null) return;

            if (UsesTierSelection)
            {
                var tiers = SelectedFarm.Definition.Tiers;
                for (var i = 0; i < tiers.Count; i++)
                    TierOptions.Add(new ReputationTierRow(tiers[i], i + 1));

                SelectedTier = TierOptions.FirstOrDefault();
                if (SelectedTier != null)
                    TargetReputationText = SelectedTier.Tier.ReputationNeeded.ToString();
            }
            else
            {
                foreach (var route in SelectedFarm.Definition.Routes)
                    RouteOptions.Add(new ReputationRouteRow(route));

                SelectedVariantRouteId = RouteOptions.FirstOrDefault()?.RouteId;
            }
        }
        finally
        {
            _suppressFarmOptionSync = false;
        }

        OnPropertyChanged(nameof(ShowTierPicker));
        OnPropertyChanged(nameof(ShowTierMaterials));
        OnPropertyChanged(nameof(ShowRoutePicker));
        OnPropertyChanged(nameof(ShowStandardCalculator));
        OnPropertyChanged(nameof(ShowStandardObjectsPanel));
    }

    private ReputationFarmTier? GetSelectedTier() => SelectedTier?.Tier;

    private IReadOnlyList<ReputationTurnInRoute> GetTierRoutes()
    {
        var tier = GetSelectedTier();
        if (tier == null || SelectedFarm == null) return [];

        return tier.VariantRouteIds
            .Select(id => ReputationTurnInCatalog.TryGetRouteById(SelectedFarm.Definition, id))
            .Where(r => r != null)
            .Cast<ReputationTurnInRoute>()
            .ToList();
    }

    private ReputationTurnInRoute? GetActiveRoute()
    {
        if (SelectedFarm == null) return null;

        if (ShowZandalarExchangeMode)
            return ReputationTurnInCatalog.TryGetRoute(SelectedFarm.Definition, GetSelectedMethod());

        if (UsesTierSelection)
            return GetTierRoutes().FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(SelectedVariantRouteId))
        {
            var byId = ReputationTurnInCatalog.TryGetRouteById(
                SelectedFarm.Definition, SelectedVariantRouteId);
            if (byId != null) return byId;
        }

        return SelectedFarm.Definition.Routes.FirstOrDefault();
    }

    private ReputationTurnInMethod GetSelectedMethod()
    {
        if (SelectedFarm == null) return ReputationTurnInMethod.Bijoux;

        var routes = SelectedFarm.Definition.Routes;
        if (routes.Count == 1) return routes[0].Method;

        return UseBijoux ? ReputationTurnInMethod.Bijoux : ReputationTurnInMethod.Coins;
    }

    private bool UsesHonorTokenForActiveRoute()
    {
        var route = GetActiveRoute();
        return route is { HonorTokenReputation: > 0 };
    }

    private void NotifyRouteLabels()
    {
        OnPropertyChanged(nameof(StockItemUnitLabel));
        OnPropertyChanged(nameof(ItemsNeededLabel));
        OnPropertyChanged(nameof(StockTotalItemsCaption));
        OnPropertyChanged(nameof(AcceptedItemsHint));
        OnPropertyChanged(nameof(UsesFixedRequirements));
        OnPropertyChanged(nameof(TotalStockDisplay));
        OnPropertyChanged(nameof(UsesTierSelection));
        OnPropertyChanged(nameof(ShowTierPicker));
        OnPropertyChanged(nameof(ShowTierMaterials));
        OnPropertyChanged(nameof(ShowStandardCalculator));
        OnPropertyChanged(nameof(ShowStandardObjectsPanel));
    }

    private void RefreshMethodDetail()
    {
        AcceptedItems.Clear();
        var tier = GetSelectedTier();
        if (tier != null)
        {
            MethodDetailText = tier.DescriptionFr;
            return;
        }

        var route = GetActiveRoute();
        if (route == null)
        {
            MethodDetailText = "";
            return;
        }

        MethodDetailText = route.DescriptionFr;

        if (route.UsesFixedRequirements)
        {
            foreach (var req in route.Requirements)
                AcceptedItems.Add(new ReputationItemRow(req.ItemId, req.NameFr, req.QuantityPerTurnIn));
        }
        else
        {
            foreach (var item in route.AcceptedItems)
                AcceptedItems.Add(new ReputationItemRow(item.ItemId, item.NameFr));
        }
    }

    private IReadOnlyList<(int ItemId, string NameFr)> GetStockItems()
    {
        if (UsesTierSelection)
        {
            var items = new Dictionary<int, string>();
            foreach (var route in GetTierRoutes())
            {
                foreach (var (itemId, nameFr) in ReputationRouteHelper.GetStockItemIds(route))
                    items[itemId] = nameFr;
            }

            return items.Select(kv => (kv.Key, kv.Value)).ToList();
        }

        var activeRoute = GetActiveRoute();
        return activeRoute == null ? [] : ReputationRouteHelper.GetStockItemIds(activeRoute);
    }

    private void RefreshTierMaterials(int target)
    {
        TierMaterialGroups.Clear();
        var tier = GetSelectedTier();
        if (tier == null || SelectedFarm == null) return;

        var routes = GetTierRoutes();
        var showTitles = routes.Count > 1;

        foreach (var route in routes)
        {
            var result = _calculator.Calculate(route, target, useHonorToken: false);
            if (result == null) continue;

            var items = new ObservableCollection<ReputationItemNeedRow>();
            foreach (var need in result.ItemBreakdown)
                items.Add(new ReputationItemNeedRow(need));

            var rewards = new ObservableCollection<ReputationItemNeedRow>();
            foreach (var reward in route.TurnInRewards)
            {
                rewards.Add(new ReputationItemNeedRow(new ReputationItemNeed
                {
                    ItemId = reward.ItemId,
                    NameFr = reward.NameFr,
                    QuantityNeeded = 1,
                }));
            }

            var showCardTitle = showTitles || route.TurnInRewards.Count > 0;
            TierMaterialGroups.Add(new ReputationTierMaterialGroupRow(
                showCardTitle ? route.DisplayVariantLabel : "",
                result.ReputationPerTurnIn,
                route.DescriptionFr,
                items,
                rewards));
        }

        OnPropertyChanged(nameof(ShowTierMaterials));
    }

    private void RefreshStockFindings()
    {
        CharacterStockGroups.Clear();
        TotalStockFound = 0;
        TotalStockTurnIns = 0;
        TotalStockReputation = 0;

        var tierRoutes = UsesTierSelection ? GetTierRoutes() : [];
        var route = GetActiveRoute();
        var activeItems = GetStockItems();
        if (activeItems.Count == 0 || (!UsesTierSelection && route == null) || (UsesTierSelection && tierRoutes.Count == 0))
        {
            StockCharacterCount = 0;
            UpdateStockSummary(0, 0);
            return;
        }

        var selectedUserIds = StockAccounts
            .Where(a => a.IsSelected)
            .Select(a => a.UserId)
            .ToList();

        if (selectedUserIds.Count == 0)
        {
            StockCharacterCount = 0;
            StockSummaryText = "Sélectionnez au moins un propriétaire de compte.";
            NotifyStockGroupProperties();
            return;
        }

        var stock = _stock.ReadStockForOwners(selectedUserIds);
        var total = 0;
        var useHonorToken = UsesHonorTokenForActiveRoute();
        var itemUnitLabel = StockItemUnitLabel;
        var globalCounts = new Dictionary<int, int>();

        foreach (var character in stock.Characters.OrderBy(c => c.CharacterName, StringComparer.OrdinalIgnoreCase))
        {
            var lines = new List<ReputationStockLine>();
            var charCounts = new Dictionary<int, int>();
            var charTotal = 0;

            foreach (var (itemId, nameFr) in activeItems)
            {
                var count = character.GetTotalOnCharacter(itemId);
                if (count <= 0) continue;
                var lineGold = ShowZandalarGoldEstimates && ReputationTurnInCatalog.IsZandalarCoin(itemId)
                    ? count * ZandalarCoinSellPriceCopper
                    : 0L;
                lines.Add(new ReputationStockLine(itemId, nameFr, count, lineGold));
                charCounts[itemId] = count;
                charTotal += count;
                globalCounts[itemId] = globalCounts.GetValueOrDefault(itemId) + count;
            }

            if (lines.Count == 0) continue;

            int charReputation;
            int charTurnIns;
            if (UsesTierSelection)
            {
                charReputation = tierRoutes.Max(r =>
                    _calculator.CalculateReputationFromItemCounts(r, charCounts, useHonorToken));
                charTurnIns = tierRoutes.Max(r =>
                    ReputationRouteHelper.CountTurnInsFromPool(r, charCounts));
            }
            else
            {
                charReputation = _calculator.CalculateReputationFromItemCounts(route!, charCounts, useHonorToken);
                charTurnIns = ReputationRouteHelper.CountTurnInsFromPool(route!, charCounts);
            }

            var usesTurnIns = UsesTierSelection || route!.UsesFixedRequirements;
            var goldEstimate = ShowZandalarGoldEstimates ? charTotal * ZandalarCoinSellPriceCopper : 0L;
            CharacterStockGroups.Add(new ReputationCharacterStockGroup(
                character.CharacterName,
                character.AccountName,
                usesTurnIns ? charTurnIns : charTotal,
                charReputation,
                goldEstimate,
                itemUnitLabel,
                usesTurnIns,
                lines.OrderBy(l => l.NameFr, StringComparer.OrdinalIgnoreCase).ToList()));
            total += usesTurnIns ? charTurnIns : charTotal;
        }

        TotalStockFound = total;
        if (UsesTierSelection)
        {
            TotalStockTurnIns = tierRoutes.Max(r =>
                ReputationRouteHelper.CountTurnInsFromPool(r, globalCounts));
            TotalStockReputation = tierRoutes.Max(r =>
                _calculator.CalculateReputationFromItemCounts(r, globalCounts, useHonorToken));
        }
        else
        {
            TotalStockTurnIns = ReputationRouteHelper.CountTurnInsFromPool(route!, globalCounts);
            TotalStockReputation = _calculator.CalculateReputationFromItemCounts(route!, globalCounts, useHonorToken);
        }
        UpdateStockSummary(total, CharacterStockGroups.Count);
        NotifyStockGroupProperties();

        if (HasResult && ItemsNeededBreakdown.Count == 1)
            StockShortfall = Math.Max(0, ItemsNeededBreakdown[0].QuantityNeeded - globalCounts.GetValueOrDefault(ItemsNeededBreakdown[0].ItemId));
        else
            StockShortfall = 0;

        OnPropertyChanged(nameof(HasStockShortfall));
        NotifyZandalarGoldEstimates();
    }

    private void UpdateStockSummary(int total, int characterCount)
    {
        StockCharacterCount = characterCount;
        if (characterCount == 0)
        {
            StockSummaryText = UsesFixedRequirements
                ? "Aucun matériau trouvé sur les persos sélectionnés."
                : string.IsNullOrWhiteSpace(StockItemUnitLabel)
                    ? "Aucun objet trouvé sur les persos sélectionnés."
                    : $"Aucun {StockItemUnitLabel} trouvé sur les persos sélectionnés.";
            NotifyStockGroupProperties();
            return;
        }

        StockSummaryText =
            $"Sur {characterCount} perso{(characterCount > 1 ? "s" : "")} des comptes sélectionnés";
        NotifyStockGroupProperties();
    }

    private void NotifyStockGroupProperties()
    {
        OnPropertyChanged(nameof(HasCharacterStockGroups));
        OnPropertyChanged(nameof(HasNoCharacterStockGroups));
        OnPropertyChanged(nameof(HasStockTotals));
        OnPropertyChanged(nameof(StockItemUnitLabel));
        OnPropertyChanged(nameof(TotalStockDisplay));
    }

    private void Recalculate()
    {
        RefreshMethodDetail();

        if (SelectedFarm == null)
        {
            ClearResult("Sélectionnez une réputation.");
            RefreshStockFindings();
            return;
        }

        if (!SelectedFarm.Definition.IsImplemented)
        {
            ClearResult("Calculateur à venir — objets et réputation par échange à configurer.");
            RefreshStockFindings();
            return;
        }

        if (!int.TryParse(TargetReputationText.Trim(), out var target) || target <= 0)
        {
            ClearResult("Indiquez un objectif de réputation valide (nombre entier > 0).");
            RefreshStockFindings();
            return;
        }

        if (UsesTierSelection)
        {
            var tier = GetSelectedTier();
            var routes = GetTierRoutes();
            if (tier == null || routes.Count == 0)
            {
                ClearResult("Impossible de calculer avec ces paramètres.");
                RefreshStockFindings();
                return;
            }

            RefreshTierMaterials(target);
            var firstResult = _calculator.Calculate(routes[0], target, useHonorToken: false);
            if (firstResult == null || TierMaterialGroups.Count == 0)
            {
                ClearResult("Impossible de calculer avec ces paramètres.");
                RefreshStockFindings();
                return;
            }

            ItemsNeeded = 0;
            ReputationGained = firstResult.ReputationGained;
            ReputationPerTurnIn = firstResult.ReputationPerTurnIn;
            SummaryText =
                $"{tier.LabelFr} : objectif {target} rép., "
                + $"{firstResult.ReputationGained} rép. si remises complètes "
                + $"({firstResult.ReputationPerTurnIn} rép./remise).";
            HasResult = true;
            StatusText = SelectedFarm.Definition.FactionNameFr;
            ItemsNeededBreakdown.Clear();
            OnPropertyChanged(nameof(ShowSingleItemsNeeded));
            OnPropertyChanged(nameof(ShowItemsNeededBreakdown));
            OnPropertyChanged(nameof(UsesFixedRequirements));
            RefreshStockFindings();
            return;
        }

        var route = GetActiveRoute();
        if (route == null)
        {
            ClearResult("Impossible de calculer avec ces paramètres.");
            RefreshStockFindings();
            return;
        }

        var result = _calculator.Calculate(route, target, UsesHonorTokenForActiveRoute());
        if (result == null)
        {
            ClearResult("Impossible de calculer avec ces paramètres.");
            RefreshStockFindings();
            return;
        }

        ItemsNeeded = result.ItemsNeeded;
        ReputationGained = result.ReputationGained;
        ReputationPerTurnIn = result.ReputationPerTurnIn;
        SummaryText = result.SummaryFr;
        HasResult = true;
        StatusText = SelectedFarm.Definition.FactionNameFr;

        ItemsNeededBreakdown.Clear();
        foreach (var need in result.ItemBreakdown)
            ItemsNeededBreakdown.Add(new ReputationItemNeedRow(need));
        OnPropertyChanged(nameof(ShowSingleItemsNeeded));
        OnPropertyChanged(nameof(ShowItemsNeededBreakdown));
        OnPropertyChanged(nameof(UsesFixedRequirements));

        NotifyZandalarGoldEstimates();
        RefreshStockFindings();
    }

    private void ClearResult(string status)
    {
        ItemsNeeded = 0;
        ReputationGained = 0;
        ReputationPerTurnIn = 0;
        SummaryText = "";
        StockShortfall = 0;
        HasResult = false;
        StatusText = status;
        ItemsNeededBreakdown.Clear();
        TierMaterialGroups.Clear();
        NotifyZandalarGoldEstimates();
        OnPropertyChanged(nameof(ShowSingleItemsNeeded));
        OnPropertyChanged(nameof(ShowItemsNeededBreakdown));
        OnPropertyChanged(nameof(ShowTierMaterials));
    }
}

public sealed class ReputationTierMaterialGroupRow
{
    public ReputationTierMaterialGroupRow(
        string title,
        int reputationPerTurnIn,
        string turnInDescriptionFr,
        ObservableCollection<ReputationItemNeedRow> items,
        ObservableCollection<ReputationItemNeedRow> rewardItems)
    {
        Title = title;
        ReputationPerTurnIn = reputationPerTurnIn;
        TurnInDescriptionFr = turnInDescriptionFr;
        Items = items;
        RewardItems = rewardItems;
    }

    public string Title { get; }
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
    public int ReputationPerTurnIn { get; }
    public string TurnInDescriptionFr { get; }
    public string Subtitle => $"{ReputationPerTurnIn} rép./remise · {TurnInDescriptionFr}";
    public ObservableCollection<ReputationItemNeedRow> Items { get; }
    public ObservableCollection<ReputationItemNeedRow> RewardItems { get; }
    public bool HasRewards => RewardItems.Count > 0;
}

public sealed class ReputationFarmRow
{
    public ReputationFarmRow(ReputationFarmDefinition definition) => Definition = definition;

    public ReputationFarmDefinition Definition { get; }

    public string DisplayName => Definition.FactionNameFr;

    public bool IsImplemented => Definition?.IsImplemented == true;

    public string StatusBadge => IsImplemented ? "" : "À venir";

    public string Subtitle => IsImplemented
        ? $"{Definition.NpcNameFr} — {Definition.LocationFr}"
        : Definition.LocationFr;
}

public sealed class ReputationTierRow
{
    public ReputationTierRow(ReputationFarmTier tier, int stepIndex)
    {
        Tier = tier;
        StepIndex = stepIndex;
    }

    public ReputationFarmTier Tier { get; }

    public int StepIndex { get; }

    public string TierId => Tier.TierId;

    public string Label => $"{Tier.LabelFr} ({Tier.ReputationNeeded:N0} rép.)";

    public string ReputationNeededLabel => Tier.ReputationNeeded.ToString("N0");
}

public sealed class ReputationRouteRow
{
    public ReputationRouteRow(ReputationTurnInRoute route) => Route = route;

    public ReputationTurnInRoute Route { get; }

    public string RouteId => Route.RouteId;

    public string Label => Route.DisplayVariantLabel;
}

public sealed class ReputationItemRow
{
    public ReputationItemRow(int itemId, string nameFr, int quantityPerTurnIn = 0)
    {
        ItemId = itemId;
        NameFr = nameFr;
        QuantityPerTurnIn = quantityPerTurnIn;
        WowItem = new WowItem { ItemId = itemId, Name = nameFr, Count = 1 };
    }

    public int ItemId { get; }
    public string NameFr { get; }
    public int QuantityPerTurnIn { get; }
    public WowItem WowItem { get; }

    public string QuantityHint => QuantityPerTurnIn > 0 ? $"×{QuantityPerTurnIn}/remise" : "";
}

public sealed class ReputationItemNeedRow
{
    public ReputationItemNeedRow(ReputationItemNeed need)
    {
        ItemId = need.ItemId;
        NameFr = need.NameFr;
        QuantityNeeded = need.QuantityNeeded;
        WowItem = new WowItem { ItemId = need.ItemId, Name = need.NameFr, Count = need.QuantityNeeded };
    }

    public int ItemId { get; }
    public string NameFr { get; }
    public int QuantityNeeded { get; }
    public WowItem WowItem { get; }

    public string QuantityLabel => $"×{QuantityNeeded}";
}

public sealed class ReputationCharacterStockGroup
{
    public ReputationCharacterStockGroup(
        string characterName,
        string accountName,
        int totalOnCharacter,
        int reputationOnCharacter,
        long goldCopperEstimate,
        string itemUnitLabel,
        bool usesTurnInCount,
        IReadOnlyList<ReputationStockLine> lines)
    {
        CharacterName = characterName;
        AccountName = accountName;
        TotalOnCharacter = totalOnCharacter;
        ReputationOnCharacter = reputationOnCharacter;
        GoldCopperEstimate = goldCopperEstimate;
        ItemUnitLabel = itemUnitLabel;
        UsesTurnInCount = usesTurnInCount;
        foreach (var line in lines)
            Lines.Add(line);
    }

    public string CharacterName { get; }
    public string AccountName { get; }
    public int TotalOnCharacter { get; }
    public int ReputationOnCharacter { get; }
    public long GoldCopperEstimate { get; }
    public bool HasGoldEstimate => GoldCopperEstimate > 0;
    public string ItemUnitLabel { get; }
    public bool UsesTurnInCount { get; }
    public ObservableCollection<ReputationStockLine> Lines { get; } = [];

    public string ItemUnitCaption
    {
        get
        {
            if (UsesTurnInCount) return "Remises";
            if (string.IsNullOrWhiteSpace(ItemUnitLabel)) return "Objets";
            return char.ToUpper(ItemUnitLabel[0]) + ItemUnitLabel[1..];
        }
    }

    public string HeaderText => string.IsNullOrWhiteSpace(AccountName)
        ? CharacterName
        : $"{CharacterName} — {AccountName}";
}

public sealed class ReputationStockLine
{
    public ReputationStockLine(int itemId, string nameFr, int count, long goldCopperEstimate = 0)
    {
        ItemId = itemId;
        NameFr = nameFr;
        Count = count;
        GoldCopperEstimate = goldCopperEstimate;
        WowItem = new WowItem { ItemId = itemId, Name = nameFr, Count = count };
    }

    public int ItemId { get; }
    public string NameFr { get; }
    public int Count { get; }
    public long GoldCopperEstimate { get; }
    public bool HasGoldEstimate => GoldCopperEstimate > 0;
    public WowItem WowItem { get; }

    public string QuantityLabel => $"×{Count}";
}
