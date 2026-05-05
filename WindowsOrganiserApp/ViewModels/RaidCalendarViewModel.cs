using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WindowsOrganiserApp.Models;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.ViewModels;

public partial class RaidCalendarViewModel : ObservableObject
{
    private readonly IRaidHelperService _raidHelperService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;

    public RaidCalendarViewModel(IRaidHelperService raidHelperService,
                                  ISettingsService settingsService, ILogger logger)
    {
        _raidHelperService = raidHelperService;
        _settingsService = settingsService;
        _logger = logger;

        var settings = settingsService.Load();
        _apiKey = settings.RaidHelperApiKey ?? string.Empty;
        _serverId = settings.RaidHelperServerId ?? string.Empty;
    }

    public ObservableCollection<RaidEvent> Events { get; } = [];

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _serverId = string.Empty;

    [ObservableProperty]
    private bool _useServerMode;

    [ObservableProperty]
    private bool _isConnected;

    public bool IsNotConnected => !IsConnected;

    partial void OnIsConnectedChanged(bool value) => OnPropertyChanged(nameof(IsNotConnected));

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Non connecté — Renseignez vos paramètres et cliquez sur Connecter.";

    [ObservableProperty]
    private RaidEvent? _selectedEvent;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    public IEnumerable<RaidEvent> EventsForSelectedDate =>
        Events.Where(e => e.StartDateTime.Date == SelectedDate.Date)
              .OrderBy(e => e.StartDateTime);

    public IEnumerable<RaidEvent> EventsForCurrentMonth =>
        Events.Where(e => e.StartDateTime.Month == SelectedDate.Month
                       && e.StartDateTime.Year == SelectedDate.Year)
              .OrderBy(e => e.StartDateTime);

    partial void OnSelectedDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(EventsForSelectedDate));
        OnPropertyChanged(nameof(EventsForCurrentMonth));
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "⚠ Remplissez votre API Key personnelle.";
            return;
        }

        if (UseServerMode && string.IsNullOrWhiteSpace(ServerId))
        {
            StatusMessage = "⚠ En mode serveur, le Server ID est requis.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Connexion en cours…";

        List<RaidEvent> events;
        if (UseServerMode && !string.IsNullOrWhiteSpace(ServerId))
            events = await _raidHelperService.GetServerEventsAsync(ServerId, ApiKey);
        else
            events = await _raidHelperService.GetUserEventsAsync(ApiKey);

        if (events.Count == 0)
        {
            StatusMessage = "⚠ Aucun événement trouvé. Vérifiez votre clé ou vous n'êtes inscrit à aucun raid.";
            IsConnected = false;
            IsLoading = false;
            return;
        }

        Events.Clear();
        foreach (var ev in events.OrderBy(e => e.StartDateTime))
            Events.Add(ev);

        IsConnected = true;
        IsLoading = false;
        StatusMessage = $"✓ Connecté — {events.Count} événement(s) chargé(s).";
        OnPropertyChanged(nameof(EventsForSelectedDate));
        OnPropertyChanged(nameof(EventsForCurrentMonth));

        SaveSettings();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!IsConnected) return;

        IsLoading = true;
        StatusMessage = "Actualisation…";

        List<RaidEvent> events;
        if (UseServerMode && !string.IsNullOrWhiteSpace(ServerId))
            events = await _raidHelperService.GetServerEventsAsync(ServerId, ApiKey);
        else
            events = await _raidHelperService.GetUserEventsAsync(ApiKey);

        Events.Clear();
        foreach (var ev in events.OrderBy(e => e.StartDateTime))
            Events.Add(ev);

        IsLoading = false;
        StatusMessage = $"✓ {events.Count} événement(s) — Dernière maj: {DateTime.Now:HH:mm}";
        OnPropertyChanged(nameof(EventsForSelectedDate));
        OnPropertyChanged(nameof(EventsForCurrentMonth));
    }

    [RelayCommand]
    private void Disconnect()
    {
        IsConnected = false;
        Events.Clear();
        StatusMessage = "Déconnecté.";
        OnPropertyChanged(nameof(EventsForSelectedDate));
    }

    [RelayCommand]
    private void PreviousDay() => SelectedDate = SelectedDate.AddDays(-1);

    [RelayCommand]
    private void NextDay() => SelectedDate = SelectedDate.AddDays(1);

    [RelayCommand]
    private void Today() => SelectedDate = DateTime.Today;

    [RelayCommand]
    private void PreviousMonth()
    {
        SelectedDate = SelectedDate.AddMonths(-1);
        OnPropertyChanged(nameof(EventsForCurrentMonth));
    }

    [RelayCommand]
    private void NextMonth()
    {
        SelectedDate = SelectedDate.AddMonths(1);
        OnPropertyChanged(nameof(EventsForCurrentMonth));
    }

    private void SaveSettings()
    {
        var settings = _settingsService.Load();
        settings.RaidHelperServerId = ServerId;
        settings.RaidHelperApiKey = ApiKey;
        _settingsService.Save(settings);
    }
}
