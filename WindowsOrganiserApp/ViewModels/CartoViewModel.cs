using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsOrganiserApp.Controls;
using WindowsOrganiserApp.Converters;
using WindowsOrganiserApp.Models.Carto;
using WindowsOrganiserApp.Models.WowSync;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.ViewModels;

public partial class CartoViewModel : ObservableObject
{
    private readonly ICartoService _cartoService;
    private readonly IWowSyncService _wowSyncService;
    private readonly ISettingsService _settingsService;
    private readonly IUserProfileService _userProfile;
    private readonly DispatcherTimer _cooldownTimer;
    private CartoData _data;
    private List<WowAccountData>? _wowSyncCache;
    private Dictionary<string, WowCharacterData> _syncByKey = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CartoAccountConfig>? _accountSettingsEditSnapshot;
    private Task? _characterDataLoadTask;

    /// <summary>Chaque seconde — mise à jour des compteurs sans redessiner la carte.</summary>
    public event EventHandler? SecondTick;

    public CartoViewModel(
        ICartoService cartoService,
        IWowSyncService wowSyncService,
        IUserProfileService userProfile,
        ISettingsService settingsService)
    {
        _cartoService = cartoService;
        _wowSyncService = wowSyncService;
        _userProfile = userProfile;
        _settingsService = settingsService;
        _data = _cartoService.Load();
        _data.AccountSettings ??= new Dictionary<string, CartoAccountConfig>(StringComparer.OrdinalIgnoreCase);
        CartoAccountSettings.MigrateLegacyDisplayNames(_data);
        CartoUserMigration.Migrate(_data);
        CartoUserMigration.MigrateRerollIntoMain(_data);
        MigrateLegacyCharacterData();
        MigrateCharacterProfiles();
        MigratePlacedOnMapFlags();
        ApplyConfiguredAccountSettings();

        Accounts = new ObservableCollection<WowAccount>();
        Characters = new ObservableCollection<WowCharacter>();
        Timers = new ObservableCollection<MapTimer>(_data.Timers);

        foreach (var ext in _data.ExternalCharacters)
            Characters.Add(ext);

        _cooldownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cooldownTimer.Tick += (_, _) =>
        {
            SecondTick?.Invoke(this, EventArgs.Empty);
            CheckTimerAlerts();
            CheckCooldownNotifications();
        };
        _cooldownTimer.Start();

        RefreshFriends();
        RefreshFriendCartoUsers();
        ApplyFilters();
    }

    public const string CharactersLoadedPropertyName = nameof(CharactersLoaded);

    public bool CharactersLoaded { get; private set; }

    /// <summary>Charge WTF / WowSync une seule fois, hors thread UI.</summary>
    public Task EnsureCharacterDataLoadedAsync()
    {
        if (CharactersLoaded)
            return Task.CompletedTask;

        return _characterDataLoadTask ??= LoadCharacterDataDeferredAsync();
    }

    private async Task LoadCharacterDataDeferredAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                _wowSyncCache = TryReadWowSyncAccounts();
                RebuildSyncIndex();
            }).ConfigureAwait(false);

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                ApplyDeferredCharacterData,
                DispatcherPriority.Background);

            _ = Task.Run(CartoMapQuestIcon.PreloadQuestStubIcons);
        }
        catch (Exception ex)
        {
            _characterDataLoadTask = null;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                System.Windows.MessageBox.Show(
                    $"Erreur chargement Carto / WowSync :\n{ex.Message}",
                    "Special Azeroth Service",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning));
        }
    }

    private void ApplyDeferredCharacterData()
    {
        RefreshCharactersFromWowSync(saveAfter: false);
        foreach (var ch in Characters.Where(c => c.Status == CharacterStatus.Reroll))
            ch.Status = CharacterStatus.Main;
        CartoUserMigration.ApplyLegacyAccountHiddenToCharacters(_data, Characters);
        foreach (var account in Accounts)
            account.IsHidden = false;
        AccountIdToNameConverter.Accounts = [.. Accounts];
        EnsureCharactersVisibleOnMap();
        CharactersLoaded = true;
        OnPropertyChanged(CharactersLoadedPropertyName);
    }

    /// <summary>Tous les persos locaux visibles sur WowMap.png (WowSync puis pile pour le reste).</summary>
    public void EnsureCharactersVisibleOnMap()
    {
        ReloadZoneCalibration();

        foreach (var ch in Characters.Where(IsEligibleForStartupMapPlacement))
            ch.IsPlacedOnMap = true;

        ApplyZonePositionsFromWowSync();
        ReorganizePlacedOnMapStacks();
        ApplyFilters();
        OnPropertyChanged(nameof(FilteredCharacters));
        OnPropertyChanged(nameof(OverlayChanged));
    }

    /// <summary>Préchargement au splash : persos, carte, placement.</summary>
    public async Task WarmupAsync(IProgress<StartupLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        void Report(double percent, string message) =>
            progress?.Report(new StartupLoadProgress(percent, message));

        Report(28, "Lecture WowSync et personnages…");
        await EnsureCharacterDataLoadedAsync().ConfigureAwait(false);

        Report(52, "Chargement de la carte…");
        await Task.Run(CartoMapPreloader.EnsureLoaded, cancellationToken).ConfigureAwait(false);

        var (mapW, mapH) = CartoMapPreloader.PixelSize;

        Report(72, "Placement des personnages sur la carte…");
        var uiDispatcher = System.Windows.Application.Current?.Dispatcher
                           ?? throw new InvalidOperationException("Application WPF non initialisée.");
        await uiDispatcher.InvokeAsync(() =>
        {
            if (NeedsMigration && mapW > 0 && mapH > 0)
                MigrateCoordinates(mapW, mapH);
            EnsureCharactersVisibleOnMap();
        }, DispatcherPriority.Background);

        Report(90, "Préparation de l'interface Carto…");
    }

    public ObservableCollection<WowAccount> Accounts { get; }
    public ObservableCollection<WowCharacter> Characters { get; }
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

    /// <summary>Volet paramètres (comptes WTF, utilisateurs, amis).</summary>
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

    partial void OnIsSettingsPanelOpenChanged(bool value)
    {
        if (value)
        {
            FriendUserStatusMessage = string.Empty;
            BeginAccountSettingsEdit();
        }
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

    [ObservableProperty]
    private bool _showTableView;

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

    /// <summary>Persos pour le bandeau latéral — les comptes masqués (œil) restent listés.</summary>
    public IEnumerable<WowCharacter> GetCharactersForRoster() =>
        Characters.Where(c => PassesRosterFilters(c));

    private bool PassesRosterFilters(WowCharacter c)
    {
        if (c.IsExternal)
        {
            var hiddenFriends = _userProfile.Friends
                .Where(f => !f.IsVisible).Select(f => f.Guid).ToHashSet();
            return c.ExternalSource != null && !hiddenFriends.Contains(c.ExternalSource);
        }

        return IsCharacterInVisibleRosterSubtree(c);
    }

    /// <summary>Utilisateur ou catégorie masquée dans le roster (et sur la carte).</summary>
    public bool IsCharacterInVisibleRosterSubtree(WowCharacter ch)
    {
        if (ch.IsExternal)
            return true;

        var userId = GetUserIdForCharacter(ch);
        if (userId == null)
            return true;

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

    public void ToggleUserRosterSubtreeVisibility(CartoUser user)
    {
        user.IsRosterSubtreeHidden = !user.IsRosterSubtreeHidden;
        Save();
        ApplyFilters();
    }

    public void ToggleCategoryRosterSubtreeVisibility(CartoUser user, CharacterStatus category)
    {
        var policy = GetCategorySyncPolicy(user.Id, category);
        policy.IsRosterSubtreeHidden = !policy.IsRosterSubtreeHidden;
        Save();
        ApplyFilters();
    }

    public bool IsUserRosterSubtreeVisible(CartoUser user) => !user.IsRosterSubtreeHidden;

    public bool IsCategoryRosterSubtreeVisible(CartoUser user, CharacterStatus category) =>
        !GetCategorySyncPolicy(user.Id, category).IsRosterSubtreeHidden;

    private void ApplyFilters()
    {
        var hiddenFriends = _userProfile.Friends
            .Where(f => !f.IsVisible).Select(f => f.Guid).ToHashSet();

        var filtered = Characters.AsEnumerable()
            .Where(c => !c.IsHidden)
            .Where(IsCharacterInVisibleRosterSubtree)
            .Where(c => !c.IsExternal || (c.ExternalSource != null && !hiddenFriends.Contains(c.ExternalSource)));

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
            return;

        FilteredCharacters = new ObservableCollection<WowCharacter>(list);
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

        SelectedCharacter.Cooldowns.Add(new CooldownEntry { Type = type });
        Save();
        OnPropertyChanged(nameof(SelectedCharacter));
    }

    [RelayCommand]
    private void ActivateCooldown(CooldownEntry cd)
    {
        cd.LastUsed = DateTime.Now;
        cd.Note = null;
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
        if (!ch.IsPlacedOnMap || ch.IsExternal)
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
                     .Where(c => c.IsPlacedOnMap && !c.IsHidden && !c.IsExternal)
                     .OrderBy(c => Accounts.FirstOrDefault(a => a.Id == c.AccountId)?.Name ?? "")
                     .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (ch.HasCustomMapPosition)
                continue;
            if (HasWowSyncMapPosition(ch))
                continue;

            var (x, y) = CartoMapLayout.GetStackPosition(stackIndex++);
            ch.MapX = x;
            ch.MapY = y;
        }
    }

    private bool HasWowSyncMapPosition(WowCharacter ch)
    {
        var sync = FindWowSyncCharacter(ch);
        if (sync == null || (sync.X <= 0 && sync.Y <= 0))
            return false;

        return ClassicEraMapProjection.TryConvert(sync, out _, out _);
    }

    /// <summary>Change la catégorie et replace le perso (cadre gauche ou pile carte).</summary>
    public void SetCharacterStatus(WowCharacter character, CharacterStatus status)
    {
        if (character.IsExternal || character.Status == status)
            return;

        character.Status = status;

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
        if (character == null || character.IsExternal) return;

        character.IsPlacedOnMap = true;
        ReorganizePlacedOnMapStacks();
        ApplyFilters();
        Save();
    }

    /// <summary>Place un perso sur la carte à une position précise (drag depuis un cadre).</summary>
    public void PlaceCharacterOnMapAt(WowCharacter character, double mapX, double mapY)
    {
        if (character.IsExternal) return;

        character.IsPlacedOnMap = true;
        character.MapX = Math.Clamp(mapX, 0, 1);
        character.MapY = Math.Clamp(mapY, 0, 1);
        if (!string.IsNullOrEmpty(character.SyncKey))
            character.HasCustomMapPosition = true;

        ReorganizePlacedOnMapStacks();
        ApplyFilters();
        Save();
    }

    /// <summary>Retire de la carte et place dans le cadre de catégorie (drag depuis la carte).</summary>
    public void MoveCharacterToCategoryFrame(WowCharacter character, CharacterStatus category)
    {
        if (character.IsExternal) return;

        if (character.Status != category)
            character.Status = category;

        if (character.IsPlacedOnMap)
        {
            character.IsPlacedOnMap = false;
            ReorganizePlacedOnMapStacks();
        }

        Save();
        ApplyFilters();
    }

    [RelayCommand]
    public void RemoveCharacterFromMap(WowCharacter? character)
    {
        character ??= SelectedCharacter;
        if (character == null || character.IsExternal) return;

        character.IsPlacedOnMap = false;
        if (SelectedCharacter == character)
            SelectedCharacter = null;
        ReorganizePlacedOnMapStacks();
        ApplyFilters();
        Save();
    }

    public void ApplyMapPosition(WowCharacter ch, double x, double y)
    {
        ch.MapX = x;
        ch.MapY = y;
        if (!string.IsNullOrEmpty(ch.SyncKey))
            ch.HasCustomMapPosition = true;
    }

    public void ApplySyncPosition(WowCharacter cartoChar, double mapX, double mapY)
    {
        cartoChar.MapX = mapX;
        cartoChar.MapY = mapY;
    }

    /// <summary>Recharge la liste depuis WowSync. Ne réorganise la carte que si <paramref name="reapplyMapLayout"/>.</summary>
    public int RefreshCharactersFromWowSync(bool saveAfter = true, bool reapplyMapLayout = false)
    {
        _wowSyncCache = null;
        _syncByKey.Clear();
        var syncAccounts = GetCachedWowSyncAccounts();
        RebuildSyncIndex();
        var extrasByKey = _data.CharacterExtras
            .ToDictionary(e => e.SyncKey, StringComparer.OrdinalIgnoreCase);
        var profilesByKey = _data.CharacterProfiles
            .ToDictionary(p => p.SyncKey, StringComparer.OrdinalIgnoreCase);

        var external = Characters.Where(c => c.IsExternal).ToList();
        var preservedAccounts = Accounts
            .Concat(_data.Accounts)
            .Where(a => !string.IsNullOrWhiteSpace(a.SourceFolder))
            .GroupBy(a => a.SourceFolder, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        Characters.Clear();
        Accounts.Clear();
        _data.Accounts.Clear();

        var stackIndex = 0;
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
                    UserId = GetDefaultUserId()
                };
            }

            foreach (var syncChar in syncAccount.Characters.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                extrasByKey.TryGetValue(syncChar.Key, out var extras);
                profilesByKey.TryGetValue(syncChar.Key, out var profile);
                var cartoChar = CartoSyncMapper.ToCartoCharacter(syncChar, account.Id, extras, profile, stackIndex);
                if (cartoChar.IsPlacedOnMap && !cartoChar.HasCustomMapPosition)
                    stackIndex++;
                CartoSyncMapper.ApplyCooldownsFromSync(syncChar, cartoChar);
                CartoCharacterEnricher.ApplyFromSync(syncChar, cartoChar);
                Characters.Add(cartoChar);
            }
        }

        foreach (var ext in external)
        {
            if (HasLocalCharacterWithSyncKey(ext.SyncKey))
                continue;

            ext.IsPlacedOnMap = true;
            Characters.Add(ext);
        }

        foreach (var ch in Characters.Where(c => c.Status == CharacterStatus.Reroll))
            ch.Status = CharacterStatus.Main;

        CleanupAccountsFromWowSync();

        EnsureCharactersVisibleOnMap();

        AccountIdToNameConverter.Accounts = [.. Accounts];
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
        if (character == null || character.IsExternal)
            return "Aucun personnage sélectionné.";

        var sync = FindWowSyncCharacter(character);
        if (sync == null)
            return "Données WowSync introuvables — actualisez WowSync.";

        if (sync.X <= 0 && sync.Y <= 0)
            return "Coords à 0 — reconnectez-vous en jeu, /reload, puis actualisez WowSync.";

        ReloadZoneCalibration();

        if (!ClassicEraMapProjection.TryConvert(sync, out var mapX, out var mapY))
        {
            var zoneLabel = WowZoneLocalization.FormatDisplay(sync.Zone, sync.SubZone);
            return $"Zone non calibrée ou inconnue : « {zoneLabel} » (map {sync.MapId}).\n"
                   + "Ajoutez/ajustez le rectangle dans Carto → Zones.";
        }

        character.IsPlacedOnMap = true;
        character.MapX = mapX;
        character.MapY = mapY;
        character.HasCustomMapPosition = false;

        Save();
        ApplyFilters();
        OnPropertyChanged(nameof(OverlayChanged));
        return null;
    }

    private static void ReloadZoneCalibration()
    {
        var calibrated = ZoneMapCalibration.LoadAll();
        if (calibrated.Count > 0)
            ClassicEraMapProjection.ApplyUserRects(calibrated);
    }

    /// <summary>Recalcule MapX/MapY depuis WowSync — chaque perso avec ses propres coords (clé Nom-Royaume).</summary>
    private void ApplyZonePositionsFromWowSync(bool clearManualPositions = false)
    {
        ReloadZoneCalibration();

        foreach (var ch in Characters.Where(c =>
                     !c.IsExternal && !c.IsHidden && !c.ExcludeFromSync))
        {
            if (!clearManualPositions && ch.HasCustomMapPosition)
                continue;

            var sync = FindWowSyncCharacter(ch);
            if (sync == null || (sync.X <= 0 && sync.Y <= 0))
                continue;

            if (!ClassicEraMapProjection.TryConvert(sync, out var mapX, out var mapY))
                continue;

            ch.IsPlacedOnMap = true;
            ch.MapX = mapX;
            ch.MapY = mapY;
            if (clearManualPositions)
                ch.HasCustomMapPosition = false;
        }
    }

    private bool IsEligibleForStartupMapPlacement(WowCharacter ch) =>
        !ch.IsExternal
        && !ch.IsHidden
        && !ch.ExcludeFromSync
        && IsCharacterInVisibleRosterSubtree(ch);

    public int CountLocalCharactersForUser(string userId) =>
        Characters.Count(c => !c.IsExternal && GetUserIdForCharacter(c) == userId);

    public int CountLocalCharactersInCategory(string userId, CharacterStatus frameCategory)
    {
        var statuses = StatusesForRosterCategory(frameCategory).ToHashSet();
        return Characters.Count(c =>
            !c.IsExternal
            && GetUserIdForCharacter(c) == userId
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
    private void RefreshWowSync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_wowSyncService.WowPath))
            {
                System.Windows.MessageBox.Show(
                    "Configurez le chemin WoW dans l'onglet Addon, puis réessayez.",
                    "WowSync",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            _wowSyncCache = null;
            RefreshCharactersFromWowSync(reapplyMapLayout: false);
            ApplyZonePositionsFromWowSync();
            ApplySyncEnrichmentForAll();
            UpdateItemSearch();
            ApplyFilters();
            Save();
            OnPropertyChanged(nameof(OverlayChanged));
            OnPropertyChanged(CharactersLoadedPropertyName);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Erreur lecture WowSync : {ex.Message}", "WowSync",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
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
        foreach (var ch in Characters.Where(c => !c.IsExternal))
            ApplySyncEnrichment(ch);
    }

    public WowCharacterData? FindWowSyncCharacter(WowCharacter ch)
    {
        if (ch.IsExternal || string.IsNullOrEmpty(ch.SyncKey))
            return null;

        if (_syncByKey.TryGetValue(ch.SyncKey, out var found))
            return found;

        RebuildSyncIndex();
        return _syncByKey.TryGetValue(ch.SyncKey, out found) ? found : null;
    }

    private void RebuildSyncIndex()
    {
        _syncByKey.Clear();
        foreach (var account in GetCachedWowSyncAccounts())
        {
            foreach (var c in account.Characters)
            {
                if (!string.IsNullOrEmpty(c.Key))
                    _syncByKey[c.Key] = c;
            }
        }
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

        foreach (var legacy in _data.Characters.Where(c => !c.IsExternal))
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

    public IEnumerable<CartoUser> CartoUsers => GetOrderedUsers();

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
        if (ch.IsExternal)
            return null;

        var account = Accounts.FirstOrDefault(a => a.Id == ch.AccountId);
        return GetUserIdForAccount(account) ?? GetDefaultUserId();
    }

    private string? GetDefaultUserId() =>
        _data.Users.FirstOrDefault(u =>
            u.Name.Equals(CartoUserMigration.DefaultUserName, StringComparison.OrdinalIgnoreCase))?.Id
        ?? _data.Users.OrderBy(u => u.SortOrder).FirstOrDefault()?.Id;

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

    public CartoCategoryPolicy GetCategorySyncPolicy(string userId, CharacterStatus category)
    {
        var policy = _data.CategoryPolicies.FirstOrDefault(p =>
            p.UserId == userId && p.Category == category);
        if (policy != null)
            return policy;

        policy = new CartoCategoryPolicy { UserId = userId, Category = category };
        _data.CategoryPolicies.Add(policy);
        return policy;
    }

    public void UpdateCategorySyncPolicy(
        string userId,
        CharacterStatus category,
        Action<CartoCategoryPolicy> apply)
    {
        var policy = GetCategorySyncPolicy(userId, category);
        apply(policy);
        Save();
    }

    public static string FormatCategorySyncSummary(CartoCategoryPolicy policy)
    {
        var parts = new List<string>(6);
        if (policy.SyncBank) parts.Add("Banque");
        if (policy.SyncInventory) parts.Add("Inv.");
        if (policy.SyncProfessions) parts.Add("Métiers");
        if (policy.SyncCooldowns) parts.Add("CDs");
        if (policy.SyncGold) parts.Add("PO");
        if (policy.SyncSoulShards) parts.Add("Frag.");
        return parts.Count == 0 ? "aucune" : string.Join(" · ", parts);
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

    public bool IsFriendUser(CartoUser user) =>
        !user.Name.Equals(CartoUserMigration.DefaultUserName, StringComparison.OrdinalIgnoreCase);

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

    public string GetCharacterFriendGroup(WowCharacter ch)
    {
        if (ch.IsExternal)
        {
            if (!string.IsNullOrEmpty(ch.ExternalSource))
            {
                var name = GetFriendName(ch.ExternalSource);
                if (!string.IsNullOrWhiteSpace(name))
                    return name.Trim();
            }

            return "Ami";
        }

        var userId = GetUserIdForCharacter(ch);
        return GetUserDisplayName(userId);
    }

    public bool HasLocalCharacterWithSyncKey(string syncKey) =>
        !string.IsNullOrWhiteSpace(syncKey)
        && Characters.Any(c => !c.IsExternal
            && syncKey.Equals(c.SyncKey, StringComparison.OrdinalIgnoreCase));

    [ObservableProperty]
    private ObservableCollection<AccountSettingRow> _accountSettingRows = [];

    [ObservableProperty]
    private ObservableCollection<CartoUser> _friendCartoUsers = [];

    [ObservableProperty]
    private string _newFriendUserName = string.Empty;

    [ObservableProperty]
    private string _friendUserStatusMessage = string.Empty;

    /// <summary>Utilisateurs « ami » locaux (tous sauf Moi) — regroupement des comptes WTF.</summary>
    public void RefreshFriendCartoUsers()
    {
        FriendCartoUsers = new ObservableCollection<CartoUser>(
            GetOrderedUsers().Where(u => !IsDefaultCartoUser(u)));
    }

    [RelayCommand]
    private void AddFriendCartoUser()
    {
        var name = NewFriendUserName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            FriendUserStatusMessage = "Indiquez un nom d'ami.";
            return;
        }

        if (name.Equals(CartoUserMigration.DefaultUserName, StringComparison.OrdinalIgnoreCase))
        {
            FriendUserStatusMessage = "« Moi » est réservé à vos comptes.";
            return;
        }

        if (_data.Users.Any(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            FriendUserStatusMessage = $"L'ami « {name} » existe déjà.";
            return;
        }

        var maxOrder = _data.Users.Count > 0 ? _data.Users.Max(u => u.SortOrder) : 0;
        _data.Users.Add(new CartoUser { Name = name, SortOrder = maxOrder + 1 });
        NewFriendUserName = string.Empty;
        FriendUserStatusMessage = $"Ami « {name} » ajouté.";
        FinishFriendUserChange();
    }

    [RelayCommand]
    private void RemoveFriendCartoUser(CartoUser? user)
    {
        if (user == null || IsDefaultCartoUser(user))
            return;

        var moiId = _data.Users.FirstOrDefault(u =>
            u.Name.Equals(CartoUserMigration.DefaultUserName, StringComparison.OrdinalIgnoreCase))?.Id;
        if (moiId == null)
            return;

        foreach (var cfg in _data.AccountSettings.Values)
        {
            if (cfg.UserId == user.Id)
                cfg.UserId = moiId;
        }

        foreach (var row in AccountSettingRows)
        {
            if (row.UserId == user.Id)
                row.UserId = moiId;
        }

        foreach (var policy in _data.CategoryPolicies)
        {
            if (policy.UserId == user.Id)
                policy.UserId = moiId;
        }

        _data.Users.RemoveAll(u => u.Id == user.Id);
        FriendUserStatusMessage = $"Ami « {user.Name} » retiré — ses comptes sont passés sous Moi.";
        FinishFriendUserChange();
    }

    private void FinishFriendUserChange()
    {
        CartoUserMigration.ReindexUsers(_data);
        RefreshFriendCartoUsers();
        OnPropertyChanged(nameof(CartoUsers));
        Save();
    }

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
        var syncAccounts = TryReadWowSyncAccounts();
        var rows = new List<AccountSettingRow>();

        foreach (var sync in syncAccounts.OrderBy(a => a.SourceAccountName, StringComparer.OrdinalIgnoreCase))
        {
            _data.AccountSettings.TryGetValue(sync.SourceAccountName, out var cfg);
            rows.Add(AccountSettingRow.From(sync.SourceAccountName, cfg, sync.Characters.Count));
        }

        foreach (var (folder, cfg) in _data.AccountSettings)
        {
            if (rows.Any(r => r.SourceFolder.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (CartoUserMigration.GhostAccountFolders.Contains(folder))
                continue;

            rows.Add(AccountSettingRow.From(folder, cfg, 0));
        }

        AccountSettingRows = new ObservableCollection<AccountSettingRow>(rows);
        RefreshFriendCartoUsers();
        OnPropertyChanged(nameof(CartoUsers));
    }

    public void CloseSettingsPanelAfterSave()
    {
        SaveAccountSettingsFromRows();
        RefreshFriendCartoUsers();
        _settingsPanelClosingAfterSave = true;
        IsSettingsPanelOpen = false;
        _settingsPanelClosingAfterSave = false;
    }

    public void SaveAccountSettingsFromRows()
    {
        _data.AccountSettings = AccountSettingRows
            .ToDictionary(r => r.SourceFolder, r => r.ToConfig(), StringComparer.OrdinalIgnoreCase);

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
        OnPropertyChanged(nameof(CartoUsers));
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

    private void PersistDataForSync()
    {
        _data.Accounts = [.. Accounts];
        _data.Timers = [.. Timers];
        _data.CharacterProfiles = Characters
            .Where(c => !c.IsExternal && !string.IsNullOrEmpty(c.SyncKey))
            .Select(CartoSyncMapper.ToProfile)
            .ToList();
        _data.CharacterExtras = Characters
            .Where(c => !c.IsExternal && !string.IsNullOrEmpty(c.SyncKey))
            .Select(CartoSyncMapper.ToExtras)
            .ToList();
        _data.ExternalCharacters = Characters.Where(c => c.IsExternal).ToList();
        _data.Characters = [.. Characters];
    }

    public const double MinMapZoom = 0.2;
    public const double MaxMapZoom = 5.0;

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
        PersistDataForSync();
        AccountIdToNameConverter.Accounts = [.. Accounts];
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
        ExternalCharacters = _data.ExternalCharacters,
        Timers = _data.Timers,
        Characters = []
    };

    // ─── Amis / persos externes (local) ─────────────────────

    public string MyGuid => _userProfile.UserGuid;

    [ObservableProperty]
    private ObservableCollection<FriendEntry> _friends = [];

    public string FriendsSummary
    {
        get
        {
            var total = _userProfile.Friends.Count;
            return total == 0 ? "👥 Données locales" : $"👥 {total} ami(s) enregistré(s)";
        }
    }

    public void RefreshFriends()
    {
        Friends = new ObservableCollection<FriendEntry>(_userProfile.Friends);
        OnPropertyChanged(nameof(FriendsSummary));
    }

    private void SaveSettings() => _userProfile.Save();

    public string? GetFriendName(string guid) => _userProfile.GetFriend(guid)?.Name;

    [RelayCommand]
    private void RemoveFriend(FriendEntry friend)
    {
        var toRemove = Characters.Where(c => c.IsExternal && c.ExternalSource == friend.Guid).ToList();
        foreach (var ch in toRemove) Characters.Remove(ch);
        _userProfile.RemoveFriend(friend.Guid);
        RefreshFriends();
        SaveSettings();
        ApplyFilters();
        Save();
    }

    [RelayCommand]
    private void ToggleFriendVisibility(FriendEntry friend)
    {
        friend.IsVisible = !friend.IsVisible;
        RefreshFriends();
        SaveSettings();
        ApplyFilters();
    }

    public bool HasLocalFriendAccountsForName(string friendName)
    {
        if (string.IsNullOrWhiteSpace(friendName))
            return false;

        var key = friendName.Trim();
        foreach (var user in _data.Users)
        {
            if (user.Name.Equals(CartoUserMigration.DefaultUserName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (user.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var account in Accounts)
        {
            var userId = GetUserIdForAccount(account);
            var userName = GetUserDisplayName(userId);
            if (userName.Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public bool ShouldShowNetworkFriend(FriendEntry friend) =>
        friend.IsVisible && !HasLocalFriendAccountsForName(friend.Name);

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

    [RelayCommand]
    private void ToggleCharacterSync(WowCharacter character)
    {
        character.ExcludeFromSync = !character.ExcludeFromSync;
        Save();
    }

    // —— Gestionnaire de zones (rectangles sur la carte) ——

    public ObservableCollection<CartoZoneRectItem> ZoneRects { get; } = [];

    public IReadOnlyList<ClassicEraMapProjection.ZoneCatalogEntry> ZoneCatalog
        => ClassicEraMapProjection.GetZoneCatalog();

    [ObservableProperty]
    private bool _isZoneEditMode;

    [ObservableProperty]
    private CartoZoneRectItem? _selectedZoneRect;

    [ObservableProperty]
    private int? _zoneToAddMapId;

    partial void OnIsZoneEditModeChanged(bool value)
    {
        if (value)
        {
            IsPlacingCharacter = false;
            IsPlacingTimer = false;
            LoadZoneRects();
            SyncSelectedZoneFromCombo();
        }

        OnPropertyChanged(nameof(OverlayChanged));
    }

    partial void OnSelectedZoneRectChanged(CartoZoneRectItem? value)
    {
        if (value != null)
            ZoneToAddMapId = value.MapId;
        OnPropertyChanged(nameof(OverlayChanged));
    }

    partial void OnZoneToAddMapIdChanged(int? value) => SyncSelectedZoneFromCombo();

    private void SyncSelectedZoneFromCombo()
    {
        if (ZoneToAddMapId is not > 0)
            return;

        var match = ZoneRects.FirstOrDefault(z => z.MapId == ZoneToAddMapId.Value);
        if (match != null && !ReferenceEquals(SelectedZoneRect, match))
            SelectedZoneRect = match;
    }

    private void LoadZoneRects()
    {
        ZoneRects.Clear();
        var rects = ZoneMapCalibration.LoadAllRaw();

        foreach (var entry in ZoneCatalog)
        {
            if (ClassicEraMapProjection.IsContinentMap(entry.MapId))
                continue;

            if (!rects.TryGetValue(entry.MapId, out var rect)
                && !ClassicEraMapProjection.TryGetMapRect(entry.MapId, out rect))
            {
                rect = ClassicEraMapProjection.CreateDefaultRect(entry.MapId);
            }

            ZoneRects.Add(new CartoZoneRectItem
            {
                MapId = entry.MapId,
                NameFr = entry.NameFr,
                NameEn = entry.NameEn,
                DisplayName = entry.DisplayName,
                Left = rect.Left,
                Top = rect.Top,
                Width = rect.Width,
                Height = rect.Height
            });
        }

        EnsureCapitalZoneRects();

        var firstWorld = ZoneRects.FirstOrDefault(z => !ClassicEraMapProjection.IsCapitalMap(z.MapId))
                         ?? ZoneRects.FirstOrDefault();
        SelectedZoneRect = firstWorld;
        if (firstWorld != null)
            ZoneToAddMapId = firstWorld.MapId;
    }

    /// <summary>Ajoute un rectangle par défaut sur chaque mini-carte de capitale non encore calibrée.</summary>
    private void EnsureCapitalZoneRects()
    {
        var added = false;
        foreach (var def in CapitalMapDefinitions.All)
        {
            if (ZoneRects.Any(z => z.MapId == def.MapId))
                continue;
            if (!ClassicEraMapProjection.TryGetCatalogEntry(def.MapId, out var entry))
                continue;

            var rect = ClassicEraMapProjection.CreateDefaultRect(def.MapId);
            ZoneRects.Add(new CartoZoneRectItem
            {
                MapId = def.MapId,
                NameFr = entry.NameFr,
                NameEn = entry.NameEn,
                DisplayName = entry.DisplayName,
                Left = rect.Left,
                Top = rect.Top,
                Width = rect.Width,
                Height = rect.Height
            });
            added = true;
        }

        if (added)
            PersistZoneRects();
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
        const double defaultW = 0.085;
        const double defaultH = 0.090;
        var left = Math.Clamp(normalizedX - defaultW / 2, 0, 1 - defaultW);
        var top = Math.Clamp(normalizedY - defaultH / 2, 0, 1 - defaultH);

        int mapId;
        if (ZoneToAddMapId is > 0)
            mapId = ZoneToAddMapId.Value;
        else
        {
            var next = ZoneCatalog.FirstOrDefault(z => ZoneRects.All(r => r.MapId != z.MapId));
            if (next.MapId == 0)
                return false;
            mapId = next.MapId;
        }

        if (ZoneRects.Any(z => z.MapId == mapId))
            return false;

        if (!ClassicEraMapProjection.TryGetCatalogEntry(mapId, out var entry))
            return false;

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
        ZoneToAddMapId = null;
        PersistZoneRects();
        OnPropertyChanged(nameof(OverlayChanged));
        return true;
    }

    [RelayCommand]
    private void DeleteZone()
    {
        if (SelectedZoneRect == null) return;
        var idx = ZoneRects.IndexOf(SelectedZoneRect);
        ZoneRects.Remove(SelectedZoneRect);
        SelectedZoneRect = ZoneRects.Count == 0
            ? null
            : ZoneRects[Math.Min(idx, ZoneRects.Count - 1)];
        PersistZoneRects();
        OnPropertyChanged(nameof(OverlayChanged));
    }

    public void PersistZoneRects()
    {
        var dict = ZoneRects.ToDictionary(
            z => z.MapId,
            z => ClassicEraMapProjection.SanitizeZoneRect(
                new ClassicEraMapProjection.CartoMapRect(z.Left, z.Top, z.Width, z.Height)));

        ZoneMapCalibration.SaveAll(dict);
        ClassicEraMapProjection.ApplyUserRects(dict);
        OnPropertyChanged(nameof(OverlayChanged));
    }
}
