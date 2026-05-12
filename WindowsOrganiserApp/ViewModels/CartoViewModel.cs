using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WindowsOrganiserApp.Converters;
using WindowsOrganiserApp.Models.Carto;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.ViewModels;

public partial class CartoViewModel : ObservableObject
{
    private readonly ICartoService _cartoService;
    private readonly SyncService _syncService;
    private readonly DispatcherTimer _cooldownTimer;
    private CartoData _data;

    public CartoViewModel(ICartoService cartoService, SyncService syncService)
    {
        _cartoService = cartoService;
        _syncService = syncService;
        _data = _cartoService.Load();

        Accounts = new ObservableCollection<WowAccount>(_data.Accounts);
        Characters = new ObservableCollection<WowCharacter>(_data.Characters);
        Timers = new ObservableCollection<MapTimer>(_data.Timers);
        AccountIdToNameConverter.Accounts = _data.Accounts;

        _syncService.FriendDataReceived += OnFriendDataReceived;
        _syncService.ConnectionStateChanged += s =>
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => SyncStatus = s);

        _cooldownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cooldownTimer.Tick += (_, _) =>
        {
            ApplyFilters();
            OnPropertyChanged(nameof(Timers));
            CheckTimerAlerts();
            CheckCooldownNotifications();
        };
        _cooldownTimer.Start();

        ApplyFilters();
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
    private CharacterStatus _newCharStatus = CharacterStatus.Reroll;

    [ObservableProperty]
    private bool _isPlacingCharacter;

    [ObservableProperty]
    private bool _isPlacingTimer;

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

    // Map overlay toggles
    [ObservableProperty]
    private bool _showZoneNames = true;

    [ObservableProperty]
    private bool _useFrencNames = true;

    [ObservableProperty]
    private bool _showAllianceFlightPaths;

    [ObservableProperty]
    private bool _showHordeFlightPaths;

    [ObservableProperty]
    private bool _showZoneLevels;

    partial void OnShowZoneNamesChanged(bool value) => OnPropertyChanged(nameof(OverlayChanged));
    partial void OnUseFrencNamesChanged(bool value) => OnPropertyChanged(nameof(OverlayChanged));
    partial void OnShowAllianceFlightPathsChanged(bool value) => OnPropertyChanged(nameof(OverlayChanged));
    partial void OnShowHordeFlightPathsChanged(bool value) => OnPropertyChanged(nameof(OverlayChanged));
    partial void OnShowZoneLevelsChanged(bool value) => OnPropertyChanged(nameof(OverlayChanged));

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
        .Cast<CharacterStatus>().ToArray();
    public ProfessionType[] ProfessionTypes => Enum.GetValues(typeof(ProfessionType))
        .Cast<ProfessionType>()
        .Where(p => p is not (ProfessionType.Peche or ProfessionType.Cuisine or ProfessionType.Secourisme))
        .ToArray();
    public CooldownType[] CooldownTypes => Enum.GetValues(typeof(CooldownType))
        .Cast<CooldownType>()
        .Where(ct => ct is not (CooldownType.Transmutation or CooldownType.Etoffe_lunaire or CooldownType.Etoffe_de_lombre))
        .ToArray();
    public Array QuestItemTypes => Enum.GetValues(typeof(QuestItemType));

    private void ApplyFilters()
    {
        var filtered = Characters.AsEnumerable();

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

        FilteredCharacters = new ObservableCollection<WowCharacter>(filtered);
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
        if (string.IsNullOrWhiteSpace(NewCharName)) return;
        IsPlacingCharacter = true;
    }

    public void PlaceCharacterAt(double mapX, double mapY)
    {
        if (!IsPlacingCharacter) return;

        if (_movingCharacter != null)
        {
            _movingCharacter.MapX = mapX;
            _movingCharacter.MapY = mapY;
            _movingCharacter = null;
        }
        else
        {
            var character = new WowCharacter
            {
                Name = NewCharName,
                Class = NewCharClass,
                Level = NewCharLevel,
                AccountId = NewCharAccountId,
                Status = NewCharStatus,
                MapX = mapX,
                MapY = mapY
            };
            Characters.Add(character);
            NewCharName = string.Empty;
            NewCharLevel = 1;
        }

        IsPlacingCharacter = false;
        ApplyFilters();
        Save();
    }

    [RelayCommand]
    private void RemoveCharacter(WowCharacter character)
    {
        Characters.Remove(character);
        if (SelectedCharacter == character) SelectedCharacter = null;
        ApplyFilters();
        Save();
    }

    private WowCharacter? _movingCharacter;

    [RelayCommand]
    private void MoveCharacter(WowCharacter character)
    {
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

    [RelayCommand]
    private void ZoomIn()
    {
        MapZoom = Math.Min(MapZoom * 1.25, 5.0);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        MapZoom = Math.Max(MapZoom / 1.25, 0.5);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        MapZoom = 1.0;
        MapOffsetX = 0;
        MapOffsetY = 0;
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

    [RelayCommand]
    private void ExportData()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Exporter mes données Carto",
            Filter = "JSON|*.json",
            FileName = "carto_export.json"
        };
        if (dlg.ShowDialog() != true) return;

        var myChars = Characters.Where(c => !c.IsExternal).ToList();
        var export = new CartoData { Accounts = [.. Accounts], Characters = myChars };
        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        File.WriteAllText(dlg.FileName, json);
    }

    [RelayCommand]
    private void ImportData()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Importer les données d'un ami",
            Filter = "JSON|*.json"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var imported = JsonSerializer.Deserialize<CartoData>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (imported == null) return;

            var sourceName = Path.GetFileNameWithoutExtension(dlg.FileName);

            // Remove previously imported chars from this source
            var toRemove = Characters.Where(c => c.IsExternal && c.ExternalSource == sourceName).ToList();
            foreach (var ch in toRemove) Characters.Remove(ch);

            // Tag and add imported characters
            foreach (var ch in imported.Characters)
            {
                ch.IsExternal = true;
                ch.ExternalSource = sourceName;
                ch.Id = Guid.NewGuid().ToString();
                Characters.Add(ch);
            }

            ApplyFilters();
            Save();
        }
        catch { /* Invalid file — ignore */ }
    }

    [RelayCommand]
    private void ClearImported()
    {
        var toRemove = Characters.Where(c => c.IsExternal).ToList();
        foreach (var ch in toRemove) Characters.Remove(ch);
        ApplyFilters();
        Save();
    }

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

    public void Save()
    {
        _data.Accounts = [.. Accounts];
        _data.Characters = [.. Characters];
        _data.Timers = [.. Timers];
        AccountIdToNameConverter.Accounts = _data.Accounts;
        _cartoService.Save(_data);
        ApplyFilters();
        _ = _syncService.PushUpdateAsync(_data);
    }

    // ─── Sync ────────────────────────────────────────────────

    [ObservableProperty]
    private string _syncStatus = "Déconnecté";

    [ObservableProperty]
    private string _friendGuidInput = string.Empty;

    public string MyGuid => _syncService.UserGuid;

    [RelayCommand]
    private async Task ConnectSync()
    {
        SyncStatus = "Connexion...";
        await _syncService.ConnectAsync();
    }

    [RelayCommand]
    private async Task AddFriend()
    {
        var guid = FriendGuidInput.Trim();
        if (string.IsNullOrWhiteSpace(guid) || guid == MyGuid) return;
        await _syncService.SubscribeToFriend(guid);
        FriendGuidInput = string.Empty;
        OnPropertyChanged(nameof(FriendGuids));
    }

    [RelayCommand]
    private async Task RemoveFriend(string guid)
    {
        await _syncService.UnsubscribeFromFriend(guid);
        OnPropertyChanged(nameof(FriendGuids));
    }

    public List<string> FriendGuids => _syncService.FriendGuids;

    private void OnFriendDataReceived(string friendGuid, string friendName, SyncPayload payload)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var toRemove = Characters.Where(c => c.IsExternal && c.ExternalSource == friendGuid).ToList();
            foreach (var ch in toRemove) Characters.Remove(ch);

            foreach (var ch in payload.Characters)
            {
                ch.IsExternal = true;
                ch.ExternalSource = friendGuid;
                Characters.Add(ch);
            }

            _data.Characters = [.. Characters];
            _cartoService.Save(_data);
            ApplyFilters();
        });
    }
}
