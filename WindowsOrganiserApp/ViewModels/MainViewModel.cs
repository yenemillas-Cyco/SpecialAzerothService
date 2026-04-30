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
    private readonly ILogger _logger;

    public MainViewModel(IWindowService windowService, ILayoutService layoutService, ILogger logger)
    {
        _windowService = windowService;
        _layoutService = layoutService;
        _logger = logger;
        RefreshMonitors();
    }

    public ObservableCollection<WindowInfo> AvailableWindows { get; } = [];
    public ObservableCollection<PreviewRect> PreviewRects { get; } = [];
    public ObservableCollection<MonitorInfo> Monitors { get; } = [];
    public ObservableCollection<MonitorLayoutConfig> MonitorConfigs { get; } = [];

    [ObservableProperty]
    private MonitorLayoutConfig? _selectedMonitorConfig;

    [ObservableProperty]
    private string _statusMessage = "Prêt";

    public string AppVersion { get; } = $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    partial void OnSelectedMonitorConfigChanged(MonitorLayoutConfig? oldValue, MonitorLayoutConfig? newValue)
    {
        if (oldValue is not null)
            oldValue.PropertyChanged -= MonitorConfig_PropertyChanged;
        if (newValue is not null)
            newValue.PropertyChanged += MonitorConfig_PropertyChanged;
        OnPropertyChanged(nameof(SelectedMonitorConfig));
    }

    private void MonitorConfig_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdatePreview();
    }

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
            config.PropertyChanged += (_, _) => UpdatePreview();
            MonitorConfigs.Add(config);
        }

        SelectedMonitorConfig = MonitorConfigs.FirstOrDefault(c => c.Monitor.IsPrimary)
                                ?? MonitorConfigs.FirstOrDefault();

        _logger.Information("Monitors refreshed: {Count} detected", monitors.Count);
    }

    // --- Commands ---

    [RelayCommand]
    private void SelectMonitorConfig(MonitorLayoutConfig? config)
    {
        if (config is not null)
            SelectedMonitorConfig = config;
    }

    [RelayCommand]
    private void RefreshWindows()
    {
        _logger.Information("Refreshing window list");

        var savedNames = AvailableWindows
            .Where(w => !string.IsNullOrWhiteSpace(w.CustomName))
            .ToDictionary(w => w.ProcessId, w => w.CustomName);
        var savedMonitors = AvailableWindows
            .Where(w => w.AssignedMonitor is not null)
            .ToDictionary(w => w.ProcessId, w => w.AssignedMonitor);
        var savedMains = AvailableWindows
            .Where(w => w.IsMainWindow)
            .Select(w => w.ProcessId)
            .ToHashSet();

        AvailableWindows.Clear();

        var defaultMonitor = Monitors.FirstOrDefault(m => m.IsPrimary) ?? Monitors.FirstOrDefault();
        var windows = _windowService.GetOpenWindows();
        foreach (var w in windows)
        {
            w.IsSelected = true;

            if (savedNames.TryGetValue(w.ProcessId, out var name))
                w.CustomName = name;
            if (savedMonitors.TryGetValue(w.ProcessId, out var mon))
                w.AssignedMonitor = mon;
            else
                w.AssignedMonitor = defaultMonitor;
            if (savedMains.Contains(w.ProcessId))
                w.IsMainWindow = true;

            w.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(WindowInfo.IsSelected) or nameof(WindowInfo.IsMainWindow)
                    or nameof(WindowInfo.AssignedMonitor))
                    UpdatePreview();
            };
            AvailableWindows.Add(w);
        }

        StatusMessage = $"{windows.Count} fenêtres détectées";
        UpdatePreview();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var w in AvailableWindows) w.IsSelected = true;
        UpdatePreview();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var w in AvailableWindows) w.IsSelected = false;
        UpdatePreview();
    }

    [RelayCommand]
    private void ApplyLayout()
    {
        var selected = AvailableWindows.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Sélectionnez au moins une fenêtre";
            _logger.Warning("Apply called with no windows selected");
            return;
        }

        var groups = selected.GroupBy(w => w.AssignedMonitor?.Handle ?? IntPtr.Zero);

        foreach (var group in groups)
        {
            var monitor = Monitors.FirstOrDefault(m => m.Handle == group.Key) ?? Monitors[0];
            var config = GetConfigForMonitor(monitor);
            var workArea = monitor.WorkArea;
            var windowsInGroup = group.ToList();

            var mainWindow = windowsInGroup.FirstOrDefault(w => w.IsMainWindow) ?? windowsInGroup[0];

            _logger.Information("Applying {Mode} layout on {Mon} for {Count} windows",
                config.Mode, monitor.DisplayLabel, windowsInGroup.Count);

            var layout = config.Mode == LayoutMode.Main
                ? _layoutService.CalculateMainLayout(windowsInGroup, workArea, config.Size, config.Position, config.HasLateral, config.HasBandeau)
                : _layoutService.CalculateSplitLayout(windowsInGroup, workArea, config.SplitOrientation);

            foreach (var (handle, rect) in layout)
                _windowService.MoveAndResize(handle, rect);
        }

        StatusMessage = $"Layout appliqué à {selected.Count} fenêtre(s)";
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
        UpdatePreview();
    }

    [RelayCommand]
    private void MoveUp(WindowInfo? window)
    {
        if (window is null) return;
        var idx = AvailableWindows.IndexOf(window);
        if (idx <= 0) return;
        AvailableWindows.Move(idx, idx - 1);
        UpdatePreview();
    }

    [RelayCommand]
    private void MoveDown(WindowInfo? window)
    {
        if (window is null) return;
        var idx = AvailableWindows.IndexOf(window);
        if (idx < 0 || idx >= AvailableWindows.Count - 1) return;
        AvailableWindows.Move(idx, idx + 1);
        UpdatePreview();
    }

    public void UpdatePreview()
    {
        PreviewRects.Clear();
        foreach (var cfg in MonitorConfigs)
            cfg.PreviewRects.Clear();

        if (Monitors.Count == 0) return;

        const double previewW = 240;
        const double previewH = 140;

        var selected = AvailableWindows.Where(w => w.IsSelected).ToList();
        var groups = selected.GroupBy(w => w.AssignedMonitor?.Handle ?? IntPtr.Zero);

        foreach (var config in MonitorConfigs)
        {
            var monitor = config.Monitor;
            var workArea = monitor.WorkArea;

            var scale = Math.Min(previewW / workArea.Width, previewH / workArea.Height);
            var offsetX = (previewW - workArea.Width * scale) / 2;
            var offsetY = (previewH - workArea.Height * scale) / 2;

            var windowsInGroup = groups
                .FirstOrDefault(g => g.Key == monitor.Handle)?
                .ToList() ?? [];

            if (windowsInGroup.Count == 0) continue;

            var layout = config.Mode == LayoutMode.Main
                ? _layoutService.CalculateMainLayout(windowsInGroup, workArea, config.Size, config.Position, config.HasLateral, config.HasBandeau)
                : _layoutService.CalculateSplitLayout(windowsInGroup, workArea, config.SplitOrientation);

            var slotIndex = 0;
            foreach (var (handle, rect) in layout)
            {
                var win = windowsInGroup.First(w => w.Handle == handle);
                slotIndex++;
                var previewRect = new PreviewRect
                {
                    X = (rect.X - workArea.X) * scale + offsetX,
                    Y = (rect.Y - workArea.Y) * scale + offsetY,
                    Width = rect.Width * scale,
                    Height = rect.Height * scale,
                    Title = win.IsMainWindow
                        ? $"★ {TruncateTitle(win.DisplayName, 16)}"
                        : $"#{slotIndex - 1} {TruncateTitle(win.DisplayName, 14)}",
                    IsMain = win.IsMainWindow,
                    Window = win
                };
                config.PreviewRects.Add(previewRect);
                PreviewRects.Add(previewRect);
            }
        }
    }

    private static string TruncateTitle(string title, int maxLength) =>
        title.Length <= maxLength ? title : string.Concat(title.AsSpan(0, maxLength - 1), "…");
}

public class PreviewRect
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public string Title { get; init; } = string.Empty;
    public bool IsMain { get; init; }
    public bool IsMonitorOutline { get; init; }
    public bool IsSelectedMonitor { get; init; }
    public WindowInfo? Window { get; init; }
    public MonitorLayoutConfig? MonitorConfig { get; init; }
}
