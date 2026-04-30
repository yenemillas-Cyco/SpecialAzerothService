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
        _selectedMode = LayoutMode.Main;
        RefreshMonitors();
    }

    public ObservableCollection<WindowInfo> AvailableWindows { get; } = [];
    public ObservableCollection<PreviewRect> PreviewRects { get; } = [];
    public ObservableCollection<MonitorInfo> Monitors { get; } = [];

    [ObservableProperty]
    private LayoutMode _selectedMode;

    [ObservableProperty]
    private MonitorInfo? _selectedMonitor;

    [ObservableProperty]
    private SplitOrientation _selectedSplitOrientation = SplitOrientation.Horizontal;

    [ObservableProperty]
    private bool _isSplitHorizontal = true;

    [ObservableProperty]
    private bool _isSplitVertical;

    [ObservableProperty]
    private string _statusMessage = "Prêt";

    public string AppVersion { get; } = $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    [ObservableProperty]
    private bool _isMainMode = true;

    [ObservableProperty]
    private bool _isSplitMode;

    // --- Taille ---
    [ObservableProperty]
    private bool _isSizeGrand;

    [ObservableProperty]
    private bool _isSizeMoyen = true;

    [ObservableProperty]
    private bool _isSizePetit;

    [ObservableProperty]
    private MainSize _selectedMainSize = MainSize.Moyen;

    // --- Position ---
    [ObservableProperty]
    private MainPosition _selectedMainPosition = MainPosition.TopRight;

    [ObservableProperty]
    private bool _isPosTopLeft;

    [ObservableProperty]
    private bool _isPosTopRight = true;

    [ObservableProperty]
    private bool _isPosBottomRight;

    [ObservableProperty]
    private bool _isPosBottomLeft;

    // --- Disposition (checkboxes combinables) ---
    [ObservableProperty]
    private bool _hasLateral = true;

    [ObservableProperty]
    private bool _hasBandeau;

    partial void OnHasLateralChanged(bool value)
    {
        if (!value && !HasBandeau) HasBandeau = true;
        UpdatePreview();
    }

    partial void OnHasBandeauChanged(bool value)
    {
        if (!value && !HasLateral) HasLateral = true;
        UpdatePreview();
    }

    // --- Position handlers ---
    partial void OnIsPosTopLeftChanged(bool value)
    {
        if (!value) return;
        IsPosTopRight = false; IsPosBottomRight = false; IsPosBottomLeft = false;
        SelectedMainPosition = MainPosition.TopLeft;
        UpdatePreview();
    }

    partial void OnIsPosTopRightChanged(bool value)
    {
        if (!value) return;
        IsPosTopLeft = false; IsPosBottomRight = false; IsPosBottomLeft = false;
        SelectedMainPosition = MainPosition.TopRight;
        UpdatePreview();
    }

    partial void OnIsPosBottomRightChanged(bool value)
    {
        if (!value) return;
        IsPosTopLeft = false; IsPosTopRight = false; IsPosBottomLeft = false;
        SelectedMainPosition = MainPosition.BottomRight;
        UpdatePreview();
    }

    partial void OnIsPosBottomLeftChanged(bool value)
    {
        if (!value) return;
        IsPosTopLeft = false; IsPosTopRight = false; IsPosBottomRight = false;
        SelectedMainPosition = MainPosition.BottomLeft;
        UpdatePreview();
    }

    // --- Mode handlers ---
    partial void OnIsMainModeChanged(bool value)
    {
        if (value)
        {
            IsSplitMode = false;
            SelectedMode = LayoutMode.Main;
            UpdatePreview();
        }
    }

    partial void OnIsSplitModeChanged(bool value)
    {
        if (value)
        {
            IsMainMode = false;
            SelectedMode = LayoutMode.Split;
            UpdatePreview();
        }
    }

    partial void OnIsSplitHorizontalChanged(bool value)
    {
        if (!value) return;
        IsSplitVertical = false;
        SelectedSplitOrientation = SplitOrientation.Horizontal;
        UpdatePreview();
    }

    partial void OnIsSplitVerticalChanged(bool value)
    {
        if (!value) return;
        IsSplitHorizontal = false;
        SelectedSplitOrientation = SplitOrientation.Vertical;
        UpdatePreview();
    }

    partial void OnSelectedMonitorChanged(MonitorInfo? oldValue, MonitorInfo? newValue)
    {
        if (newValue is null) { UpdatePreview(); return; }
        foreach (var w in AvailableWindows)
        {
            if (w.AssignedMonitor is null || w.AssignedMonitor == oldValue)
                w.AssignedMonitor = newValue;
        }
        UpdatePreview();
    }

    // --- Size handlers ---
    partial void OnIsSizeGrandChanged(bool value)
    {
        if (!value) return;
        IsSizeMoyen = false; IsSizePetit = false;
        SelectedMainSize = MainSize.Grand;
        UpdatePreview();
    }

    partial void OnIsSizeMoyenChanged(bool value)
    {
        if (!value) return;
        IsSizeGrand = false; IsSizePetit = false;
        SelectedMainSize = MainSize.Moyen;
        UpdatePreview();
    }

    partial void OnIsSizePetitChanged(bool value)
    {
        if (!value) return;
        IsSizeGrand = false; IsSizeMoyen = false;
        SelectedMainSize = MainSize.Petit;
        UpdatePreview();
    }

    // --- Helpers ---

    private WindowRect GetSelectedWorkArea() =>
        SelectedMonitor?.WorkArea ?? _windowService.GetWorkArea();

    private void RefreshMonitors()
    {
        Monitors.Clear();
        var monitors = _windowService.GetMonitors();
        var secondaryIndex = 1;
        foreach (var m in monitors)
        {
            var indexed = m with { Index = m.IsPrimary ? 0 : secondaryIndex++ };
            Monitors.Add(indexed);
        }

        SelectedMonitor = Monitors.FirstOrDefault(m => m.IsPrimary) ?? Monitors.FirstOrDefault();
        _logger.Information("Monitors refreshed: {Count} detected", monitors.Count);
    }

    // --- Commands ---

    [RelayCommand]
    private void RefreshMonitorList()
    {
        RefreshMonitors();
        StatusMessage = $"{Monitors.Count} écran(s) détecté(s)";
        UpdatePreview();
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

        AvailableWindows.Clear();

        var windows = _windowService.GetOpenWindows();
        var isFirst = true;
        foreach (var w in windows)
        {
            w.IsSelected = true;
            if (isFirst) { w.IsMainWindow = true; isFirst = false; }

            if (savedNames.TryGetValue(w.ProcessId, out var name))
                w.CustomName = name;
            if (savedMonitors.TryGetValue(w.ProcessId, out var mon))
                w.AssignedMonitor = mon;
            else
                w.AssignedMonitor = SelectedMonitor;

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

        var groups = selected.GroupBy(w => w.AssignedMonitor?.Handle ?? SelectedMonitor?.Handle ?? IntPtr.Zero);

        foreach (var group in groups)
        {
            var monitor = Monitors.FirstOrDefault(m => m.Handle == group.Key);
            var workArea = monitor?.WorkArea ?? GetSelectedWorkArea();
            var windowsInGroup = group.ToList();

            _logger.Information("Applying {Mode} layout on monitor {Mon} for {Count} windows",
                SelectedMode, monitor?.DisplayLabel ?? "default", windowsInGroup.Count);

            var layout = SelectedMode == LayoutMode.Main
                ? _layoutService.CalculateMainLayout(windowsInGroup, workArea, SelectedMainSize, SelectedMainPosition, HasLateral, HasBandeau)
                : _layoutService.CalculateSplitLayout(windowsInGroup, workArea, SelectedSplitOrientation);

            foreach (var (handle, rect) in layout)
                _windowService.MoveAndResize(handle, rect);
        }

        StatusMessage = $"Layout appliqué à {selected.Count} fenêtre(s)";
        _logger.Information("Layout applied to {Count} windows across {Groups} screen(s)", selected.Count, groups.Count());
    }

    [RelayCommand]
    private void FullscreenWindow(WindowInfo? window)
    {
        if (window is null) return;
        var workArea = window.AssignedMonitor?.WorkArea ?? GetSelectedWorkArea();
        _windowService.MoveAndResize(window.Handle, workArea);
        _logger.Information("Fullscreen window: {Title} on {W}x{H}", window.Title, workArea.Width, workArea.Height);
        StatusMessage = $"{window.DisplayName} en plein écran";
    }

    [RelayCommand]
    private void SetAsMain(WindowInfo? window)
    {
        if (window is null) return;
        foreach (var w in AvailableWindows) w.IsMainWindow = false;
        window.IsMainWindow = true;
        window.IsSelected = true;
        _logger.Information("Set main window: {Title}", window.Title);
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

        const double previewW = 480;
        const double previewH = 270;

        if (Monitors.Count == 0) return;

        var minX = Monitors.Min(m => m.Bounds.X);
        var minY = Monitors.Min(m => m.Bounds.Y);
        var maxX = Monitors.Max(m => m.Bounds.X + m.Bounds.Width);
        var maxY = Monitors.Max(m => m.Bounds.Y + m.Bounds.Height);
        var totalW = maxX - minX;
        var totalH = maxY - minY;

        var scale = Math.Min(previewW / totalW, previewH / totalH);
        var offsetX = (previewW - totalW * scale) / 2;
        var offsetY = (previewH - totalH * scale) / 2;

        foreach (var monitor in Monitors)
        {
            PreviewRects.Add(new PreviewRect
            {
                X = (monitor.Bounds.X - minX) * scale + offsetX,
                Y = (monitor.Bounds.Y - minY) * scale + offsetY,
                Width = monitor.Bounds.Width * scale,
                Height = monitor.Bounds.Height * scale,
                Title = monitor.DisplayLabel,
                IsMonitorOutline = true
            });
        }

        var selected = AvailableWindows.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0) return;

        var groups = selected.GroupBy(w => w.AssignedMonitor?.Handle ?? SelectedMonitor?.Handle ?? IntPtr.Zero);

        foreach (var group in groups)
        {
            var monitor = Monitors.FirstOrDefault(m => m.Handle == group.Key) ?? Monitors[0];
            var workArea = monitor.WorkArea;
            var windowsInGroup = group.ToList();

            var layout = SelectedMode == LayoutMode.Main
                ? _layoutService.CalculateMainLayout(windowsInGroup, workArea, SelectedMainSize, SelectedMainPosition, HasLateral, HasBandeau)
                : _layoutService.CalculateSplitLayout(windowsInGroup, workArea, SelectedSplitOrientation);

            var slotIndex = 0;
            foreach (var (handle, rect) in layout)
            {
                var win = windowsInGroup.First(w => w.Handle == handle);
                slotIndex++;
                PreviewRects.Add(new PreviewRect
                {
                    X = (rect.X - minX) * scale + offsetX,
                    Y = (rect.Y - minY) * scale + offsetY,
                    Width = rect.Width * scale,
                    Height = rect.Height * scale,
                    Title = win.IsMainWindow
                        ? $"★ {TruncateTitle(win.DisplayName, 16)}"
                        : $"#{slotIndex - 1} {TruncateTitle(win.DisplayName, 14)}",
                    IsMain = win.IsMainWindow,
                    Window = win
                });
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
    public WindowInfo? Window { get; init; }
}
