using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WindowsOrganiserApp.Models;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IWindowService _windowService;
    private readonly ILayoutService _layoutService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger _logger;
    private AppSettings? _loadedSettings;

    public MainViewModel(IWindowService windowService, ILayoutService layoutService,
                         ISettingsService settingsService, IThemeService themeService,
                         ILocalizationService localizationService, ILogger logger)
    {
        _windowService = windowService;
        _layoutService = layoutService;
        _settingsService = settingsService;
        _themeService = themeService;
        _localizationService = localizationService;
        _logger = logger;
        _loadedSettings = _settingsService.Load();

        if (!string.IsNullOrEmpty(_loadedSettings.Theme))
            _themeService.ApplyTheme(_loadedSettings.Theme);
        if (!string.IsNullOrEmpty(_loadedSettings.Language))
            _localizationService.ApplyLanguage(_loadedSettings.Language);

        _currentThemeLabel = _themeService.CurrentTheme;
        _currentLanguage = _localizationService.CurrentLanguage;
        RefreshMonitors();
    }

    public string[] AvailableThemes => _themeService.AvailableThemes;
    public string[] AvailableLanguages => _localizationService.AvailableLanguages;

    [ObservableProperty]
    private bool _isOrganiserMode = true;

    [ObservableProperty]
    private bool _isCartoMode;

    [ObservableProperty]
    private bool _isBountyMode;

    partial void OnIsOrganiserModeChanged(bool value)
    {
        if (value)
        {
            IsCartoMode = false;
            IsBountyMode = false;
            AdvancedVm?.RefreshFromMain();
        }
    }

    partial void OnIsCartoModeChanged(bool value)
    {
        if (value) { IsOrganiserMode = false; IsBountyMode = false; }
    }

    partial void OnIsBountyModeChanged(bool value)
    {
        if (value) { IsOrganiserMode = false; IsCartoMode = false; }
    }

    public AdvancedViewModel? AdvancedVm { get; set; }
    public CartoViewModel? CartoVm { get; set; }
    public BountyViewModel? BountyVm { get; set; }

    [ObservableProperty]
    private string _currentThemeLabel;

    [ObservableProperty]
    private string _currentLanguage;

    [ObservableProperty]
    private bool _wowOnly = true;

    partial void OnWowOnlyChanged(bool value)
    {
        RefreshWindowsCommand.Execute(null);
    }

    partial void OnCurrentThemeLabelChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && value != _themeService.CurrentTheme)
            _themeService.ApplyTheme(value);
    }

    partial void OnCurrentLanguageChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && value != _localizationService.CurrentLanguage)
            _localizationService.ApplyLanguage(value);
    }

    public AppSettings GetCurrentSettings()
    {
        var settings = _settingsService.Load();
        settings.Theme = _themeService.CurrentTheme;
        settings.Language = _localizationService.CurrentLanguage;

        settings.MonitorConfigs = MonitorConfigs.Select(c => new MonitorConfigSettings
        {
            DeviceName = c.Monitor.DeviceName,
            Mode = c.Mode.ToString(),
            Size = c.Size.ToString(),
            Position = c.Position.ToString(),
            HasLateral = c.HasLateral,
            HasBandeau = c.HasBandeau,
            SplitOrientation = c.SplitOrientation.ToString()
        }).ToList();

        settings.Windows = AvailableWindows.Select(w => new WindowSettings
        {
            LaunchOrder = w.LaunchOrder,
            CustomName = w.CustomName,
            AssignedMonitorDeviceName = w.AssignedMonitor?.DeviceName,
            IsMainWindow = w.IsMainWindow,
            IsSelected = w.IsSelected
        }).ToList();

        return settings;
    }

    public ObservableCollection<WindowInfo> AvailableWindows { get; } = [];
    public ObservableCollection<MonitorInfo> Monitors { get; } = [];
    public ObservableCollection<MonitorLayoutConfig> MonitorConfigs { get; } = [];

    [ObservableProperty]
    private string _statusMessage = "Prêt";

    public string AppVersion { get; } = $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    // --- Helpers ---

    private MonitorLayoutConfig GetConfigForMonitor(MonitorInfo monitor) =>
        MonitorConfigs.FirstOrDefault(c => c.Monitor.Handle == monitor.Handle)
        ?? MonitorConfigs.First();

    private void RefreshMonitors()
    {
        Monitors.Clear();
        MonitorConfigs.Clear();

        var monitors = _windowService.GetMonitors();
        var secondaryIndex = 1;
        foreach (var m in monitors)
        {
            var indexed = m with { Index = m.IsPrimary ? 0 : secondaryIndex++ };
            Monitors.Add(indexed);

            var config = new MonitorLayoutConfig { Monitor = indexed };

            var saved = _loadedSettings?.MonitorConfigs
                .FirstOrDefault(s => s.DeviceName == indexed.DeviceName);
            if (saved is not null)
            {
                if (Enum.TryParse<LayoutMode>(saved.Mode, out var mode)) config.Mode = mode;
                if (Enum.TryParse<MainSize>(saved.Size, out var size)) config.Size = size;
                if (Enum.TryParse<MainPosition>(saved.Position, out var pos)) config.Position = pos;
                config.HasLateral = saved.HasLateral;
                config.HasBandeau = saved.HasBandeau;
                if (Enum.TryParse<SplitOrientation>(saved.SplitOrientation, out var ori)) config.SplitOrientation = ori;
            }

            MonitorConfigs.Add(config);
        }

        _logger.Information("Monitors refreshed: {Count} detected", monitors.Count);
    }

    // --- Commands ---

    [RelayCommand]
    private void RefreshWindows()
    {
        _logger.Information("Refreshing window list");

        // 1. Save window state keyed by LaunchOrder
        var savedState = AvailableWindows.ToDictionary(
            w => w.LaunchOrder,
            w => (w.CustomName, MonitorDevice: w.AssignedMonitor?.DeviceName, w.IsMainWindow, w.IsSelected));

        // 2. Save monitor configs keyed by DeviceName
        var savedMonCfg = MonitorConfigs.ToDictionary(
            c => c.Monitor.DeviceName,
            c => (c.Mode, c.Size, c.Position, c.HasLateral, c.HasBandeau, c.SplitOrientation));

        // 3. Refresh monitors
        RefreshMonitors();

        // 4. Restore saved monitor configs
        foreach (var cfg in MonitorConfigs)
        {
            if (savedMonCfg.TryGetValue(cfg.Monitor.DeviceName, out var prev))
            {
                cfg.Mode = prev.Mode;
                cfg.Size = prev.Size;
                cfg.Position = prev.Position;
                cfg.HasLateral = prev.HasLateral;
                cfg.HasBandeau = prev.HasBandeau;
                cfg.SplitOrientation = prev.SplitOrientation;
            }
        }

        // 5. Refresh windows
        AvailableWindows.Clear();
        var defaultMonitor = Monitors.FirstOrDefault(m => m.IsPrimary) ?? Monitors.FirstOrDefault();
        var windows = _windowService.GetOpenWindows(wowOnly: WowOnly);
        foreach (var w in windows)
        {
            w.IsSelected = true;
            w.AssignedMonitor = defaultMonitor;

            if (savedState.TryGetValue(w.LaunchOrder, out var prev))
            {
                if (!string.IsNullOrWhiteSpace(prev.CustomName))
                    w.CustomName = prev.CustomName;
                if (prev.MonitorDevice is not null)
                {
                    var mon = Monitors.FirstOrDefault(m => m.DeviceName == prev.MonitorDevice);
                    w.AssignedMonitor = mon ?? defaultMonitor;
                }
                w.IsMainWindow = prev.IsMainWindow;
                w.IsSelected = prev.IsSelected;
            }

            // First launch: apply persisted settings by LaunchOrder
            if (savedState.Count == 0 && _loadedSettings?.Windows is { Count: > 0 } savedWindows)
            {
                var sw = savedWindows.FirstOrDefault(s => s.LaunchOrder == w.LaunchOrder);
                if (sw is not null)
                {
                    if (!string.IsNullOrWhiteSpace(sw.CustomName))
                        w.CustomName = sw.CustomName;
                    w.IsMainWindow = sw.IsMainWindow;
                    w.IsSelected = sw.IsSelected;
                    if (sw.AssignedMonitorDeviceName is not null)
                    {
                        var monitor = Monitors.FirstOrDefault(m => m.DeviceName == sw.AssignedMonitorDeviceName);
                        if (monitor is not null)
                            w.AssignedMonitor = monitor;
                    }
                }
            }

            w.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(WindowInfo.IsSelected) or nameof(WindowInfo.IsMainWindow)
                    or nameof(WindowInfo.AssignedMonitor))
                {
                    if (IsOrganiserMode)
                    {
                        if (e.PropertyName == nameof(WindowInfo.IsSelected))
                            AdvancedVm?.RefreshFromMain();
                        else if (e.PropertyName == nameof(WindowInfo.AssignedMonitor))
                            AdvancedVm?.MoveWindowToAssignedMonitor(w);
                    }
                }
            };
            AvailableWindows.Add(w);
        }

        StatusMessage = $"{windows.Count} fenêtres détectées — {Monitors.Count} écran(s)";

        if (IsOrganiserMode)
            AdvancedVm?.RefreshFromMain();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var w in AvailableWindows) w.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var w in AvailableWindows) w.IsSelected = false;
    }

    [RelayCommand]
    private void FullscreenWindow(WindowInfo? window)
    {
        if (window is null) return;

        if (window.IsFullscreen && window.SavedRect is not null)
        {
            _windowService.MoveAndResize(window.Handle, window.SavedRect);
            window.IsFullscreen = false;
            StatusMessage = $"{window.DisplayName} restauré";
        }
        else
        {
            window.SavedRect = _windowService.GetWindowRect(window.Handle);
            var workArea = window.AssignedMonitor?.WorkArea ?? _windowService.GetWorkArea();
            _windowService.MoveAndResize(window.Handle, workArea);
            window.IsFullscreen = true;
            StatusMessage = $"{window.DisplayName} en plein écran";
        }
    }

    [RelayCommand]
    private void SetAsMain(WindowInfo? window)
    {
        if (window is null) return;
        var targetMonitor = window.AssignedMonitor;
        foreach (var w in AvailableWindows)
        {
            if (w.AssignedMonitor?.Handle == targetMonitor?.Handle)
                w.IsMainWindow = false;
        }
        window.IsMainWindow = true;
        window.IsSelected = true;
        _logger.Information("Set main window: {Title} on {Mon}", window.Title, targetMonitor?.DisplayLabel);
    }
}
