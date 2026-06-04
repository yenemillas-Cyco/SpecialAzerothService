using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsOrganiserApp.Controls;
using WindowsOrganiserApp.Converters;
using SpecialAzerothService.Core.Models;
using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;
using WindowsOrganiserApp.Models.Carto;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.ViewModels;

public partial class CartoViewModel : ObservableObject
{
    private readonly ICartoService _cartoService;
    private readonly IWowSyncService _wowSyncService;
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _cooldownTimer;
    private CartoData _data;
    private List<WowAccountData>? _wowSyncCache;
    private Dictionary<string, WowCharacterData> _syncByKey = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, WowCharacterData> _syncByNormalizedKey = new(StringComparer.Ordinal);
    private Dictionary<string, CartoAccountConfig>? _accountSettingsEditSnapshot;
    private string? _appliedWowPath;
    private bool _wowFolderPromptShownThisSession;
    private bool _startupWtfScanDone;
    private readonly SemaphoreSlim _wowSyncRefreshLock = new(1, 1);

    private enum WowWtfScanMode
    {
        StartupOnce,
        ManualRescan
    }

    /// <summary>Notifie la vue (roster) après un scan WowSync terminé.</summary>
    public event EventHandler? CharactersRescanned;
    public event EventHandler? RosterRefreshRequested;
    private bool _zoneCalibrationLoaded;
    private bool _zonePanelDataLoaded;
    private int _mapPlacementStamp;
    private int _appliedMapPlacementStamp;
    private bool _mapPositionsReady;
    private IReadOnlyList<CartoMapPositionPrecompute.CharacterPlacement>? _precomputedPlacements;

    /// <summary>Chaque seconde — mise à jour des compteurs sans redessiner la carte.</summary>
    public event EventHandler? SecondTick;

    public CartoViewModel(
        ICartoService cartoService,
        IWowSyncService wowSyncService,
        ISettingsService settingsService,
        AppSettings settings)
    {
        _cartoService = cartoService;
        _wowSyncService = wowSyncService;
        _settingsService = settingsService;
        _settings = settings;
        _data = _cartoService.Load();

        _data.AccountSettings ??= new Dictionary<string, CartoAccountConfig>(StringComparer.OrdinalIgnoreCase);
        CartoAccountSettings.MigrateLegacyDisplayNames(_data);
        CartoUserMigration.Migrate(_data);
        CartoUserMigration.MigrateRerollIntoMain(_data);
        MigrateLegacyCharacterData();
        MigrateCharacterProfiles();
        MigratePlacedOnMapFlags();
        MigrateStripNonAddonMapPositions();
        MigrateClearBulkMapPlacementFlags();
        ApplyConfiguredAccountSettings();

        Accounts = new ObservableCollection<WowAccount>();
        Characters = new ObservableCollection<WowCharacter>();
        RosterTreeRoots = [];
        Timers = new ObservableCollection<MapTimer>(_data.Timers);

        _cooldownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cooldownTimer.Tick += (_, _) =>
        {
            SecondTick?.Invoke(this, EventArgs.Empty);
            CheckTimerAlerts();
            CheckCooldownNotifications();
        };
        _cooldownTimer.Start();

        ApplyFilters();
        CharacterSyncGoldConverter.Vm = this;
        EnsureDefaultCartoUserExists();
        RefreshCartoUserCollections();
        ApplyStoredWowGameRootFromSettings();
    }

    private void EnsureDefaultCartoUserExists()
    {
        _data.Users ??= [];
        if (_data.Users.Any(u =>
                u.Name.Equals(CartoUserMigration.DefaultUserName, StringComparison.OrdinalIgnoreCase)))
            return;

        _data.Users.Add(new CartoUser
        {
            Name = CartoUserMigration.DefaultUserName,
            SortOrder = 0
        });
        _cartoService.Save(_data);
    }

    /// <summary>Enregistre la racine WoW (fichier settings + singleton + service + UI).</summary>
    private void PersistWowGameRoot(string gameRoot)
    {
        var root = WowInstallPaths.NormalizeStoredPath(gameRoot);
        if (string.IsNullOrWhiteSpace(root))
            return;

        _appliedWowPath = root;
        _settings.WowPath = root;
        if (_settings.DataSchemaVersion < CartoDataSchemaMigration.CurrentVersion)
            _settings.DataSchemaVersion = CartoDataSchemaMigration.CurrentVersion;
        WowGameRootStore.Write(root);
        _settingsService.Save(_settings);

        if (!string.Equals(WowPath, root, StringComparison.Ordinal))
            WowPath = root;

        OnPropertyChanged(nameof(AddonInstallPathHint));
        OnPropertyChanged(nameof(ResolvedWowPathsSummary));
    }

    private void ApplyStoredWowGameRootFromSettings()
    {
        var candidate = !string.IsNullOrWhiteSpace(_settings.WowPath)
            ? _settings.WowPath
            : !string.IsNullOrWhiteSpace(_wowSyncService.WowPath)
                ? _wowSyncService.WowPath
                : WowGameRootStore.Read() ?? "";

        if (string.IsNullOrWhiteSpace(candidate))
        {
            _appliedWowPath = "";
            WowPath = "";
            AddonStatusText =
                "⚠ Chemin WoW non enregistré.\nCarto → Paramètres → choisir le dossier « World of Warcraft » (ex. D:\\Programmes\\World of Warcraft).";
            return;
        }

        if (WowInstallPaths.TryCompleteUserFolder(candidate, out var resolved))
        {
            PersistWowGameRoot(resolved.GameRoot);
            return;
        }

        WowPath = WowInstallPaths.NormalizeStoredPath(candidate);
        _appliedWowPath = WowPath;
        AddonStatusText = WowInstallPaths.GetValidationError(WowPath);
    }

    private bool TryGetStoredWowResolution(out WowInstallPaths.WowPathResolution resolution)
    {
        if (WowInstallPaths.TryCompleteUserFolder(_wowSyncService.WowPath, out resolution))
            return true;
        if (WowInstallPaths.TryCompleteUserFolder(_settings.WowPath, out resolution))
            return true;
        return WowInstallPaths.TryCompleteUserFolder(WowPath, out resolution);
    }

    public string AddonVersion => _wowSyncService.AddonVersion;

    [ObservableProperty]
    private string _wowPath = "";

    [ObservableProperty]
    private string _addonStatusText = "";

    partial void OnWowPathChanged(string value)
    {
        var stored = WowInstallPaths.NormalizeStoredPath(value);
        if (!string.Equals(stored, value, StringComparison.Ordinal))
        {
            WowPath = stored;
            return;
        }

        OnPropertyChanged(nameof(AddonInstallPathHint));
        OnPropertyChanged(nameof(ResolvedWowPathsSummary));

        if (string.IsNullOrWhiteSpace(stored))
        {
            _appliedWowPath = "";
            _settings.WowPath = "";
            WowGameRootStore.TryDelete();
            _settingsService.Save(_settings);
            AddonStatusText = "";
            InvalidateWowSyncLoadState();
            return;
        }

        if (!WowInstallPaths.TryCompleteUserFolder(stored, out var resolved))
        {
            _appliedWowPath = stored;
            AddonStatusText = WowInstallPaths.GetValidationError(value);
            InvalidateWowSyncLoadState();
            return;
        }

        PersistWowGameRoot(resolved.GameRoot);
        if (!string.Equals(WowPath, resolved.GameRoot, StringComparison.Ordinal))
            return;

        InvalidateWowSyncLoadState();
        AddonStatusText = "Chemin enregistré — cliquez « Rescanner » pour charger les personnages depuis WTF.";
    }

    public string ResolvedWowPathsSummary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(WowPath))
                return "";

            return WowInstallPaths.DescribeResolution(WowPath, WowSyncService.AddonVersionValue);
        }
    }

    /// <summary>Valide, enregistre le dossier choisi et charge les personnages (bouton 📁).</summary>
    public async Task<bool> CommitWowGameRootAsync(string? folderPath, bool showErrors = false)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;

        if (!WowInstallPaths.TryCompleteUserFolder(folderPath, out var resolved))
        {
            var error = WowInstallPaths.GetValidationError(folderPath);
            AddonStatusText = error;
            if (showErrors)
            {
                System.Windows.MessageBox.Show(
                    error,
                    "Dossier World of Warcraft incorrect",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }

            return false;
        }

        PersistWowGameRoot(resolved.GameRoot);
        if (_startupWtfScanDone)
        {
            InvalidateWowSyncLoadState();
            return await ScanWowFromWtfCoreAsync(WowWtfScanMode.ManualRescan).ConfigureAwait(true);
        }

        return await ScanWowFromWtfCoreAsync(WowWtfScanMode.StartupOnce).ConfigureAwait(true);
    }

    /// <summary>Scan WTF unique au démarrage (après choix du chemin WoW).</summary>
    public Task<bool> ScanWowFromWtfAtStartupAsync() =>
        RunOnUiThreadAsync(() => ScanWowFromWtfCoreAsync(WowWtfScanMode.StartupOnce));

    /// <summary>Vide la liste puis relit uniquement les WowSync.lua sous WTF.</summary>
    public Task<bool> RescanWowFromWtfAsync() =>
        RunOnUiThreadAsync(() => ScanWowFromWtfCoreAsync(WowWtfScanMode.ManualRescan));

    public Task<bool> RescanWowAndAccountsAsync() => RescanWowFromWtfAsync();

    /// <summary>Aligne settings/WowSyncService sur <see cref="WowPath"/> (champ UI).</summary>
    public bool EnsureWowPathPersisted(out WowInstallPaths.WowPathResolution resolution)
    {
        if (!TryGetStoredWowResolution(out resolution))
        {
            var stored = WowInstallPaths.NormalizeStoredPath(WowPath);
            if (!string.IsNullOrWhiteSpace(stored)
                && WowInstallPaths.TryCompleteUserFolder(stored, out resolution))
                PersistWowGameRoot(resolution.GameRoot);
            else
                return false;
        }
        else
        {
            PersistWowGameRoot(resolution.GameRoot);
        }

        return true;
    }

    public bool EnsureWowPathPersisted() => EnsureWowPathPersisted(out _);

    private static async Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher
                         ?? throw new InvalidOperationException("Application WPF non initialisée.");
        if (dispatcher.CheckAccess())
            return await action().ConfigureAwait(true);
        return await dispatcher.InvokeAsync(action).Task.Unwrap().ConfigureAwait(true);
    }

    private void InvalidateWowSyncLoadState()
    {
        _startupWtfScanDone = false;
        _wowSyncCache = null;
        CharactersLoaded = false;
        OnPropertyChanged(CharactersLoadedPropertyName);
    }

    /// <summary>Supprime tous les persos/comptes dérivés du WTF avant un nouveau scan.</summary>
    private void ClearWowSyncRosterState()
    {
        Characters.Clear();
        Accounts.Clear();
        _data.Accounts.Clear();
        _wowSyncCache = null;
        _syncByKey.Clear();
        _syncByNormalizedKey.Clear();
        _precomputedPlacements = null;
        _mapPositionsReady = false;
        _appliedMapPlacementStamp = 0;
        _mapPlacementStamp = 0;
        FilteredCharacters.Clear();
        CharactersLoaded = false;
        OnPropertyChanged(CharactersLoadedPropertyName);
        OnPropertyChanged(nameof(FilteredCharacters));
        BumpMapPlacementStamp();
    }

    /// <summary>Demande le dossier WoW au premier lancement si absent (addon déjà installé).</summary>
    private async Task EnsureWowPathConfiguredAsync()
    {
        if (WowInstallPaths.TryCompleteUserFolder(WowPath, out _)
            || WowInstallPaths.TryCompleteUserFolder(_wowSyncService.WowPath, out _))
            return;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        await dispatcher.InvokeAsync(async () =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Dossier World of Warcraft (racine du jeu)"
            };
            if (dialog.ShowDialog() != true)
                return;

            await CommitWowGameRootAsync(dialog.FolderName, showErrors: true).ConfigureAwait(true);
        }, System.Windows.Threading.DispatcherPriority.Normal).Task.Unwrap();
    }

    public string AddonInstallPathHint =>
        string.IsNullOrWhiteSpace(WowPath) ? "" : WowInstallPaths.DescribeResolution(WowPath);

    public const string CharactersLoadedPropertyName = nameof(CharactersLoaded);

    public bool CharactersLoaded { get; private set; }

    /// <summary>Ne relit pas WTF — le scan a lieu une seule fois au démarrage ou via Rescanner.</summary>
    public Task EnsureCharacterDataLoadedAsync() => Task.CompletedTask;

    /// <summary>Applique les coords absolues calculées au warmup (cache map-positions-cache.json).</summary>
    private void ApplyPrecomputedMapPositions()
    {
        if (_precomputedPlacements == null || _precomputedPlacements.Count == 0)
            return;

        var byKey = _precomputedPlacements.ToDictionary(
            p => p.SyncKey,
            StringComparer.OrdinalIgnoreCase);

        foreach (var ch in Characters.Where(c => !string.IsNullOrEmpty(c.SyncKey)))
        {
            if (!byKey.TryGetValue(ch.SyncKey, out var placement) || !placement.Placed)
                continue;

            ApplySyncPosition(ch, placement.MapX, placement.MapY);
            ch.IsPlacedOnMap = true;
        }

        _mapPositionsReady = true;
        _zoneCalibrationLoaded = true;
    }

    public bool MapPositionsReady => _mapPositionsReady;

    /// <summary>Marqueurs carte : IsPlacedOnMap + pile pour le reste (sans recalcul zone).</summary>
    public void RefreshMapDisplayPlacement() => FinishMapPlacementForDisplay();

    private void FinishMapPlacementForDisplay()
    {
        foreach (var ch in Characters.Where(IsEligibleForStartupMapPlacement))
            ch.IsPlacedOnMap = true;

        ReorganizePlacedOnMapStacks();
        _appliedMapPlacementStamp = _mapPlacementStamp;

        RefreshUnplacedCharacters();
        if (ApplyFiltersChanged())
            OnPropertyChanged(nameof(FilteredCharacters));
        OnPropertyChanged(nameof(OverlayChanged));
    }

    /// <summary>Tous les persos locaux visibles sur WowMap.png (WowSync puis pile pour le reste).</summary>
    public void EnsureCharactersVisibleOnMap(bool force = false)
    {
        if (!force && _appliedMapPlacementStamp == _mapPlacementStamp)
            return;

        if (!force && _mapPositionsReady)
        {
            FinishMapPlacementForDisplay();
            return;
        }

        EnsureZoneCalibrationLoaded();

        foreach (var ch in Characters.Where(IsEligibleForStartupMapPlacement))
            ch.IsPlacedOnMap = true;

        var _ = ApplyZonePositionsFromWowSync();
        ReorganizePlacedOnMapStacks();
        _appliedMapPlacementStamp = _mapPlacementStamp;
        _mapPositionsReady = true;

        RefreshUnplacedCharacters();
        if (ApplyFiltersChanged())
            OnPropertyChanged(nameof(FilteredCharacters));
        OnPropertyChanged(nameof(OverlayChanged));
    }

    private void RebuildPrecomputedMapPositions()
    {
        if (CartoMapPreloader.GetBitmap() is { PixelWidth: > 0, PixelHeight: > 0 } bmp)
            ClassicEraMapProjection.SetMapImagePixelSize(bmp.PixelWidth, bmp.PixelHeight);

        var accounts = GetCachedWowSyncAccounts();
        _precomputedPlacements = CartoMapPositionPrecompute.ComputeForAccounts(accounts);
        ApplyPrecomputedMapPositions();
    }

    private void BumpMapPlacementStamp() => _mapPlacementStamp++;

    private bool ApplyFiltersChanged()
    {
        var filtered = Characters.AsEnumerable()
            .Where(c => IsCharacterVisibleOnMap(c));

        if (FilterClass.HasValue)
            filtered = filtered.Where(c => c.Class == FilterClass.Value);

        if (FilterLevelMin.HasValue)
            filtered = filtered.Where(c => c.Level >= FilterLevelMin.Value);

        if (FilterLevelMax.HasValue)
            filtered = filtered.Where(c => c.Level <= FilterLevelMax.Value);

        if (!string.IsNullOrEmpty(FilterAccountId))
            filtered = filtered.Where(c => c.AccountId == FilterAccountId);

        if (!string.IsNullOrEmpty(FilterName))
            filtered = filtered.Where(c => c.Name.Contains(FilterName, StringComparison.OrdinalIgnoreCase));

        if (FilterStatus.HasValue)
            filtered = filtered.Where(c => c.Status == FilterStatus.Value);

        var list = filtered.ToList();
        if (FilteredListsEqual(FilteredCharacters, list))
            return false;

        FilteredCharacters = new ObservableCollection<WowCharacter>(list);
        return true;
    }

    /// <summary>Préchargement au splash : persos, carte, placement.</summary>
    public async Task WarmupAsync(IProgress<StartupLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        void Report(double percent, string message) =>
            progress?.Report(new StartupLoadProgress(percent, message));

        Report(22, "Chemin World of Warcraft…");
        ApplyStoredWowGameRootFromSettings();
        await EnsureWowPathConfiguredAsync().ConfigureAwait(false);

        if (EnsureWowPathPersisted(out _) && !_startupWtfScanDone)
        {
            Report(28, "Scan WTF (WowSync.lua)…");
            var loaded = await ScanWowFromWtfAtStartupAsync().ConfigureAwait(false);
            if (loaded)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsRosterOpen = true;
                }, DispatcherPriority.Normal);
            }
        }
        else
        {
            Report(28, "Choisissez le dossier WoW dans Carto → Paramètres.");
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AddonStatusText =
                    "⚠ Chemin WoW non enregistré.\nChoisissez le dossier « World of Warcraft » dans Paramètres (📁), puis Rescanner.";
            }, DispatcherPriority.Normal);
        }

        Report(52, "Chargement de la carte…");
        await Task.Run(CartoMapPreloader.EnsureLoaded, cancellationToken).ConfigureAwait(false);

        var (mapW, mapH) = CartoMapPreloader.PixelSize;

        Report(72, "Positions WowSync sur la carte…");

        _ = Task.Run(CartoMapQuestIcon.PreloadQuestStubIcons);

        var uiDispatcher = System.Windows.Application.Current?.Dispatcher
                           ?? throw new InvalidOperationException("Application WPF non initialisée.");
        await uiDispatcher.InvokeAsync(() =>
        {
            if (NeedsMigration && mapW > 0 && mapH > 0)
                MigrateCoordinates(mapW, mapH);
            if (!_mapPositionsReady)
                EnsureCharactersVisibleOnMap(force: true);
            else
                FinishMapPlacementForDisplay();
        }, DispatcherPriority.Background);

        Report(90, "Préparation de l'interface Carto…");
        await uiDispatcher.InvokeAsync(PrepareMapDisplay, DispatcherPriority.Background);

        Report(96, "Finalisation…");
    }

    public ObservableCollection<WowAccount> Accounts { get; }
    public ObservableCollection<WowCharacter> Characters { get; }
    public ObservableCollection<CartoRosterTreeNode> RosterTreeRoots { get; }
    public ObservableCollection<MapTimer> Timers { get; }

    [ObservableProperty]
    private ObservableCollection<WowCharacter> _filteredCharacters = [];


    [ObservableProperty]
    private WowCharacter? _selectedCharacter;

    [ObservableProperty]
    private WowClass? _filterClass;

    [ObservableProperty]
    private int? _filterLevelMin;

    [ObservableProperty]
    private int? _filterLevelMax;

    [ObservableProperty]
    private string? _filterAccountId;

    [ObservableProperty]
    private string _filterName = string.Empty;

    [ObservableProperty]
    private double _mapZoom = 1.0;

    [ObservableProperty]
    private double _mapOffsetX;

    [ObservableProperty]
    private double _mapOffsetY;

    // New character form
    [ObservableProperty]
    private string _newCharName = string.Empty;

    [ObservableProperty]
    private WowClass _newCharClass;

    [ObservableProperty]
    private int _newCharLevel = 1;

    [ObservableProperty]
    private string? _newCharAccountId;

    [ObservableProperty]
    private CharacterStatus _newCharStatus = CharacterStatus.Main;

    [ObservableProperty]
    private bool _isPlacingCharacter;

    [ObservableProperty]
    private bool _isPlacingTimer;

    /// <summary>Bandeau droit : liste des personnages ou recherche d'objets.</summary>
    [ObservableProperty]
    private CartoRightPanelMode _rightPanelMode = CartoRightPanelMode.Characters;

    /// <summary>Volet personnages visible à droite.</summary>
    [ObservableProperty]
    private bool _isRosterOpen;

    [ObservableProperty]
    private bool _isCooldownRosterOpen;

    /// <summary>Volet détail personnage (remplace la popup).</summary>
    [ObservableProperty]
    private bool _isCharacterDetailOpen;

    /// <summary>Volet paramètres (comptes WoW, utilisateurs locaux).</summary>
    [ObservableProperty]
    private bool _isSettingsPanelOpen;

    /// <summary>Volet recherche d'objets à côté du roster.</summary>
    [ObservableProperty]
    private bool _isItemSearchOpen;

    /// <summary>Volet timers à côté du roster.</summary>
    [ObservableProperty]
    private bool _isTimersPanelOpen;

    [ObservableProperty]
    private string _itemSearchQuery = "";

    public ObservableCollection<WowItemSearchResult> ItemSearchResults { get; } = [];

    partial void OnItemSearchQueryChanged(string value) => UpdateItemSearch();

    partial void OnRightPanelModeChanged(CartoRightPanelMode value)
    {
        if (value == CartoRightPanelMode.ItemSearch)
        {
            IsItemSearchOpen = true;
            UpdateItemSearch();
        }
    }

    partial void OnIsItemSearchOpenChanged(bool value)
    {
        if (value)
            UpdateItemSearch();
    }

    [RelayCommand]
    private void ToggleItemSearchPanel() => IsItemSearchOpen = !IsItemSearchOpen;

    [RelayCommand]
    private void ToggleTimersPanel() => IsTimersPanelOpen = !IsTimersPanelOpen;

    [RelayCommand]
    private void ToggleRosterPanel() => IsRosterOpen = !IsRosterOpen;

    [RelayCommand]
    private async Task BrowseWowPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Dossier World of Warcraft (racine du jeu)",
            InitialDirectory = string.IsNullOrWhiteSpace(WowPath) ? null : WowPath
        };
        if (dialog.ShowDialog() == true)
            await CommitWowGameRootAsync(dialog.FolderName, showErrors: true).ConfigureAwait(true);
    }

    [RelayCommand]
    private void DeployAddon()
    {
        if (!EnsureWowPathPersisted(out var resolved))
        {
            AddonStatusText = string.IsNullOrWhiteSpace(WowPath)
                ? "⚠ Choisissez le dossier « World of Warcraft » d'abord."
                : WowInstallPaths.GetValidationError(WowPath);
            return;
        }

        try
        {
            _wowSyncService.DeployAddon(resolved.GameRoot);
            OnPropertyChanged(nameof(AddonInstallPathHint));
            AddonStatusText =
                $"✅ Addon v{WowSyncService.AddonVersionValue} déployé.\n{resolved.AddonsDirectory}\n/reload en jeu.";
        }
        catch (Exception ex)
        {
            AddonStatusText = $"❌ Erreur déploiement : {ex.Message}";
        }
    }

    partial void OnIsSettingsPanelOpenChanged(bool value)
    {
        if (value)
            BeginAccountSettingsEdit();
        else if (!_settingsPanelClosingAfterSave)
            CancelAccountSettingsEdit();
    }

    private bool _settingsPanelClosingAfterSave;

    [RelayCommand]
    private void ClearItemSearch()
    {
        ItemSearchQuery = string.Empty;
    }

    [RelayCommand]
    private void SelectItemSearchResult(WowItemSearchResult? result)
    {
        if (result?.Character == null)
            return;

        var cartoChar = Characters.FirstOrDefault(c =>
            c.SyncKey.Equals(result.Character.Key, StringComparison.OrdinalIgnoreCase));
        if (cartoChar != null)
            SelectedCharacter = cartoChar;
    }

    public void UpdateItemSearch()
    {
        var results = CartoItemSearch.Search(GetCachedWowSyncAccounts(), ItemSearchQuery);
        ItemSearchResults.Clear();
        foreach (var r in results)
            ItemSearchResults.Add(r);
        OnPropertyChanged(nameof(ItemSearchResults));
    }

    [ObservableProperty]
    private int _newTimerHours;

    [ObservableProperty]
    private int _newTimerMinutes = 15;

    [ObservableProperty]
    private int _newTimerSeconds;

    [ObservableProperty]
    private string _newTimerLabel = "New_Timer";

    [ObservableProperty]
    private string? _editNote = string.Empty;

    // Map overlay toggles (vols uniquement)
    [ObservableProperty]
    private bool _showAllianceFlightPaths;

    [ObservableProperty]
    private bool _showHordeFlightPaths;

    partial void OnShowAllianceFlightPathsChanged(bool value) => OnPropertyChanged(nameof(OverlayChanged));
    partial void OnShowHordeFlightPathsChanged(bool value) => OnPropertyChanged(nameof(OverlayChanged));

    public object? OverlayChanged => null;

    [ObservableProperty]
    private CharacterStatus? _filterStatus;

    partial void OnFilterClassChanged(WowClass? value) => ApplyFilters();
    partial void OnFilterLevelMinChanged(int? value) => ApplyFilters();
    partial void OnFilterLevelMaxChanged(int? value) => ApplyFilters();
    partial void OnFilterAccountIdChanged(string? value) => ApplyFilters();
    partial void OnFilterNameChanged(string value) => ApplyFilters();
    partial void OnFilterStatusChanged(CharacterStatus? value) => ApplyFilters();

    partial void OnSelectedCharacterChanged(WowCharacter? value)
    {
        EditNote = value?.Note ?? string.Empty;
    }

    public Array WowClasses => Enum.GetValues(typeof(WowClass));
    public CharacterStatus[] CharacterStatusValues { get; } = Enum.GetValues(typeof(CharacterStatus))
        .Cast<CharacterStatus>()
        .Where(s => s != CharacterStatus.Reroll)
        .ToArray();

    /// <summary>Catégories affichées dans le bandeau (Reroll fusionné dans Personnages).</summary>
    public static CharacterStatus[] RosterCategoryStatuses { get; } =
    [
        CharacterStatus.Main,
        CharacterStatus.Banque,
        CharacterStatus.TpBoy,
        CharacterStatus.ClicBoys
    ];

    public static string RosterCategoryTitle(CharacterStatus status) => status switch
    {
        CharacterStatus.Main => "Personnages",
        _ => status.DisplayName()
    };

    public static bool IsPersonnagesCategory(CharacterStatus status) =>
        status is CharacterStatus.Main or CharacterStatus.Reroll;

    public static IEnumerable<CharacterStatus> StatusesForRosterCategory(CharacterStatus frameStatus) =>
        frameStatus == CharacterStatus.Main
            ? [CharacterStatus.Main, CharacterStatus.Reroll]
            : [frameStatus];
    public ProfessionType[] ProfessionTypes => Enum.GetValues(typeof(ProfessionType))
        .Cast<ProfessionType>()
        .Where(p => p is not (ProfessionType.Peche or ProfessionType.Cuisine or ProfessionType.Secourisme))
        .ToArray();
    public CooldownType[] CooldownTypes => Enum.GetValues(typeof(CooldownType))
        .Cast<CooldownType>()
        .Where(ct => ct is not (CooldownType.Transmutation or CooldownType.Etoffe_lunaire or CooldownType.Etoffe_de_lombre))
        .ToArray();
    public Array QuestItemTypes => Enum.GetValues(typeof(QuestItemType));

    /// <summary>Tous les personnages locaux pour le volet Personnages (sans filtre carte).</summary>
    public IEnumerable<WowCharacter> GetCharactersForRoster() => Characters;

    public bool IsCharacterEligibleForRosterTree(WowCharacter c) => true;

    /// <summary>Marqueur affiché sur la carte (perso + propriétaire / compte / catégorie).</summary>
    public bool IsCharacterVisibleOnMap(WowCharacter ch)
    {
        if (ch.IsHidden)
            return false;

        var account = _data.Accounts.FirstOrDefault(a => a.Id == ch.AccountId);
        if (account?.IsHidden == true)
            return false;

        var userId = GetUserIdForCharacter(ch);
        var user = GetUserById(userId);
        if (user?.IsRosterSubtreeHidden == true)
            return false;

        var frameCategory = ch.Status == CharacterStatus.Reroll ? CharacterStatus.Main : ch.Status;
        if (!RosterCategoryStatuses.Contains(frameCategory))
            return true;

        var policy = _data.CategoryPolicies.FirstOrDefault(p =>
            p.UserId == userId && p.Category == frameCategory);
        return policy?.IsRosterSubtreeHidden != true;
    }

    public void ToggleUserMapVisibility(CartoUser user)
    {
        user.IsRosterSubtreeHidden = !user.IsRosterSubtreeHidden;
        Save();
        ApplyFilters();
        OnPropertyChanged(nameof(OverlayChanged));
    }

    public void ToggleAccountMapVisibility(WowAccount account)
    {
        account.IsHidden = !account.IsHidden;
        Save();
        ApplyFilters();
        OnPropertyChanged(nameof(OverlayChanged));
    }

    public void ToggleCategoryMapVisibility(CartoUser user, CharacterStatus category)
    {
        var policy = GetCategoryPolicy(user.Id, category);
        policy.IsRosterSubtreeHidden = !policy.IsRosterSubtreeHidden;
        Save();
        ApplyFilters();
        OnPropertyChanged(nameof(OverlayChanged));
    }

    public bool IsUserVisibleOnMap(CartoUser user) => !user.IsRosterSubtreeHidden;

    public bool IsAccountVisibleOnMap(WowAccount account) => !account.IsHidden;

    public bool IsCategoryVisibleOnMap(CartoUser user, CharacterStatus category) =>
        !GetCategoryPolicy(user.Id, category).IsRosterSubtreeHidden;

    [ObservableProperty]
    private string _autoSortStatusMessage = "";

    [RelayCommand]
    private void AutoSortCharacterCategories()
    {
        var changed = 0;
        foreach (var ch in Characters)
        {
            var sync = FindWowSyncCharacter(ch);
            var suggested = CartoCharacterCategoryAutoSort.SuggestCategory(ch, sync);
            if (suggested == null || ch.Status == suggested)
                continue;

            ch.Status = suggested.Value;
            changed++;
        }

        Save();
        ApplyFilters();
        RefreshRosterTree();
        AutoSortStatusMessage = changed == 0
            ? "Aucun changement (règles déjà appliquées ou niveaux hors plage)."
            : $"{changed} personnage(s) reclassé(s).";
    }

    [RelayCommand]
    private void HideAllExceptTpBoyOnMap()
    {
        foreach (var ch in Characters)
            ch.IsHidden = ch.Status != CharacterStatus.TpBoy;

        Save();
        ApplyFilters();
        OnPropertyChanged(nameof(OverlayChanged));
        AutoSortStatusMessage = "Carte : seuls les TP Boy restent visibles.";
    }

    [RelayCommand]
    private void ShowAllOnMap()
    {
        foreach (var ch in Characters)
            ch.IsHidden = false;

        foreach (var user in _data.Users)
            user.IsRosterSubtreeHidden = false;

        foreach (var policy in _data.CategoryPolicies)
            policy.IsRosterSubtreeHidden = false;

        foreach (var account in _data.Accounts)
            account.IsHidden = false;

        Save();
        ApplyFilters();
        OnPropertyChanged(nameof(OverlayChanged));
        AutoSortStatusMessage = "Tous les personnages sont à nouveau visibles sur la carte.";
    }

    /// <summary>Sans réglage utilisateur : tous les comptes WTF sont rattachés à « Moi ».</summary>
    public void EnsureAccountsAssignedToDefaultUser()
    {
        EnsureDefaultCartoUserExists();
        var moiId = GetDefaultUserId();
        if (string.IsNullOrWhiteSpace(moiId))
            return;

        _data.AccountSettings ??= new Dictionary<string, CartoAccountConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (var account in Accounts)
        {
            var folder = GetSourceFolderForAccount(account);
            if (string.IsNullOrWhiteSpace(folder))
                continue;

            if (!_data.AccountSettings.TryGetValue(folder, out var cfg))
            {
                _data.AccountSettings[folder] = new CartoAccountConfig
                {
                    DisplayName = account.Name,
                    UserId = moiId,
                    Scope = AccountScope.Mine
                };
                continue;
            }

            if (string.IsNullOrWhiteSpace(cfg.UserId))
                cfg.UserId = moiId;
        }
    }

    public void RefreshRosterTree(Func<string, bool, bool>? resolveExpanded = null)
    {
        EnsureAccountsAssignedToDefaultUser();
        CartoRosterTreeBuilder.Rebuild(this, RosterTreeRoots, resolveExpanded);
        OnPropertyChanged(nameof(RosterTreeRoots));
        RosterRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    public string? GetDefaultUserId() =>
        _data.Users.FirstOrDefault(u =>
            u.Name.Equals(CartoUserMigration.DefaultUserName, StringComparison.OrdinalIgnoreCase))?.Id
        ?? _data.Users.OrderBy(u => u.SortOrder).FirstOrDefault()?.Id;

    private void ApplyFilters()
    {
        if (ApplyFiltersChanged())
            OnPropertyChanged(nameof(FilteredCharacters));
    }

    /// <summary>Placement + filtres carte (appel à l'ouverture de l'onglet Carto).</summary>
    public void PrepareMapDisplay()
    {
        if (!CharactersLoaded)
            return;

        EnsureCharactersVisibleOnMap(force: true);
        ApplyFilters();
    }

    private static bool FilteredListsEqual(
        IReadOnlyList<WowCharacter> current,
        List<WowCharacter> next)
    {
        if (current.Count != next.Count) return false;
        for (var i = 0; i < current.Count; i++)
        {
            if (!ReferenceEquals(current[i], next[i])) return false;
        }

        return true;
    }

    [RelayCommand]
    private void AddAccount(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var account = new WowAccount { Name = name };
        Accounts.Add(account);
        Save();
    }

    [RelayCommand]
    private void RemoveAccount(WowAccount account)
    {
        Accounts.Remove(account);
        Save();
    }

    [RelayCommand]
    private void StartPlaceCharacter()
    {
        // Liste des personnages fournie par WowSync uniquement.
    }

    public void PlaceCharacterAt(double mapX, double mapY)
    {
        if (!IsPlacingCharacter) return;
        IsPlacingCharacter = false;
        _movingCharacter = null;
    }

    [RelayCommand]
    private void RemoveCharacter(WowCharacter character)
    {
        if (!string.IsNullOrEmpty(character.SyncKey))
        {
            character.IsHidden = true;
            if (SelectedCharacter == character) SelectedCharacter = null;
            ApplyFilters();
            Save();
            return;
        }

        Characters.Remove(character);
        if (SelectedCharacter == character) SelectedCharacter = null;
        ApplyFilters();
        Save();
    }

    private WowCharacter? _movingCharacter;

    [RelayCommand]
    private void MoveCharacter(WowCharacter character)
    {
        if (character.IsLocked) return;
        _movingCharacter = character;
        SelectedCharacter = character;
        IsPlacingCharacter = true;
    }

    public void CancelPlacement()
    {
        _movingCharacter = null;
        IsPlacingCharacter = false;
    }

    [RelayCommand]
    private void SaveNote()
    {
        if (SelectedCharacter == null) return;
        SelectedCharacter.Note = EditNote ?? string.Empty;
        Save();
    }

    [RelayCommand]
    private void AddCooldown(CooldownType type)
    {
        if (SelectedCharacter == null) return;
        if (SelectedCharacter.Cooldowns.Any(c => c.Type == type)) return;

        if (CooldownGroups.IsAlchemyTransmute(type))
        {
            foreach (var other in SelectedCharacter.Cooldowns.Where(c => CooldownGroups.IsAlchemyTransmute(c.Type)).ToList())
                SelectedCharacter.Cooldowns.Remove(other);
        }

        SelectedCharacter.Cooldowns.Add(new CooldownEntry { Type = type });
        CooldownGroups.NormalizeAlchemyCooldowns(SelectedCharacter.Cooldowns);
        Save();
        OnPropertyChanged(nameof(SelectedCharacter));
    }

    [RelayCommand]
    private void ActivateCooldown(CooldownEntry cd)
    {
        if (SelectedCharacter != null && CooldownGroups.IsAlchemyTransmute(cd.Type))
        {
            foreach (var other in SelectedCharacter.Cooldowns.Where(c => CooldownGroups.IsAlchemyTransmute(c.Type) && c != cd).ToList())
                SelectedCharacter.Cooldowns.Remove(other);
            cd.ReadyAtOverride = null;
        }

        cd.LastUsed = DateTime.Now;
        cd.Note = null;
        CooldownGroups.NormalizeAlchemyCooldowns(SelectedCharacter?.Cooldowns ?? []);
        Save();
        OnPropertyChanged(nameof(SelectedCharacter));
        OnPropertyChanged(nameof(FilteredCharacters));
    }

    [RelayCommand]
    private void RemoveCooldown(CooldownEntry cd)
    {
        if (SelectedCharacter == null) return;
        SelectedCharacter.Cooldowns.Remove(cd);
        Save();
        OnPropertyChanged(nameof(SelectedCharacter));
    }

    [RelayCommand]
    private void AddQuestItem(QuestItemType type)
    {
        if (SelectedCharacter == null) return;
        if (SelectedCharacter.QuestItems.Any(q => q.Type == type)) return;

        SelectedCharacter.QuestItems.Add(new QuestItemEntry { Type = type, HasItem = true });
        Save();
        OnPropertyChanged(nameof(SelectedCharacter));
    }

    [RelayCommand]
    private void SetQuestItemPlanning(QuestItemEntry item)
    {
        item.PlannedTurnIn = DateTime.Now.AddHours(1);
        Save();
        OnPropertyChanged(nameof(SelectedCharacter));
    }

    [RelayCommand]
    private void MarkQuestItemTurnedIn(QuestItemEntry item)
    {
        if (SelectedCharacter == null) return;
        SelectedCharacter.QuestItems.Remove(item);
        Save();
        OnPropertyChanged(nameof(SelectedCharacter));
    }

    [RelayCommand]
    private void AddProfession(ProfessionType type)
    {
        if (SelectedCharacter == null) return;
        if (SelectedCharacter.Professions.Any(p => p.Type == type)) return;

        SelectedCharacter.Professions.Add(new ProfessionInfo { Type = type, Skill = 1 });
        Save();
        OnPropertyChanged(nameof(SelectedCharacter));
    }

    public bool TryGetMarkerPosition(WowCharacter ch, out double x, out double y)
    {
        if (!ch.IsPlacedOnMap)
        {
            x = 0;
            y = 0;
            return false;
        }

        x = ch.MapX;
        y = ch.MapY;
        return true;
    }

    /// <summary>Réorganise la pile des persos sans position WowSync ni placement manuel.</summary>
    public void ReorganizePlacedOnMapStacks()
    {
        var stackIndex = 0;
        foreach (var ch in Characters
                     .Where(c => c.IsPlacedOnMap && !c.IsHidden)
                     .OrderBy(c => Accounts.FirstOrDefault(a => a.Id == c.AccountId)?.Name ?? "")
                     .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (HasWowSyncMapPosition(ch))
                continue;

            var (x, y) = CartoMapLayout.GetStackPosition(stackIndex++);
            ch.MapX = x;
            ch.MapY = y;
        }
    }

    private bool HasWowSyncMapPosition(WowCharacter ch) => HasEffectiveWorldMapPlacement(ch);

    public bool HasEffectiveWorldMapPlacement(WowCharacter ch)
    {
        return CanResolveWowSyncWorldPosition(ch);
    }

    private bool CanResolveWowSyncWorldPosition(WowCharacter ch)
    {
        if (ch.IsHidden)
            return false;

        var sync = FindWowSyncCharacter(ch);
        if (sync == null || (sync.X <= 0 && sync.Y <= 0))
            return false;

        return ClassicEraMapProjection.TryConvert(sync, out _, out _)
               || CartoDungeonMarkerResolver.TryResolve(sync.Zone, sync.SubZone, out _, out _);
    }

    public ObservableCollection<CartoUnplacedCharacterItem> UnplacedCharacters { get; } = [];

    public void RefreshUnplacedCharacters()
    {
        if (!IsZonesPanelOpen)
            return;

        var list = new List<CartoUnplacedCharacterItem>();
        foreach (var ch in Characters.Where(IsEligibleForStartupMapPlacement))
        {
            var sync = FindWowSyncCharacter(ch);
            if (sync != null && (sync.X > 0 || sync.Y > 0)
                && (ClassicEraMapProjection.TryConvert(sync, out _, out _)
                    || CartoDungeonMarkerResolver.TryResolve(sync.Zone, sync.SubZone, out _, out _)))
                continue;

            if (sync == null && !ch.IsPlacedOnMap)
                continue;
            var account = Accounts.FirstOrDefault(a => a.Id == ch.AccountId);
            var reason = ClassifyUnplacedReason(ch, sync);
            var coords = sync == null
                ? "—"
                : sync.X > 0 || sync.Y > 0
                    ? $"{sync.X * 100:F1}, {sync.Y * 100:F1}"
                    : "0, 0";

            list.Add(new CartoUnplacedCharacterItem
            {
                SyncKey = ch.SyncKey,
                Name = ch.Name,
                AccountName = account?.Name ?? "",
                Zone = sync?.Zone ?? "",
                SubZone = sync?.SubZone ?? "",
                MapId = sync?.MapId ?? 0,
                CoordsDisplay = coords,
                Reason = reason,
                IsOnStack = CartoMapLayout.IsStackPosition(ch.MapX, ch.MapY)
            });
        }

        var ordered = list
            .OrderBy(i => i.AccountName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (UnplacedListsEqual(UnplacedCharacters, ordered))
            return;

        UnplacedCharacters.Clear();
        foreach (var item in ordered)
            UnplacedCharacters.Add(item);

        if (SelectedZoneRect == null && SelectedDungeonMarker == null)
        {
            var n = UnplacedCharacters.Count;
            ZonePanelStatusMessage = n == 0
                ? "Tous les personnages visibles sont placés sur la carte."
                : $"{n} personnage(s) non placés (zone / instance / coords) — voir liste ci-dessous.";
        }
    }

    private static bool UnplacedListsEqual(
        IReadOnlyList<CartoUnplacedCharacterItem> current,
        IReadOnlyList<CartoUnplacedCharacterItem> next)
    {
        if (current.Count != next.Count)
            return false;

        for (var i = 0; i < current.Count; i++)
        {
            var a = current[i];
            var b = next[i];
            if (!a.SyncKey.Equals(b.SyncKey, StringComparison.OrdinalIgnoreCase)
                || a.Reason != b.Reason
                || a.MapId != b.MapId)
                return false;
        }

        return true;
    }

    private static CartoUnplacedReason ClassifyUnplacedReason(WowCharacter ch, WowCharacterData? sync)
    {
        if (sync == null)
            return CartoUnplacedReason.NoSync;
        if (sync.X <= 0 && sync.Y <= 0)
            return CartoUnplacedReason.CoordsZero;
        if (LooksLikeInstanceZone(sync))
            return CartoUnplacedReason.InInstance;
        return CartoUnplacedReason.ZoneNotCalibrated;
    }

    private static bool LooksLikeInstanceZone(WowCharacterData sync)
    {
        if (CartoDungeonMarkerResolver.TryResolve(sync.Zone, sync.SubZone, out _, out _))
            return false;

        var blob = $"{sync.Zone} {sync.SubZone}".ToLowerInvariant();
        foreach (var entry in CartoDungeonCatalog.All)
        {
            if (blob.Contains(entry.NameFr.ToLowerInvariant(), StringComparison.Ordinal))
                return true;
        }

        return sync.MapId > 0
               && !ClassicEraMapProjection.IsCapitalMap(sync.MapId)
               && !IsKnownOpenWorldMapId(sync.MapId);
    }

    private static bool IsKnownOpenWorldMapId(int mapId) =>
        ClassicEraMapProjection.TryGetCatalogEntry(mapId, out _);

    [RelayCommand]
    private void SelectUnplacedCharacter(CartoUnplacedCharacterItem? item)
    {
        if (item == null)
            return;

        var ch = Characters.FirstOrDefault(c =>
            c.SyncKey.Equals(item.SyncKey, StringComparison.OrdinalIgnoreCase));
        if (ch == null)
            return;

        SelectedCharacter = ch;
        ZonePanelStatusMessage = $"{ch.Name} : {item.ZoneDisplay} — {item.ReasonDisplay}";
    }

    /// <summary>Change la catégorie et replace le perso (cadre gauche ou pile carte).</summary>
    public void SetCharacterStatus(WowCharacter character, CharacterStatus status)
    {
        if (character.Status == status)
            return;

        character.Status = status;
        RefreshRosterTree();

        // Hors carte → le cadre correspondant est mis à jour par la vue.
        // Sur carte → retirer pour rejoindre le cadre de la nouvelle catégorie.
        if (character.IsPlacedOnMap)
        {
            character.IsPlacedOnMap = false;
            ReorganizePlacedOnMapStacks();
        }

        Save();
        ApplyFilters();
    }

    [RelayCommand]
    public void PlaceCharacterOnMap(WowCharacter? character)
    {
        character ??= SelectedCharacter;
        if (character == null) return;

        character.IsPlacedOnMap = true;
        character.HasCustomMapPosition = true;
        ReorganizePlacedOnMapStacks();
        ApplyFilters();
        Save();
    }

    /// <summary>Place un perso sur la carte à une position précise (drag depuis un cadre).</summary>
    public void PlaceCharacterOnMapAt(WowCharacter character, double mapX, double mapY)
    {
        character.IsPlacedOnMap = true;
        if (!TryApplyWowSyncMapPosition(character))
        {
            character.HasCustomMapPosition = true;
            ReorganizePlacedOnMapStacks();
        }

        ApplyFilters();
        Save();
    }

    /// <summary>Retire de la carte et place dans le cadre de catégorie (drag depuis la carte).</summary>
    public void MoveCharacterToCategoryFrame(WowCharacter character, CharacterStatus category)
    {
        if (character.Status != category)
            character.Status = category;

        if (character.IsPlacedOnMap)
        {
            character.IsPlacedOnMap = false;
            ReorganizePlacedOnMapStacks();
        }

        Save();
        ApplyFilters();
        RefreshRosterTree();
    }

    [RelayCommand]
    public void RemoveCharacterFromMap(WowCharacter? character)
    {
        character ??= SelectedCharacter;
        if (character == null) return;

        character.IsPlacedOnMap = false;
        if (SelectedCharacter == character)
            SelectedCharacter = null;
        ReorganizePlacedOnMapStacks();
        ApplyFilters();
        Save();
    }

    public void ApplyMapPosition(WowCharacter ch, double x, double y)
    {
        TryApplyWowSyncMapPosition(ch);
    }

    public void ApplySyncPosition(WowCharacter cartoChar, double mapX, double mapY)
    {
        cartoChar.MapX = mapX;
        cartoChar.MapY = mapY;
        cartoChar.HasCustomMapPosition = false;
    }

    /// <summary>Recharge la liste depuis WowSync. Ne réorganise la carte que si <paramref name="reapplyMapLayout"/>.</summary>
    public int RefreshCharactersFromWowSync(bool saveAfter = false, bool reapplyMapLayout = false)
    {
        _wowSyncCache = null;
        _syncByKey.Clear();
        _syncByNormalizedKey.Clear();
        var syncAccounts = GetCachedWowSyncAccounts();
        RebuildSyncIndex();
        var extrasByKey = BuildExtrasByKey();
        var profilesByKey = _data.CharacterProfiles
            .ToDictionary(p => p.SyncKey, StringComparer.OrdinalIgnoreCase);

        var preservedAccounts = _data.Accounts
            .Concat(Accounts)
            .Where(a => !string.IsNullOrWhiteSpace(a.SourceFolder))
            .GroupBy(a => a.SourceFolder, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        Characters.Clear();
        Accounts.Clear();
        _data.Accounts.Clear();

        var stackIndex = 0;
        var seenCharacterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenGlobalNameRealm = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var syncAccount in syncAccounts.OrderBy(a => a.SourceAccountName, StringComparer.OrdinalIgnoreCase))
        {
            var sourceFolder = syncAccount.SourceAccountName;
            if (string.IsNullOrWhiteSpace(sourceFolder))
                sourceFolder = syncAccount.AccountName;

            var displayName = CartoAccountSettings.ResolveDisplayName(sourceFolder, _data.AccountSettings);
            _data.AccountSettings.TryGetValue(sourceFolder, out var folderCfg);

            WowAccount account;
            if (preservedAccounts.TryGetValue(sourceFolder, out var existing))
            {
                account = existing;
                account.Name = displayName;
                account.SourceFolder = sourceFolder;
            }
            else
            {
                account = new WowAccount
                {
                    SourceFolder = sourceFolder,
                    Name = displayName,
                    IsHidden = folderCfg?.IsHiddenOnMap ?? false
                };
            }

            _data.Accounts.Add(account);
            if (!Accounts.Contains(account))
                Accounts.Add(account);

            if (!string.IsNullOrWhiteSpace(sourceFolder)
                && !_data.AccountSettings.ContainsKey(sourceFolder))
            {
                _data.AccountSettings[sourceFolder] = new CartoAccountConfig
                {
                    DisplayName = displayName,
                    UserId = CartoUserMigration.ResolveDefaultUserIdForFolder(sourceFolder, _data)
                        ?? GetDefaultUserId()
                };
            }

            foreach (var syncChar in syncAccount.Characters.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(syncChar.Name))
                    continue;

                var rosterKey = $"{sourceFolder}|{syncChar.Key}";
                if (!seenCharacterKeys.Add(rosterKey))
                    continue;

                var globalKey = CartoCharacterSyncKey.Normalize($"{syncChar.Name}|{syncChar.Realm}");
                if (!seenGlobalNameRealm.Add(globalKey))
                    continue;

                var extras = ResolveCharacterExtras(extrasByKey, syncChar);
                CartoCharacterProfile? profile = null;
                if (!profilesByKey.TryGetValue(syncChar.Key, out profile)
                    && !string.IsNullOrEmpty(syncChar.StorageKey))
                    profilesByKey.TryGetValue(syncChar.StorageKey, out profile);
                var cartoChar = CartoSyncMapper.ToCartoCharacter(syncChar, account.Id, extras, profile, stackIndex);
                CartoSyncMapper.ApplyCooldownsFromSync(syncChar, cartoChar);
                CartoCharacterEnricher.ApplyFromSync(syncChar, cartoChar);
                Characters.Add(cartoChar);
            }
        }

        foreach (var ch in Characters.Where(c => c.Status == CharacterStatus.Reroll))
            ch.Status = CharacterStatus.Main;

        CleanupAccountsFromWowSync();

        BumpMapPlacementStamp();
        RebuildPrecomputedMapPositions();
        FinishMapPlacementForDisplay();

        AccountIdToNameConverter.Accounts = [.. Accounts];
        CharacterSyncGoldConverter.Vm = this;
        ApplyFilters();
        OnPropertyChanged(nameof(OverlayChanged));

        if (saveAfter)
            Save();

        return stackIndex;
    }

    /// <summary>Premiers persos sans entrée extras : pile carte. Les positions sauvegardées ne sont pas écrasées.</summary>
    private void AutoPlaceNewCharactersWithoutExtras()
    {
        var extrasKeys = _data.CharacterExtras
            .Select(e => e.SyncKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var ch in Characters.Where(c => IsEligibleForStartupMapPlacement(c) && !extrasKeys.Contains(c.SyncKey)))
            ch.IsPlacedOnMap = true;

        ReorganizePlacedOnMapStacks();
    }

    /// <summary>Place sur la carte via rectangle de zone + coords WowSync (addon).</summary>
    public string? TryPlaceCharacterFromWowSync(WowCharacter? character = null)
    {
        character ??= SelectedCharacter;
        if (character == null)
            return "Aucun personnage sélectionné.";

        var sync = FindWowSyncCharacter(character);
        if (sync == null)
            return "Données WowSync introuvables — actualisez WowSync.";

        if (sync.X <= 0 && sync.Y <= 0)
            return "Coords à 0 — reconnectez-vous en jeu, /reload, puis actualisez WowSync.";

        EnsureZoneCalibrationLoaded();

        if (!CartoDungeonMarkerResolver.TryResolve(sync.Zone, sync.SubZone, out var mapX, out var mapY)
            && !ClassicEraMapProjection.TryConvert(sync, out mapX, out mapY))
        {
            var zoneLabel = string.IsNullOrWhiteSpace(sync.Zone) ? "?" : sync.Zone;
            if (!string.IsNullOrWhiteSpace(sync.SubZone))
                zoneLabel += $" — {sync.SubZone}";
            return $"Zone non calibrée : « {zoneLabel} » (map {sync.MapId}).\n"
                   + "Calibrez le rectangle (volet Zones) ou placez un repère lieu-dit, puis réessayez.";
        }

        ApplySyncPosition(character, mapX, mapY);
        character.IsPlacedOnMap = true;

        Save();
        ApplyFilters();
        RefreshUnplacedCharacters();
        OnPropertyChanged(nameof(OverlayChanged));

        return null;
    }

    private void EnsureZoneCalibrationLoaded()
    {
        if (_zoneCalibrationLoaded)
            return;

        var user = ZoneMapCalibration.LoadUserOverrides();
        var calibrated = ZoneMapCalibration.BuildProjectionCalibration(user);
        if (calibrated.Count > 0)
            ClassicEraMapProjection.ApplyUserRects(calibrated);
        _zoneCalibrationLoaded = true;
    }

    public void InvalidateZoneCalibration()
    {
        _zoneCalibrationLoaded = false;
        _mapPositionsReady = false;
        BumpMapPlacementStamp();
    }

    private static void ReloadZoneCalibration()
    {
        var user = ZoneMapCalibration.LoadUserOverrides();
        var calibrated = ZoneMapCalibration.BuildProjectionCalibration(user);
        if (calibrated.Count > 0)
            ClassicEraMapProjection.ApplyUserRects(calibrated);
    }

    /// <summary>Recalcule MapX/MapY uniquement depuis WowSync (addon).</summary>
    private int ApplyZonePositionsFromWowSync()
    {
        var placed = 0;
        foreach (var ch in Characters.Where(c => !c.IsHidden))
        {
            if (TryApplyWowSyncMapPosition(ch))
            {
                ch.IsPlacedOnMap = true;
                placed++;
            }
        }

        return placed;
    }

    /// <summary>Position carte depuis l'addon ; false si pas de coords / zone non calibrée.</summary>
    private bool TryApplyWowSyncMapPosition(WowCharacter ch)
    {
        ch.HasCustomMapPosition = false;

        var sync = FindWowSyncCharacter(ch);
        if (sync == null || (sync.X <= 0 && sync.Y <= 0))
            return false;

        if (!CartoDungeonMarkerResolver.TryResolve(sync.Zone, sync.SubZone, out var mapX, out var mapY)
            && !ClassicEraMapProjection.TryConvert(sync, out mapX, out mapY))
            return false;

        ApplySyncPosition(ch, mapX, mapY);
        return true;
    }

    private bool IsEligibleForStartupMapPlacement(WowCharacter ch) => IsCharacterVisibleOnMap(ch);

    public int CountLocalCharactersForUser(string userId) =>
        Characters.Count(c => GetUserIdForCharacter(c) == userId);

    public int CountLocalCharactersInCategory(string userId, CharacterStatus frameCategory)
    {
        var statuses = StatusesForRosterCategory(frameCategory).ToHashSet();
        return Characters.Count(c =>
            GetUserIdForCharacter(c) == userId
            && statuses.Contains(c.Status));
    }

    [RelayCommand]
    private void PlaceSelectedFromWowSync()
    {
        var error = TryPlaceCharacterFromWowSync(SelectedCharacter);
        if (error != null)
            System.Windows.MessageBox.Show(error, "Placement WowSync", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private Task RefreshWowSync() => RescanWowFromWtfAsync();

    private async Task<bool> ScanWowFromWtfCoreAsync(WowWtfScanMode mode)
    {
        await _wowSyncRefreshLock.WaitAsync().ConfigureAwait(true);
        try
        {
            if (mode == WowWtfScanMode.StartupOnce && _startupWtfScanDone)
                return Characters.Count > 0;

            if (!EnsureWowPathPersisted(out _))
            {
                AddonStatusText = string.IsNullOrWhiteSpace(_settings.WowPath) && string.IsNullOrWhiteSpace(WowPath)
                    ? "⚠ Chemin WoW non enregistré — Paramètres → dossier « World of Warcraft »."
                    : WowInstallPaths.GetValidationError(WowPath);
                return false;
            }

            ClearWowSyncRosterState();

            var gameRoot = _wowSyncService.WowPath;
            AddonStatusText = mode == WowWtfScanMode.ManualRescan
                ? "Rescan WTF : vidage puis lecture WowSync.lua…"
                : "Lecture WTF (WowSync.lua)…";

            var accountSettings = _data.AccountSettings;
            var (scan, syncAccounts, placements) = await Task.Run(() =>
            {
                var diagnostics = _wowSyncService.GetScanDiagnostics(gameRoot);
                var accounts = _wowSyncService.ReadAllAccounts(accountSettings, gameRoot);
                var computed = CartoMapPositionPrecompute.ComputeForAccounts(accounts);
                return (diagnostics, accounts, computed);
            }).ConfigureAwait(true);

            ApplyWowSyncScanResults(scan, syncAccounts, placements);
            _startupWtfScanDone = true;

            var localCount = Characters.Count;
            AddonStatusText = BuildWowSyncStatusText(scan, _wowSyncService.ListWtfAccountFolderNames(), localCount);
            if (localCount > 0)
                IsRosterOpen = true;
            return localCount > 0;
        }
        catch (Exception ex)
        {
            AddonStatusText = $"Erreur lecture WTF : {ex.Message}";
            return false;
        }
        finally
        {
            _wowSyncRefreshLock.Release();
        }
    }

    private void ApplyWowSyncScanResults(
        WowSyncScanDiagnostics scan,
        List<WowAccountData> syncAccounts,
        IReadOnlyList<CartoMapPositionPrecompute.CharacterPlacement> placements)
    {
        var wtfFolders = _wowSyncService.ListWtfAccountFolderNames();

        CartoUserMigration.Migrate(_data);
        EnsureDefaultCartoUserExists();
        _wowSyncCache = syncAccounts;
        BumpMapPlacementStamp();
        RefreshCharactersFromWowSync(saveAfter: false, reapplyMapLayout: false);

        _precomputedPlacements = placements;
        ApplyPrecomputedMapPositions();
        FinishMapPlacementForDisplay();

        ApplySyncEnrichmentForAll();
        UpdateItemSearch();
        ApplyFilters();
        Save();
        OnPropertyChanged(nameof(OverlayChanged));

        var localCount = Characters.Count;
        AddonStatusText = BuildWowSyncStatusText(scan, wtfFolders, localCount);

        RefreshAccountSettingRows();
        ApplyConfiguredAccountSettings();

        CharactersLoaded = true;
        OnPropertyChanged(CharactersLoadedPropertyName);
        OnPropertyChanged(nameof(FilteredCharacters));
        RefreshCartoUserCollections();
        RefreshRosterTree();
        CharactersRescanned?.Invoke(this, EventArgs.Empty);

        if (localCount > 0)
        {
            IsCooldownRosterOpen = false;
            IsRosterOpen = true;
        }
    }

    private static string BuildWowSyncStatusText(
        WowSyncScanDiagnostics scan,
        IReadOnlyList<string> wtfFolders,
        int loadedCharacterCount)
    {
        if (string.IsNullOrWhiteSpace(scan.WtfAccountPath) && scan.Issues.Count > 0)
            return $"❌ {scan.Issues[0]}";

        if (string.IsNullOrWhiteSpace(scan.WtfAccountPath))
            return $"❌ {scan.Issues.FirstOrDefault() ?? "Chemin WoW invalide."}";

        if (scan.WtfFolderCount == 0)
            return $"⚠ Comptes : {scan.WtfAccountPath}\nAucun sous-dossier compte Battle.net.";

        if (loadedCharacterCount > 0)
        {
            return $"✅ {loadedCharacterCount} personnage(s) chargé(s).\n"
                   + $"Lecture : {scan.WtfAccountPath}\n"
                   + $"({scan.WowSyncLuaFileCount} WowSync.lua, {scan.CharactersInLuaFiles} entrée(s) en jeu)";
        }

        var folderHint = wtfFolders.Count > 0
            ? string.Join(", ", wtfFolders.Take(4)) + (wtfFolders.Count > 4 ? "…" : "")
            : "—";
        var issue = scan.Issues.FirstOrDefault()
                    ?? "Aucun personnage dans WowSync.lua — connectez-vous, /wowsync, déconnectez-vous.";
        return $"⚠ 0 personnage chargé ({scan.WtfFolderCount} compte(s) WTF : {folderHint}).\n"
               + $"Fichiers WowSync.lua : {scan.WowSyncLuaFileCount} — entrées lues : {scan.CharactersInLuaFiles}.\n"
               + issue;
    }

    public string? GetCharacterPositionDisplay(WowCharacter ch)
    {
        var sync = FindWowSyncCharacter(ch);
        return sync == null ? null : sync.PositionDisplay;
    }

    public void ApplySyncEnrichment(WowCharacter ch)
    {
        var sync = FindWowSyncCharacter(ch);
        if (sync != null)
            CartoCharacterEnricher.ApplyFromSync(sync, ch);
    }

    public void ApplySyncEnrichmentForAll()
    {
        foreach (var ch in Characters)
            ApplySyncEnrichment(ch);
    }

    public WowCharacterData? FindWowSyncCharacter(WowCharacter ch)
    {
        EnsureSyncIndex();
        if (TryGetSyncCharacter(ch.SyncKey, out var found))
            return found;

        if (!string.IsNullOrEmpty(ch.Name)
            && TryGetSyncByNameRealm(ch.Name, ch.SyncKey, out found))
            return found;

        return null;
    }

    private void EnsureSyncIndex()
    {
        if (_syncByKey.Count > 0)
            return;
        RebuildSyncIndex();
    }

    private bool TryGetSyncCharacter(string? key, out WowCharacterData found)
    {
        found = null!;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (_syncByKey.TryGetValue(key, out found!))
            return true;

        if (_syncByNormalizedKey.TryGetValue(CartoCharacterSyncKey.Normalize(key), out found!))
            return true;

        return false;
    }

    private bool TryGetSyncByNameRealm(string name, string? syncKey, out WowCharacterData found)
    {
        found = null!;
        var realm = ExtractRealmFromSyncKey(syncKey);
        foreach (var account in GetCachedWowSyncAccounts())
        {
            foreach (var c in account.Characters)
            {
                if (!name.Equals(c.Name, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (realm != null && !realm.Equals(c.Realm, StringComparison.OrdinalIgnoreCase))
                    continue;
                found = c;
                return true;
            }
        }

        return false;
    }

    private static string? ExtractRealmFromSyncKey(string? syncKey)
    {
        if (string.IsNullOrWhiteSpace(syncKey))
            return null;
        var dash = syncKey.IndexOf('-');
        return dash < 0 ? null : syncKey[(dash + 1)..].Trim();
    }

    private void RebuildSyncIndex()
    {
        _syncByKey.Clear();
        _syncByNormalizedKey.Clear();
        foreach (var account in GetCachedWowSyncAccounts())
        {
            foreach (var c in account.Characters)
            {
                RegisterSyncCharacter(c.Key, c);
                if (!string.IsNullOrEmpty(c.StorageKey))
                    RegisterSyncCharacter(c.StorageKey, c);
            }
        }
    }

    private void RegisterSyncCharacter(string key, WowCharacterData c)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        _syncByKey[key] = c;
        _syncByNormalizedKey[CartoCharacterSyncKey.Normalize(key)] = c;
    }

    private Dictionary<string, CartoCharacterExtras> BuildExtrasByKey()
    {
        var result = new Dictionary<string, CartoCharacterExtras>(StringComparer.OrdinalIgnoreCase);
        foreach (var extra in _data.CharacterExtras)
        {
            if (string.IsNullOrWhiteSpace(extra.SyncKey))
                continue;

            result[extra.SyncKey] = extra;
            var norm = CartoCharacterSyncKey.Normalize(extra.SyncKey);
            if (!result.ContainsKey(norm))
                result[norm] = extra;
        }

        return result;
    }

    private static CartoCharacterExtras? ResolveCharacterExtras(
        Dictionary<string, CartoCharacterExtras> extrasByKey,
        WowCharacterData sync)
    {
        if (extrasByKey.TryGetValue(sync.Key, out var extras)
            || (!string.IsNullOrEmpty(sync.StorageKey) && extrasByKey.TryGetValue(sync.StorageKey, out extras)))
        {
            if (!CartoCharacterSyncKey.Equals(extras.SyncKey, sync.Key))
                extras.SyncKey = sync.Key;
            return extras;
        }

        var norm = CartoCharacterSyncKey.Normalize(sync.Key);
        if (extrasByKey.TryGetValue(norm, out extras))
        {
            extras.SyncKey = sync.Key;
            return extras;
        }

        if (!string.IsNullOrEmpty(sync.StorageKey))
        {
            norm = CartoCharacterSyncKey.Normalize(sync.StorageKey);
            if (extrasByKey.TryGetValue(norm, out extras))
            {
                extras.SyncKey = sync.Key;
                return extras;
            }
        }

        return null;
    }

    private List<WowAccountData> GetCachedWowSyncAccounts()
    {
        if (_wowSyncCache != null)
            return _wowSyncCache;

        _wowSyncCache = TryReadWowSyncAccounts();
        RebuildSyncIndex();
        return _wowSyncCache;
    }

    private void CleanupAccountsFromWowSync()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var sync in GetCachedWowSyncAccounts())
            counts[sync.SourceAccountName] = sync.Characters.Count;

        CartoUserMigration.CleanupAccounts(_data, counts);
        ApplyConfiguredAccountSettings();
    }

    private List<WowAccountData> TryReadWowSyncAccounts()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_wowSyncService.WowPath))
                return [];
            return _wowSyncService.ReadAllAccounts(_data.AccountSettings);
        }
        catch
        {
            return [];
        }
    }

    private void MigrateLegacyCharacterData()
    {
        if (_data.CharacterExtras.Count > 0 || _data.Characters.Count == 0)
            return;

        foreach (var legacy in _data.Characters)
        {
            _data.CharacterExtras.Add(CartoSyncMapper.MigrateLegacyCharacter(legacy));
            if (!string.IsNullOrEmpty(legacy.SyncKey) || !string.IsNullOrEmpty(legacy.Name))
                _data.CharacterProfiles.Add(CartoSyncMapper.MigrateLegacyProfile(legacy));
        }

        _data.Characters.Clear();
    }

    /// <summary>Importe catégorie + note depuis characterExtras (ancien format) vers characterProfiles.</summary>
    private void MigrateCharacterProfiles()
    {
        _data.CharacterProfiles ??= [];
        var byKey = _data.CharacterProfiles
            .Where(p => !string.IsNullOrEmpty(p.SyncKey))
            .ToDictionary(p => p.SyncKey, StringComparer.OrdinalIgnoreCase);

        foreach (var extra in _data.CharacterExtras)
        {
            if (string.IsNullOrEmpty(extra.SyncKey) || byKey.ContainsKey(extra.SyncKey))
                continue;

            var hasLegacyCategory = extra.Status != CharacterStatus.Reroll;
            var hasLegacyNote = !string.IsNullOrWhiteSpace(extra.Note);
            if (!hasLegacyCategory && !hasLegacyNote)
                continue;

            var profile = new CartoCharacterProfile
            {
                SyncKey = extra.SyncKey,
                Category = extra.Status,
                Note = extra.Note ?? ""
            };
            _data.CharacterProfiles.Add(profile);
            byKey[extra.SyncKey] = profile;
        }
    }

    private void MigratePlacedOnMapFlags()
    {
        foreach (var extra in _data.CharacterExtras)
        {
            if (extra.IsPlacedOnMap) continue;
            if (extra.HasCustomMapPosition || extra.MapX > 0.001 || extra.MapY > 0.001)
                extra.IsPlacedOnMap = true;
        }
    }

    /// <summary>Supprime les positions carte sauvegardées hors addon (pile / drag manuel).</summary>
    /// <summary>Retire les drapeaux « sur carte » posés en masse (sans placement manuel ni coords addon).</summary>
    private void MigrateClearBulkMapPlacementFlags()
    {
        var changed = false;
        foreach (var extra in _data.CharacterExtras)
        {
            if (!extra.IsPlacedOnMap || extra.HasCustomMapPosition)
                continue;

            extra.IsPlacedOnMap = false;
            changed = true;
        }

        if (changed)
            _cartoService.Save(_data);
    }

    private void MigrateStripNonAddonMapPositions()
    {
        var changed = false;
        foreach (var extra in _data.CharacterExtras)
        {
            if (!extra.HasCustomMapPosition && extra.MapX == 0 && extra.MapY == 0)
                continue;

            extra.HasCustomMapPosition = false;
            extra.MapX = 0;
            extra.MapY = 0;
            changed = true;
        }

        if (changed)
            _cartoService.Save(_data);
    }

    private void ApplyConfiguredAccountSettings()
    {
        foreach (var account in _data.Accounts)
        {
            var folderKey = GetSourceFolderForAccount(account);
            if (folderKey == null || !_data.AccountSettings.TryGetValue(folderKey, out var cfg))
                continue;

            if (!string.IsNullOrWhiteSpace(cfg.DisplayName))
                account.Name = cfg.DisplayName;

            account.IsHidden = false;
        }
    }

    [ObservableProperty]
    private ObservableCollection<CartoUser> _cartoUsers = [];

    /// <summary>Propriétaires locaux autres que « Moi » (paramètres).</summary>
    [ObservableProperty]
    private ObservableCollection<CartoUser> _otherCartoOwners = [];

    [ObservableProperty]
    private string _newCartoOwnerName = "";

    [ObservableProperty]
    private string _cartoOwnerStatusMessage = "";

    public void RefreshCartoUserCollections()
    {
        CartoUsers = new ObservableCollection<CartoUser>(GetOrderedUsers());
        OtherCartoOwners = new ObservableCollection<CartoUser>(
            CartoUsers.Where(u => !IsDefaultCartoUser(u)));
    }

    [RelayCommand]
    private void AddCartoOwner()
    {
        var name = NewCartoOwnerName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            CartoOwnerStatusMessage = "Indiquez un nom de propriétaire.";
            return;
        }

        if (name.Equals(CartoUserMigration.DefaultUserName, StringComparison.OrdinalIgnoreCase))
        {
            CartoOwnerStatusMessage = "« Moi » est réservé à vos comptes.";
            return;
        }

        if (_data.Users.Any(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            CartoOwnerStatusMessage = $"Le propriétaire « {name} » existe déjà.";
            return;
        }

        var maxOrder = _data.Users.Count > 0 ? _data.Users.Max(u => u.SortOrder) : 0;
        _data.Users.Add(new CartoUser { Name = name, SortOrder = maxOrder + 1 });
        NewCartoOwnerName = string.Empty;
        CartoOwnerStatusMessage = $"Propriétaire « {name} » ajouté.";
        FinishCartoOwnerChange();
    }

    [RelayCommand]
    private void RemoveCartoOwner(CartoUser? user)
    {
        if (user == null || IsDefaultCartoUser(user))
            return;

        var moiId = GetDefaultUserId();
        if (moiId == null)
            return;

        foreach (var cfg in _data.AccountSettings.Values)
        {
            if (cfg.UserId == user.Id)
                cfg.UserId = moiId;
        }

        _data.Users.RemoveAll(u => u.Id == user.Id);
        CartoOwnerStatusMessage = $"Propriétaire « {user.Name} » retiré.";
        FinishCartoOwnerChange();
    }

    private void FinishCartoOwnerChange()
    {
        CartoUserMigration.ReindexUsers(_data);
        RefreshCartoUserCollections();
        RefreshAccountSettingRows();
        Save();
        RefreshRosterTree();
    }

    public IEnumerable<CartoUser> GetOrderedUsers() =>
        _data.Users.OrderBy(u => u.SortOrder).ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase);

    public string? GetUserIdForAccount(WowAccount? account)
    {
        var folder = GetSourceFolderForAccount(account);
        return folder == null
            ? null
            : CartoAccountSettings.ResolveUserId(folder, _data.AccountSettings, _data.Users);
    }

    public CartoUser? GetUserById(string? userId) =>
        string.IsNullOrWhiteSpace(userId)
            ? null
            : _data.Users.FirstOrDefault(u => u.Id == userId);

    public string? GetUserIdForCharacter(WowCharacter ch)
    {
        var account = Accounts.FirstOrDefault(a => a.Id == ch.AccountId);
        return GetUserIdForAccount(account) ?? GetDefaultUserId();
    }

    public string GetUserDisplayName(string? userId)
    {
        var user = GetUserById(userId);
        return user?.Name ?? "Sans utilisateur";
    }

    public int GetAccountCountForUser(string userId) =>
        Accounts.Count(a => GetUserIdForAccount(a) == userId);

    public bool ShouldShowAccountNameForCharacter(WowCharacter ch)
    {
        var userId = GetUserIdForCharacter(ch);
        return userId != null && GetAccountCountForUser(userId) > 1;
    }

    /// <summary>Nom affiché du compte WTF lié au personnage (paramètres Comptes).</summary>
    public string? GetCharacterAccountDisplayName(WowCharacter ch)
    {
        if (string.IsNullOrEmpty(ch.AccountId))
            return null;

        var account = Accounts.FirstOrDefault(a => a.Id == ch.AccountId);
        if (account == null)
            return null;

        var folder = GetSourceFolderForAccount(account);
        if (!string.IsNullOrWhiteSpace(folder))
            return GetAccountDisplayName(folder);

        return string.IsNullOrWhiteSpace(account.Name) ? null : account.Name.Trim();
    }

    public long GetUserTotalGoldCopper(string userId, IReadOnlyList<WowCharacter>? scopeChars = null)
    {
        long total = 0;
        foreach (var ch in scopeChars ?? Characters)
        {
            if (GetUserIdForCharacter(ch) != userId)
                continue;

            var sync = FindWowSyncCharacter(ch);
            if (sync != null)
                total += sync.Gold;
        }

        return total;
    }

    public IReadOnlyList<WowCharacter> GetLocalCharactersForUserCategory(string userId, CharacterStatus category)
    {
        var statuses = StatusesForRosterCategory(category).ToHashSet();
        return Characters
            .Where(c => GetUserIdForCharacter(c) == userId
                        && statuses.Contains(c.Status))
            .ToList();
    }

    public long GetAccountGoldCopper(string sourceFolder)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder))
            return 0;

        long total = 0;
        foreach (var ch in Characters)
        {
            var account = Accounts.FirstOrDefault(a => a.Id == ch.AccountId);
            if (account == null
                || !sourceFolder.Equals(account.SourceFolder, StringComparison.OrdinalIgnoreCase))
                continue;

            var sync = FindWowSyncCharacter(ch);
            if (sync != null)
                total += sync.Gold;
        }

        return total;
    }

    public string GetUserCooldownSummary(string userId, IReadOnlyList<WowCharacter>? scopeChars = null)
    {
        var ready = new List<string>();
        var running = 0;
        foreach (var ch in scopeChars ?? Characters)
        {
            if (GetUserIdForCharacter(ch) != userId)
                continue;

            foreach (var cd in ch.Cooldowns)
            {
                if (cd.LastUsed == null)
                    continue;

                if (cd.IsReady)
                    ready.Add($"{ch.Name}:{CdShortName(cd.Type)}");
                else
                    running++;
            }
        }

        if (ready.Count == 0 && running == 0)
            return "";

        if (ready.Count > 0 && running > 0)
            return $"CD prêts {ready.Count} · en cours {running}";

        if (ready.Count > 0)
            return ready.Count <= 2
                ? $"CD prêt {string.Join(", ", ready)}"
                : $"CD prêts ({ready.Count})";

        return $"CD en cours ({running})";
    }

    private static string CdShortName(CooldownType t) => t switch
    {
        CooldownType.Arcanite => "Arc",
        CooldownType.Transmute_Elementaire => "Él",
        CooldownType.Mooncloth => "Lun",
        CooldownType.Sel_raffine => "Sel",
        _ => t.ToString()
    };

    public CartoCategoryPolicy GetCategoryPolicy(string userId, CharacterStatus category)
    {
        var policy = _data.CategoryPolicies.FirstOrDefault(p =>
            p.UserId == userId && p.Category == category);
        if (policy != null)
            return policy;

        policy = new CartoCategoryPolicy { UserId = userId, Category = category };
        _data.CategoryPolicies.Add(policy);
        return policy;
    }

    public long GetCategoryGoldCopper(IReadOnlyList<WowCharacter> chars)
    {
        long total = 0;
        foreach (var ch in chars)
        {
            var sync = FindWowSyncCharacter(ch);
            if (sync != null)
                total += sync.Gold;
        }

        return total;
    }

    public int GetCategorySoulShardCount(IReadOnlyList<WowCharacter> chars) =>
        chars.Where(c => c.Class == WowClass.Demoniste).Sum(c => c.ShardCount);

    public Dictionary<QuestItemType, int> GetCategoryQuestItemCounts(IReadOnlyList<WowCharacter> chars)
    {
        var counts = new Dictionary<QuestItemType, int>();
        foreach (var ch in chars)
        {
            foreach (var q in ch.QuestItems.Where(q => q.HasItem))
                counts[q.Type] = counts.GetValueOrDefault(q.Type) + 1;
        }

        return counts;
    }

    public (WowCharacter Character, int ShardCount)? GetTpBoyWithMinimumShards(IReadOnlyList<WowCharacter> chars)
    {
        var tpBoys = chars
            .Where(c => c.Status == CharacterStatus.TpBoy && c.Class == WowClass.Demoniste)
            .OrderBy(c => c.ShardCount)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tpBoys.Count == 0)
            return null;

        var best = tpBoys[0];
        return (best, best.ShardCount);
    }

    public static bool IsDefaultCartoUser(CartoUser user) =>
        user.Name.Equals(CartoUserMigration.DefaultUserName, StringComparison.OrdinalIgnoreCase);

    public string? GetCharacterSyncLabel(WowCharacter ch)
    {
        var sync = FindWowSyncCharacter(ch);
        return string.IsNullOrWhiteSpace(sync?.LastUpdate) ? null : sync.LastUpdate.Trim();
    }

    public string? GetCharacterZoneLabel(WowCharacter ch)
    {
        var sync = FindWowSyncCharacter(ch);
        if (sync == null)
            return null;

        var zone = WowZoneLocalization.FormatDisplay(sync.Zone, sync.SubZone);
        return string.IsNullOrWhiteSpace(zone) ? null : zone.Trim();
    }

    public double? GetCharacterXpPercent(WowCharacter ch)
    {
        if (ch.Level >= 60)
            return null;

        var sync = FindWowSyncCharacter(ch);
        if (sync == null || sync.XpPercent < 0)
            return null;

        return sync.XpPercent;
    }

    public string? GetCategoryLatestSyncLabel(IReadOnlyList<WowCharacter> chars)
    {
        string? latest = null;
        foreach (var ch in chars)
        {
            var label = GetCharacterSyncLabel(ch);
            if (string.IsNullOrWhiteSpace(label))
                continue;

            if (latest == null || string.Compare(label, latest, StringComparison.Ordinal) > 0)
                latest = label;
        }

        return latest;
    }

    public string? GetCategoryZoneSummary(IReadOnlyList<WowCharacter> chars)
    {
        var zones = chars
            .Select(GetCharacterZoneLabel)
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        return zones.Count == 0 ? null : string.Join(" · ", zones);
    }

    public string? GetCategoryNotePreview(IReadOnlyList<WowCharacter> chars) =>
        chars.Select(c => c.Note?.Trim())
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

    private string? FindSourceFolderForAccountName(string accountName)
    {
        foreach (var (folder, cfg) in _data.AccountSettings)
        {
            if (folder.Equals(accountName, StringComparison.OrdinalIgnoreCase)
                || cfg.DisplayName.Equals(accountName, StringComparison.OrdinalIgnoreCase))
                return folder;
        }

        foreach (var sync in GetCachedWowSyncAccounts())
        {
            if (sync.AccountName.Equals(accountName, StringComparison.OrdinalIgnoreCase))
                return sync.SourceAccountName;
        }

        return null;
    }

    public string GetAccountDisplayName(string sourceFolder) =>
        CartoAccountSettings.ResolveDisplayName(sourceFolder, _data.AccountSettings);

    public string? GetSourceFolderForAccount(WowAccount? account)
    {
        if (account == null)
            return null;

        if (!string.IsNullOrWhiteSpace(account.SourceFolder))
            return account.SourceFolder.Trim();

        return FindSourceFolderForAccountName(account.Name);
    }

    public bool HasLocalCharacterWithSyncKey(string syncKey) =>
        !string.IsNullOrWhiteSpace(syncKey)
        && Characters.Any(c => syncKey.Equals(c.SyncKey, StringComparison.OrdinalIgnoreCase));

    [ObservableProperty]
    private ObservableCollection<AccountSettingRow> _accountSettingRows = [];

    /// <summary>Ouvre la popup comptes : copie de secours pour Annuler.</summary>
    public void BeginAccountSettingsEdit()
    {
        _accountSettingsEditSnapshot = CloneAccountSettings(_data.AccountSettings);
        RefreshAccountSettingRows();
    }

    public void CancelAccountSettingsEdit()
    {
        if (_accountSettingsEditSnapshot != null)
        {
            _data.AccountSettings = CloneAccountSettings(_accountSettingsEditSnapshot);
            _data.AccountDisplayNames = _data.AccountSettings.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.DisplayName,
                StringComparer.OrdinalIgnoreCase);
            ApplyAccountSettingsToRuntime();
        }

        _accountSettingsEditSnapshot = null;
        RefreshAccountSettingRows();
    }

    /// <summary>Scanne les dossiers WTF WowSync et fusionne avec la config sauvegardée.</summary>
    public void RefreshAccountSettingRows()
    {
        var pendingEdits = AccountSettingRows.ToDictionary(
            r => r.SourceFolder,
            r => (r.DisplayName, r.UserId),
            StringComparer.OrdinalIgnoreCase);

        var syncAccounts = TryReadWowSyncAccounts();
        var syncByFolder = syncAccounts.ToDictionary(
            a => a.SourceAccountName,
            StringComparer.OrdinalIgnoreCase);
        var wtfFolders = _wowSyncService.ListWtfAccountFolderNames();
        var rows = new List<AccountSettingRow>();
        var users = GetOrderedUsers().ToList();

        foreach (var folder in wtfFolders)
        {
            syncByFolder.TryGetValue(folder, out var sync);
            _data.AccountSettings.TryGetValue(folder, out var cfg);
            var row = AccountSettingRow.From(
                folder,
                cfg,
                sync?.Characters.Count ?? 0,
                GetAccountGoldCopper(folder),
                users);
            if (pendingEdits.TryGetValue(folder, out var edit))
            {
                row.DisplayName = edit.DisplayName;
                row.UserId = edit.UserId;
                row.RefreshOwnerDisplayName(users);
            }

            rows.Add(row);
        }

        foreach (var (folder, cfg) in _data.AccountSettings)
        {
            if (rows.Any(r => r.SourceFolder.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (CartoUserMigration.GhostAccountFolders.Contains(folder))
                continue;

            var orphan = AccountSettingRow.From(folder, cfg, 0, GetAccountGoldCopper(folder), users);
            if (pendingEdits.TryGetValue(folder, out var edit))
            {
                orphan.DisplayName = edit.DisplayName;
                orphan.UserId = edit.UserId;
                orphan.RefreshOwnerDisplayName(users);
            }

            rows.Add(orphan);
        }

        AccountSettingRows = new ObservableCollection<AccountSettingRow>(rows);
        RefreshCartoUserCollections();
    }

    public void CloseSettingsPanelAfterSave()
    {
        SaveAccountSettingsFromRows();
        _settingsPanelClosingAfterSave = true;
        IsSettingsPanelOpen = false;
        _settingsPanelClosingAfterSave = false;
    }

    public void SaveAccountSettingsFromRows()
    {
        var merged = new Dictionary<string, CartoAccountConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in AccountSettingRows)
        {
            _data.AccountSettings.TryGetValue(row.SourceFolder, out var previous);
            merged[row.SourceFolder] = row.ToConfig(previous);
        }

        foreach (var (folder, cfg) in _data.AccountSettings)
        {
            if (!merged.ContainsKey(folder))
                merged[folder] = cfg;
        }

        _data.AccountSettings = merged;
        _data.AccountDisplayNames = _data.AccountSettings.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.DisplayName,
            StringComparer.OrdinalIgnoreCase);

        ApplyAccountSettingsToRuntime();
        Save();
        _accountSettingsEditSnapshot = null;
    }

    /// <summary>Applique noms d'affichage et groupes utilisateur sans recharger WowSync ni la carte.</summary>
    private void ApplyAccountSettingsToRuntime()
    {
        ApplyConfiguredAccountSettings();
        AccountIdToNameConverter.Accounts = [.. Accounts];
        CharacterSyncGoldConverter.Vm = this;
        RefreshCartoUserCollections();
    }

    private static Dictionary<string, CartoAccountConfig> CloneAccountSettings(
        IReadOnlyDictionary<string, CartoAccountConfig> source)
    {
        var clone = new Dictionary<string, CartoAccountConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var (folder, cfg) in source)
        {
            clone[folder] = new CartoAccountConfig
            {
                DisplayName = cfg.DisplayName,
                UserId = cfg.UserId,
                Scope = cfg.Scope,
                FriendLabel = cfg.FriendLabel,
                IsHiddenOnMap = cfg.IsHiddenOnMap
            };
        }

        return clone;
    }

    private void PersistDataBeforeSave()
    {
        _data.Accounts = [.. Accounts];
        _data.Timers = [.. Timers];
        MergeCharacterProfilesFromMemory();
        MergeCharacterExtrasFromMemory();
        _data.Characters = [.. Characters];
    }

    /// <summary>Fusionne les catégories en mémoire — ne vide pas le fichier si la liste UI n'est pas encore chargée.</summary>
    private void MergeCharacterProfilesFromMemory()
    {
        _data.CharacterProfiles ??= [];
        var byKey = _data.CharacterProfiles
            .Where(p => !string.IsNullOrWhiteSpace(p.SyncKey))
            .ToDictionary(p => p.SyncKey, StringComparer.OrdinalIgnoreCase);

        foreach (var ch in Characters.Where(c => !string.IsNullOrWhiteSpace(c.SyncKey)))
            byKey[ch.SyncKey] = CartoSyncMapper.ToProfile(ch);

        _data.CharacterProfiles = byKey.Values.ToList();
    }

    private void MergeCharacterExtrasFromMemory()
    {
        _data.CharacterExtras ??= [];
        var byKey = _data.CharacterExtras
            .Where(e => !string.IsNullOrWhiteSpace(e.SyncKey))
            .ToDictionary(e => e.SyncKey, StringComparer.OrdinalIgnoreCase);

        foreach (var ch in Characters.Where(c => !string.IsNullOrWhiteSpace(c.SyncKey)))
            byKey[ch.SyncKey] = CartoSyncMapper.ToExtras(ch);

        _data.CharacterExtras = byKey.Values.ToList();
    }

    public const double MinMapZoom = 0.15;
    public const double MaxMapZoom = 8.0;

    /// <summary>Facteur multiplicatif pour une cran de molette (120 = 1 cran Windows).</summary>
    public static double WheelZoomFactorFromDelta(int delta) =>
        Math.Pow(1.12, delta / 120.0);

    /// <summary>Zoom ancré sur un point du viewport (coordonnées MapBorder).</summary>
    public void ApplyZoomAt(double viewportX, double viewportY, double factor)
    {
        var oldZoom = MapZoom;
        var newZoom = Math.Clamp(oldZoom * factor, MinMapZoom, MaxMapZoom);
        if (Math.Abs(newZoom - oldZoom) < 1e-9)
            return;

        var contentX = (viewportX - MapOffsetX) / oldZoom;
        var contentY = (viewportY - MapOffsetY) / oldZoom;
        MapZoom = newZoom;
        MapOffsetX = viewportX - contentX * newZoom;
        MapOffsetY = viewportY - contentY * newZoom;
    }

    /// <summary>Empêche de perdre la carte hors écran ; centre si la carte est plus petite que la zone visible.</summary>
    /// <param name="dockOverlayRight">Largeur réservée à droite (volet en surcouche) — le centrage / pan utilise la bande visible à gauche.</param>
    public void ClampMapPan(double viewportW, double viewportH, double mapPixelW, double mapPixelH, double dockOverlayRight = 0)
    {
        if (viewportW < 1 || viewportH < 1 || mapPixelW < 1 || mapPixelH < 1)
            return;

        var overlay = Math.Max(0, dockOverlayRight);
        var visW = Math.Max(1, viewportW - overlay);
        var scaledW = mapPixelW * MapZoom;
        var scaledH = mapPixelH * MapZoom;
        const double edgePad = 24;

        if (scaledW <= visW)
        {
            var maxX = visW - scaledW;
            MapOffsetX = Math.Clamp(MapOffsetX, -edgePad, maxX + edgePad);
        }
        else
            MapOffsetX = Math.Clamp(MapOffsetX, visW - scaledW - edgePad, edgePad);

        if (scaledH <= viewportH)
        {
            var maxY = viewportH - scaledH;
            MapOffsetY = Math.Clamp(MapOffsetY, -edgePad, maxY + edgePad);
        }
        else
            MapOffsetY = Math.Clamp(MapOffsetY, viewportH - scaledH - edgePad, edgePad);
    }

    [RelayCommand]
    private void ClearFilters()
    {
        FilterClass = null;
        FilterLevelMin = null;
        FilterLevelMax = null;
        FilterAccountId = null;
        FilterName = string.Empty;
        FilterStatus = null;
    }

    private void CheckCooldownNotifications()
    {
        foreach (var character in Characters)
        {
            foreach (var cd in character.Cooldowns)
            {
                if (cd.LastUsed == null || !cd.IsReady) continue;
                var key = $"{character.Id}_{cd.Type}_{cd.LastUsed:O}";
                if (_alertedCooldownKeys.Add(key))
                    CooldownReady?.Invoke(this, (character, cd));
            }
        }
    }

    public event EventHandler<(WowCharacter Character, CooldownEntry Cooldown)>? CooldownReady;


    // ─── Timers ───────────────────────────────────────────────

    public event Action<MapTimer>? TimerExpired;

    [RelayCommand]
    private void StartPlaceTimer()
    {
        IsPlacingTimer = true;
    }

    public void PlaceTimerAt(double x, double y)
    {
        var totalSeconds = NewTimerHours * 3600 + NewTimerMinutes * 60 + NewTimerSeconds;
        if (totalSeconds <= 0) totalSeconds = 60;
        var timer = new MapTimer
        {
            Label = NewTimerLabel,
            MapX = x, MapY = y,
            DurationSeconds = totalSeconds,
            StartedAt = DateTime.Now,
            IsRunning = true
        };
        Timers.Add(timer);
        IsPlacingTimer = false;
        Save();
    }

    [RelayCommand]
    private void RestartTimer(MapTimer t)
    {
        t.StartedAt = DateTime.Now;
        t.IsRunning = true;
        Save();
    }

    [RelayCommand]
    private void StopTimer(MapTimer t)
    {
        if (t.IsRunning && t.StartedAt.HasValue)
            t.PausedRemainingSeconds = Math.Max(0, (int)t.Remaining.TotalSeconds);
        t.IsRunning = false;
        Save();
    }

    [RelayCommand]
    private void ResumeTimer(MapTimer t)
    {
        var remaining = t.PausedRemainingSeconds ?? t.DurationSeconds;
        t.DurationSeconds = remaining;
        t.StartedAt = DateTime.Now;
        t.PausedRemainingSeconds = null;
        t.IsRunning = true;
        Save();
    }

    [RelayCommand]
    private void RemoveTimer(MapTimer t)
    {
        Timers.Remove(t);
        Save();
    }

    private readonly HashSet<string> _alertedTimerIds = [];
    private readonly HashSet<string> _alertedCooldownKeys = [];

    private void CheckTimerAlerts()
    {
        foreach (var t in Timers.Where(t => t.IsRunning && t.IsExpired))
        {
            if (_alertedTimerIds.Add(t.Id))
            {
                t.IsRunning = false;
                TimerExpired?.Invoke(t);
                Save();
            }
        }
    }

    public bool NeedsMigration =>
        Characters.Any(c => c.MapX > 1 || c.MapY > 1) ||
        Timers.Any(t => t.MapX > 1 || t.MapY > 1);

    public void MigrateCoordinates(double imageWidth, double imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0) return;
        foreach (var ch in Characters)
        {
            if (ch.MapX > 1 || ch.MapY > 1)
            {
                ch.MapX /= imageWidth;
                ch.MapY /= imageHeight;
            }
        }
        foreach (var t in Timers)
        {
            if (t.MapX > 1 || t.MapY > 1)
            {
                t.MapX /= imageWidth;
                t.MapY /= imageHeight;
            }
        }
        Save();
    }

    public void Save()
    {
        PersistDataBeforeSave();
        AccountIdToNameConverter.Accounts = [.. Accounts];
        CharacterSyncGoldConverter.Vm = this;
        _cartoService.Save(BuildDiskSnapshot());
    }

    private CartoData BuildDiskSnapshot() => new()
    {
        Users = _data.Users,
        CategoryPolicies = _data.CategoryPolicies,
        AccountSettings = _data.AccountSettings,
        AccountDisplayNames = _data.AccountDisplayNames,
        Accounts = _data.Accounts,
        CharacterProfiles = _data.CharacterProfiles,
        CharacterExtras = _data.CharacterExtras,
        Timers = _data.Timers,
        Characters = []
    };

    private void SaveSettings() => _settingsService.Save(_settings);

    [RelayCommand]
    private void ToggleCharacterMapVisibility(WowCharacter character)
    {
        character.IsHidden = !character.IsHidden;
        ApplyFilters();
        OnPropertyChanged(nameof(FilteredCharacters));
        OnPropertyChanged(nameof(OverlayChanged));
        Save();
    }

    [RelayCommand]
    private void ToggleCharacterLock(WowCharacter character)
    {
        character.IsLocked = !character.IsLocked;
        Save();
    }

    // —— Gestionnaire de zones (rectangles sur la carte) ——

    public ObservableCollection<CartoZoneRectItem> ZoneRects { get; } = [];

    /// <summary>Zones monde calibrées (hors capitales), pour la liste du volet Zones.</summary>
    public ObservableCollection<CartoZoneRectItem> WorldZoneRects { get; } = [];

    /// <summary>Rectangles de calibration sur les 6 mini-cartes de capitales.</summary>
    public ObservableCollection<CartoZoneRectItem> CapitalZoneRects { get; } = [];

    public ObservableCollection<CartoDungeonMarker> DungeonMarkers { get; } = [];

    /// <summary>Lieux-dits déjà posés sur Azeroth (liste du volet Zones).</summary>
    public ObservableCollection<CartoDungeonMarker> PlacedDungeonMarkers { get; } = [];

    public IReadOnlyList<ClassicEraMapProjection.ZoneCatalogEntry> ZoneCatalog
        => ClassicEraMapProjection.GetZoneCatalog();

    public IReadOnlyList<CartoDungeonCatalog.DungeonEntry> DungeonCatalog
        => CartoDungeonCatalog.All;

    public ObservableCollection<ZoneCatalogListItem> ZoneCatalogItems { get; } = [];

    public ObservableCollection<DungeonCatalogListItem> DungeonCatalogItems { get; } = [];

    [ObservableProperty]
    private bool _isZonesPanelOpen;

    [ObservableProperty]
    private bool _isZoneEditMode;

    [ObservableProperty]
    private CartoZoneRectItem? _selectedZoneRect;

    [ObservableProperty]
    private CartoDungeonMarker? _selectedDungeonMarker;

    [ObservableProperty]
    private int? _zoneToAddMapId;

    /// <summary>True après ＋ zones : le rectangle n'est créé qu'au clic sur la carte.</summary>
    [ObservableProperty]
    private bool _isPlacingZone;

    /// <summary>Ignore les clics carte juste après changement de combo (évite le clic fantôme à la fermeture).</summary>
    public DateTime SuppressCapitalMapClickUntilUtc { get; private set; }

    public void SuppressCapitalMapClicks(int milliseconds = 1200) =>
        SuppressCapitalMapClickUntilUtc = DateTime.UtcNow.AddMilliseconds(milliseconds);

    [ObservableProperty]
    private string? _dungeonToPlaceKey;

    [ObservableProperty]
    private bool _isPlacingDungeonMarker;

    [ObservableProperty]
    private string? _zonePanelStatusMessage;

    [ObservableProperty]
    private bool _showMapOverlays = CartoRuntimeOptions.ShowMapOverlays;

    partial void OnShowMapOverlaysChanged(bool value)
    {
        CartoRuntimeOptions.ShowMapOverlays = value;
        if (IsZonesPanelOpen)
            ZonePanelStatusMessage = value
                ? "Zones et repères visibles sur la carte."
                : "Calques masqués — cochez pour afficher.";
        OnPropertyChanged(nameof(OverlayChanged));
    }

    partial void OnIsZonesPanelOpenChanged(bool value)
    {
        IsZoneEditMode = value;
        if (value)
        {
            IsPlacingCharacter = false;
            IsPlacingTimer = false;
            if (!_zonePanelDataLoaded)
            {
                LoadZoneRects();
                _zonePanelDataLoaded = true;
            }

            LoadDungeonMarkers();
            RefreshZoneCatalogItems();
            SyncSelectedZoneFromCombo();
            ShowMapOverlays = CartoRuntimeOptions.ShowMapOverlays;
            ZonePanelStatusMessage = ShowMapOverlays
                ? "Calques visibles sur WowMap.png (une seule image)."
                : "Carte nue : cochez « Afficher sur la carte » pour voir vos rectangles et repères.";
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                RefreshUnplacedCharacters,
                System.Windows.Threading.DispatcherPriority.Background);
        }
        else
        {
            IsPlacingDungeonMarker = false;
            ZonePanelStatusMessage = null;
        }

        OnPropertyChanged(nameof(OverlayChanged));
    }

    partial void OnIsZoneEditModeChanged(bool value)
    {
        if (value)
        {
            IsPlacingCharacter = false;
            IsPlacingTimer = false;
            SyncSelectedZoneFromCombo();
        }

        OnPropertyChanged(nameof(OverlayChanged));
    }

    partial void OnSelectedZoneRectChanged(CartoZoneRectItem? value)
    {
        if (value != null)
        {
            ZoneToAddMapId = value.MapId;
            SelectedDungeonMarker = null;
            ZonePanelStatusMessage = $"Zone : {value.DisplayName}";
        }
        else if (IsZonesPanelOpen)
            ZonePanelStatusMessage = null;

        OnPropertyChanged(nameof(OverlayChanged));
    }

    partial void OnSelectedDungeonMarkerChanged(CartoDungeonMarker? value)
    {
        if (value != null)
        {
            SelectedZoneRect = null;
            ZonePanelStatusMessage = $"Lieu-dit : {value.DisplayName}";
        }
        else if (IsZonesPanelOpen && SelectedZoneRect == null)
            ZonePanelStatusMessage = null;

        OnPropertyChanged(nameof(OverlayChanged));
    }

    partial void OnIsPlacingDungeonMarkerChanged(bool value)
    {
        if (value)
        {
            IsPlacingCharacter = false;
            IsPlacingTimer = false;
            ZonePanelStatusMessage = "Clic sur la carte = placer le repère lieu-dit";
        }

        OnPropertyChanged(nameof(OverlayChanged));
    }

    partial void OnZoneToAddMapIdChanged(int? value) => SyncSelectedZoneFromCombo();

    partial void OnIsPlacingZoneChanged(bool value)
    {
        if (!value || !IsZonesPanelOpen || ZoneToAddMapId is not int mapId)
            return;

        var name = ClassicEraMapProjection.TryGetCatalogEntry(mapId, out var entry)
            ? entry.DisplayName
            : "la zone";
        ZonePanelStatusMessage =
            $"{name} : ＋ puis clic où vous voulez sur la carte (Azeroth ou capitales — libre).";
        OnPropertyChanged(nameof(OverlayChanged));
    }

    private void SyncSelectedZoneFromCombo()
    {
        if (ZoneToAddMapId is not > 0)
            return;

        var match = ZoneRects.FirstOrDefault(z => z.MapId == ZoneToAddMapId.Value);
        if (match != null && !ReferenceEquals(SelectedZoneRect, match))
            SelectedZoneRect = match;
    }

    /// <summary>Zones placées par l'utilisateur (fichier local uniquement — pas de rectangles intégrés).</summary>
    private void LoadZoneRects()
    {
        ZoneRects.Clear();
        var userRects = ZoneMapCalibration.LoadUserOverrides();
        var mapW = 1024;
        var mapH = 768;
        if (CartoMapPreloader.GetBitmap() is { PixelWidth: > 0, PixelHeight: > 0 } bmp)
        {
            mapW = bmp.PixelWidth;
            mapH = bmp.PixelHeight;
        }

        foreach (var (mapId, rect) in userRects.OrderBy(kv => kv.Key))
        {
            if (ClassicEraMapProjection.IsContinentMap(mapId))
                continue;

            if (!ClassicEraMapProjection.TryGetCatalogEntry(mapId, out var entry))
                continue;

            var left = rect.Left;
            var top = rect.Top;
            var width = rect.Width;
            var height = rect.Height;
            if (ClassicEraMapProjection.IsCapitalMap(mapId)
                && WowMapLayout.LooksLikeTileRelativeCapitalRect(left, top, width, height))
            {
                (left, top, width, height) = WowMapLayout.TileRelativeToFullMapNorm(
                    mapW, mapH, mapId, left, top, width, height);
            }

            ZoneRects.Add(new CartoZoneRectItem
            {
                MapId = mapId,
                NameFr = entry.NameFr,
                NameEn = entry.NameEn,
                DisplayName = entry.DisplayName,
                Left = left,
                Top = top,
                Width = width,
                Height = height
            });
        }

        RefreshWorldZoneRects();
        RefreshCapitalZoneRects();
        RefreshZoneCatalogItems();

        if (SelectedZoneRect == null || ZoneRects.All(z => !ReferenceEquals(z, SelectedZoneRect)))
            SelectedZoneRect = ZoneRects.FirstOrDefault();
    }

    private void RefreshWorldZoneRects()
    {
        WorldZoneRects.Clear();
        foreach (var z in ZoneRects
                     .Where(z => !ClassicEraMapProjection.IsCapitalMap(z.MapId))
                     .OrderBy(z => z.DisplayName, StringComparer.OrdinalIgnoreCase))
            WorldZoneRects.Add(z);
    }

    private void RefreshCapitalZoneRects()
    {
        CapitalZoneRects.Clear();
        foreach (var z in ZoneRects
                     .Where(z => ClassicEraMapProjection.IsCapitalMap(z.MapId))
                     .OrderBy(z => z.DisplayName, StringComparer.OrdinalIgnoreCase))
            CapitalZoneRects.Add(z);
    }

    private static bool IsWorldZonePlaced(int mapId, IEnumerable<CartoZoneRectItem> zoneRects) =>
        zoneRects.Any(z => z.MapId == mapId);

    private static bool IsCapitalPlaced(int mapId, IEnumerable<CartoZoneRectItem> zoneRects) =>
        zoneRects.Any(z => z.MapId == mapId);

    private bool IsDungeonPlaced(string key) =>
        DungeonMarkers.Any(m =>
            m.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
            && (m.MapX > 0 || m.MapY > 0));

    private void RefreshZoneCatalogItems()
    {
        ZoneCatalogItems.Clear();
        foreach (var z in ZoneCatalog
                     .Where(z => !ClassicEraMapProjection.IsContinentMap(z.MapId))
                     .OrderBy(z => z.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var placed = ZoneRects.Any(r => r.MapId == z.MapId);
            ZoneCatalogItems.Add(new ZoneCatalogListItem
            {
                MapId = z.MapId,
                DisplayName = placed ? $"{z.DisplayName} ✓" : z.DisplayName
            });
        }

        DungeonCatalogItems.Clear();
        foreach (var d in DungeonCatalog
                     .Where(d => !IsDungeonPlaced(d.Key))
                     .OrderBy(d => d.IsLieuDit ? 0 : 1)
                     .ThenBy(d => d.NameFr, StringComparer.OrdinalIgnoreCase))
        {
            DungeonCatalogItems.Add(new DungeonCatalogListItem
            {
                Key = d.Key,
                NameFr = d.NameFr,
                ParentZoneFr = d.ParentZoneFr,
                IsLieuDit = d.IsLieuDit
            });
        }

        SyncCatalogComboSelections();
    }

    private void SyncCatalogComboSelections()
    {
        if (ZoneToAddMapId is int zoneId && ZoneCatalogItems.All(i => i.MapId != zoneId))
            ZoneToAddMapId = ZoneCatalogItems.FirstOrDefault()?.MapId;

        if (!string.IsNullOrWhiteSpace(DungeonToPlaceKey)
            && DungeonCatalogItems.All(i => !i.Key.Equals(DungeonToPlaceKey, StringComparison.OrdinalIgnoreCase)))
        {
            DungeonToPlaceKey = DungeonCatalogItems.FirstOrDefault()?.Key;
        }
    }

    private void LoadDungeonMarkers()
    {
        DungeonMarkers.Clear();
        foreach (var m in DungeonMarkerStore.LoadAll()
                     .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase))
            DungeonMarkers.Add(m);

        RefreshZoneCatalogItems();
        RefreshPlacedDungeonMarkers();
    }

    private void RefreshPlacedDungeonMarkers()
    {
        PlacedDungeonMarkers.Clear();
        foreach (var m in DungeonMarkers
                     .Where(m => m.MapX > 0 || m.MapY > 0)
                     .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase))
            PlacedDungeonMarkers.Add(m);
    }

    private CartoDungeonMarker? GetOrCreateDungeonMarker(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        key = CartoDungeonMarkerResolver.NormalizeMarkerKey(key);
        var existing = DungeonMarkers.FirstOrDefault(m => m.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        if (!CartoDungeonCatalog.TryGet(key, out var entry))
            return null;

        return new CartoDungeonMarker
        {
            Key = entry.Key,
            NameFr = entry.NameFr
        };
    }

    [RelayCommand]
    private void AddZoneFromPanel()
    {
        SuppressCapitalMapClicks();
        SelectedZoneRect = null;

        if (ZoneToAddMapId is not > 0)
        {
            ZonePanelStatusMessage = "Choisissez une zone dans la liste, puis ＋.";
            return;
        }

        IsPlacingZone = true;
        OnPropertyChanged(nameof(OverlayChanged));
    }

    [RelayCommand]
    private void StartPlaceDungeonMarker()
    {
        if (string.IsNullOrWhiteSpace(DungeonToPlaceKey))
        {
            ZonePanelStatusMessage = "Choisissez un lieu-dit dans la liste, puis cliquez ＋.";
            return;
        }

        DungeonToPlaceKey = CartoDungeonMarkerResolver.NormalizeMarkerKey(DungeonToPlaceKey);
        SelectedDungeonMarker = GetOrCreateDungeonMarker(DungeonToPlaceKey);
        IsPlacingDungeonMarker = true;
    }

    public bool TryPlaceDungeonMarkerAt(double mapX, double mapY)
    {
        if (string.IsNullOrWhiteSpace(DungeonToPlaceKey))
            return false;

        var marker = GetOrCreateDungeonMarker(DungeonToPlaceKey);
        if (marker == null)
            return false;

        marker.MapX = Math.Clamp(mapX, 0, 1);
        marker.MapY = Math.Clamp(mapY, 0, 1);

        if (!DungeonMarkers.Contains(marker))
            DungeonMarkers.Add(marker);

        SortDungeonMarkers();
        SelectedDungeonMarker = marker;
        IsPlacingDungeonMarker = false;
        PersistDungeonMarkers();
        ApplyDungeonMarkersChanged();
        RefreshZoneCatalogItems();
        RefreshPlacedDungeonMarkers();
        ZonePanelStatusMessage = $"Repère placé : {marker.DisplayName}";
        OnPropertyChanged(nameof(OverlayChanged));
        return true;
    }

    public void PersistDungeonMarkers() =>
        DungeonMarkerStore.SaveAll(DungeonMarkers.Where(m => m.MapX > 0 || m.MapY > 0));

    private void ApplyDungeonMarkersChanged()
    {
        BumpMapPlacementStamp();
        RebuildPrecomputedMapPositions();
        FinishMapPlacementForDisplay();
    }

    private void SortDungeonMarkers()
    {
        var sorted = DungeonMarkers.OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        DungeonMarkers.Clear();
        foreach (var m in sorted)
            DungeonMarkers.Add(m);
    }

    public void MoveSelectedDungeonMarker(double mapX, double mapY)
    {
        if (SelectedDungeonMarker == null)
            return;

        SelectedDungeonMarker.MapX = Math.Clamp(mapX, 0, 1);
        SelectedDungeonMarker.MapY = Math.Clamp(mapY, 0, 1);
        PersistDungeonMarkers();
        ApplyDungeonMarkersChanged();
        OnPropertyChanged(nameof(OverlayChanged));
    }

    [RelayCommand]
    private void AddZone()
    {
        if (!TryAddZoneAt(0.42, 0.42))
        {
            System.Windows.MessageBox.Show(
                "Choisissez une zone dans la liste, ou cliquez sur la carte en mode Zones pour placer un rectangle.",
                "Zones",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    /// <summary>Ajoute un rectangle (coords 0–1 sur WowMap.png). Retourne false si la zone existe déjà.</summary>
    public bool TryAddZoneAt(double normalizedX, double normalizedY)
    {
        if (ZoneToAddMapId is not > 0)
        {
            ZonePanelStatusMessage = "Choisissez une zone dans la liste (ex. Orgrimmar), puis ＋.";
            return false;
        }

        if (!IsPlacingZone)
        {
            ZonePanelStatusMessage = "Appuyez sur ＋ avant de cliquer sur la carte.";
            return false;
        }

        var mapId = ZoneToAddMapId.Value;
        const double defaultW = 0.085;
        const double defaultH = 0.090;
        var left = Math.Clamp(normalizedX - defaultW / 2, 0, 1 - defaultW);
        var top = Math.Clamp(normalizedY - defaultH / 2, 0, 1 - defaultH);

        if (ZoneRects.FirstOrDefault(z => z.MapId == mapId) is { } previous)
        {
            ZoneRects.Remove(previous);
            if (ReferenceEquals(SelectedZoneRect, previous))
                SelectedZoneRect = null;
        }

        if (!ClassicEraMapProjection.TryGetCatalogEntry(mapId, out var entry))
        {
            ZonePanelStatusMessage = $"Zone {mapId} introuvable dans le catalogue.";
            return false;
        }

        var item = new CartoZoneRectItem
        {
            MapId = mapId,
            NameFr = entry.NameFr,
            NameEn = entry.NameEn,
            DisplayName = entry.DisplayName,
            Left = left,
            Top = top,
            Width = defaultW,
            Height = defaultH
        };
        ZoneRects.Add(item);
        SelectedZoneRect = item;
        ZoneToAddMapId = mapId;
        IsPlacingZone = false;
        PersistZoneRects();
        RefreshWorldZoneRects();
        RefreshCapitalZoneRects();
        RefreshZoneCatalogItems();
        ZonePanelStatusMessage = $"{item.DisplayName} placée — glissez le rectangle pour ajuster.";
        OnPropertyChanged(nameof(OverlayChanged));
        return true;
    }

    [RelayCommand]
    private void DeleteZone(CartoZoneRectItem? zone)
    {
        var target = zone ?? SelectedZoneRect;
        if (target == null)
            return;

        var idx = ZoneRects.IndexOf(target);
        if (idx < 0)
            return;

        ZoneRects.RemoveAt(idx);
        if (ReferenceEquals(SelectedZoneRect, target))
            SelectedZoneRect = ZoneRects.FirstOrDefault();

        PersistZoneRects();
        RefreshWorldZoneRects();
        RefreshCapitalZoneRects();
        RefreshZoneCatalogItems();
        ZonePanelStatusMessage = $"{target.DisplayName} supprimée.";

        OnPropertyChanged(nameof(OverlayChanged));
    }

    [RelayCommand]
    private void ClearDungeonMarker(CartoDungeonMarker? marker)
    {
        var target = marker ?? SelectedDungeonMarker;
        if (target == null)
            return;

        var name = target.DisplayName;
        DungeonMarkers.Remove(target);
        if (ReferenceEquals(SelectedDungeonMarker, target))
            SelectedDungeonMarker = null;

        PersistDungeonMarkers();
        ApplyDungeonMarkersChanged();
        RefreshZoneCatalogItems();
        RefreshPlacedDungeonMarkers();
        ZonePanelStatusMessage = $"{name} : repère supprimé de la liste.";
        OnPropertyChanged(nameof(OverlayChanged));
    }

    private Dictionary<int, ClassicEraMapProjection.CartoMapRect> BuildUserZoneOverridesFromPanel()
    {
        var user = ZoneMapCalibration.LoadUserOverrides();
        foreach (var def in CapitalMapDefinitions.All)
            user.Remove(def.MapId);

        foreach (var z in ZoneRects)
        {
            user[z.MapId] = ClassicEraMapProjection.SanitizeZoneRect(
                new ClassicEraMapProjection.CartoMapRect(z.Left, z.Top, z.Width, z.Height));
        }

        return user;
    }

    private void ApplyZoneProjectionFromPanel(bool saveToDisk)
    {
        var user = BuildUserZoneOverridesFromPanel();
        if (saveToDisk)
            ZoneMapCalibration.SaveAll(user);

        var projection = ZoneMapCalibration.BuildProjectionCalibration(user);
        ClassicEraMapProjection.ApplyUserRects(projection);
        _zoneCalibrationLoaded = true;
    }

    public void PersistZoneRects()
    {
        ApplyZoneProjectionFromPanel(saveToDisk: true);
        BumpMapPlacementStamp();
        RebuildPrecomputedMapPositions();
        FinishMapPlacementForDisplay();
        OnPropertyChanged(nameof(OverlayChanged));
        if (IsZonesPanelOpen)
        {
            RefreshZoneCatalogItems();
            RefreshUnplacedCharacters();
        }
    }

    /// <summary>Enregistre les zones, puis recalcule toutes les positions persos (addon + rectangles).</summary>
    [RelayCommand]
    private void RefreshCharacterMapPositions()
    {
        if (CartoMapPreloader.GetBitmap() is { PixelWidth: > 0, PixelHeight: > 0 } bmp)
            ClassicEraMapProjection.SetMapImagePixelSize(bmp.PixelWidth, bmp.PixelHeight);

        ApplyZoneProjectionFromPanel(saveToDisk: true);
        var placed = ApplyZonePositionsFromWowSync();
        ReorganizePlacedOnMapStacks();
        BumpMapPlacementStamp();
        _mapPositionsReady = true;
        _appliedMapPlacementStamp = _mapPlacementStamp;

        Save();
        if (ApplyFiltersChanged())
            OnPropertyChanged(nameof(FilteredCharacters));

        RefreshUnplacedCharacters();
        var total = Characters.Count(c => !c.IsHidden);
        ZonePanelStatusMessage = placed == total
            ? $"{placed} personnage(s) repositionné(s) sur la carte."
            : $"{placed}/{total} personnage(s) repositionné(s) — les autres manquent coords ou zone non calibrée.";
        OnPropertyChanged(nameof(OverlayChanged));
    }
}
