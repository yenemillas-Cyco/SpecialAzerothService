using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsOrganiserApp.Models.Carto;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.ViewModels;

public partial class CartoViewModel : ObservableObject
{
    private readonly ICartoService _cartoService;
    private readonly DispatcherTimer _cooldownTimer;
    private CartoData _data;

    public CartoViewModel(ICartoService cartoService)
    {
        _cartoService = cartoService;
        _data = _cartoService.Load();

        Accounts = new ObservableCollection<WowAccount>(_data.Accounts);
        Characters = new ObservableCollection<WowCharacter>(_data.Characters);

        _cooldownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _cooldownTimer.Tick += (_, _) =>
        {
            OnPropertyChanged(nameof(FilteredCharacters));
            CheckCooldownNotifications();
        };
        _cooldownTimer.Start();

        ApplyFilters();
    }

    public ObservableCollection<WowAccount> Accounts { get; }
    public ObservableCollection<WowCharacter> Characters { get; }

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
    private bool _isPlacingCharacter;

    [ObservableProperty]
    private string? _editNote = string.Empty;

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

    partial void OnFilterClassChanged(WowClass? value) => ApplyFilters();
    partial void OnFilterLevelMinChanged(int? value) => ApplyFilters();
    partial void OnFilterLevelMaxChanged(int? value) => ApplyFilters();
    partial void OnFilterAccountIdChanged(string? value) => ApplyFilters();
    partial void OnFilterNameChanged(string value) => ApplyFilters();

    partial void OnSelectedCharacterChanged(WowCharacter? value)
    {
        EditNote = value?.Note ?? string.Empty;
    }

    public Array WowClasses => Enum.GetValues(typeof(WowClass));
    public Array ProfessionTypes => Enum.GetValues(typeof(ProfessionType));
    public Array CooldownTypes => Enum.GetValues(typeof(CooldownType));
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

        var character = new WowCharacter
        {
            Name = NewCharName,
            Class = NewCharClass,
            Level = NewCharLevel,
            AccountId = NewCharAccountId,
            MapX = mapX,
            MapY = mapY
        };

        Characters.Add(character);
        IsPlacingCharacter = false;
        NewCharName = string.Empty;
        NewCharLevel = 1;
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

    [RelayCommand]
    private void MoveCharacter(WowCharacter character)
    {
        SelectedCharacter = character;
        IsPlacingCharacter = true;
        Characters.Remove(character);
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
    }

    private void CheckCooldownNotifications()
    {
        foreach (var character in Characters)
        {
            foreach (var cd in character.Cooldowns)
            {
                if (cd.LastUsed != null && cd.IsReady && cd.Note != "notified")
                {
                    cd.Note = "notified";
                    CooldownReady?.Invoke(this, (character, cd));
                }
            }
        }
    }

    public event EventHandler<(WowCharacter Character, CooldownEntry Cooldown)>? CooldownReady;

    public void Save()
    {
        _data.Accounts = [.. Accounts];
        _data.Characters = [.. Characters];
        _cartoService.Save(_data);
    }
}
