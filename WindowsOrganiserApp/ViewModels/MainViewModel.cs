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
    }

    public ObservableCollection<WindowInfo> AvailableWindows { get; } = [];
    public ObservableCollection<PreviewRect> PreviewRects { get; } = [];

    [ObservableProperty]
    private LayoutMode _selectedMode;

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

    // --- Commands ---

    [RelayCommand]
    private void RefreshWindows()
    {
        _logger.Information("Refreshing window list");
        AvailableWindows.Clear();

        var windows = _windowService.GetOpenWindows();
        var isFirst = true;
        foreach (var w in windows)
        {
            w.IsSelected = true;
            if (isFirst) { w.IsMainWindow = true; isFirst = false; }

            w.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(WindowInfo.IsSelected) or nameof(WindowInfo.IsMainWindow))
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

        var workArea = _windowService.GetWorkArea();
        _logger.Information("Applying {Mode} layout (size={Size}, pos={Pos}, lat={Lat}, band={Band}) on {W}x{H}",
            SelectedMode, SelectedMainSize, SelectedMainPosition, HasLateral, HasBandeau, workArea.Width, workArea.Height);

        var layout = SelectedMode == LayoutMode.Main
            ? _layoutService.CalculateMainLayout(selected, workArea, SelectedMainSize, SelectedMainPosition, HasLateral, HasBandeau)
            : _layoutService.CalculateSplitLayout(selected, workArea);

        foreach (var (handle, rect) in layout)
            _windowService.MoveAndResize(handle, rect);

        StatusMessage = $"Layout appliqué à {selected.Count} fenêtre(s)";
        _logger.Information("Layout applied to {Count} windows", selected.Count);
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

        var selected = AvailableWindows.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0) return;

        const double previewW = 480;
        const double previewH = 270;

        var workArea = _windowService.GetWorkArea();
        var scaleX = previewW / workArea.Width;
        var scaleY = previewH / workArea.Height;

        var layout = SelectedMode == LayoutMode.Main
            ? _layoutService.CalculateMainLayout(selected, workArea, SelectedMainSize, SelectedMainPosition, HasLateral, HasBandeau)
            : _layoutService.CalculateSplitLayout(selected, workArea);

        var slotIndex = 0;
        foreach (var (handle, rect) in layout)
        {
            var win = selected.First(w => w.Handle == handle);
            slotIndex++;
            PreviewRects.Add(new PreviewRect
            {
                X = (rect.X - workArea.X) * scaleX,
                Y = (rect.Y - workArea.Y) * scaleY,
                Width = rect.Width * scaleX,
                Height = rect.Height * scaleY,
                Title = win.IsMainWindow
                    ? $"★ {TruncateTitle(win.DisplayName, 16)}"
                    : $"#{slotIndex - 1} {TruncateTitle(win.DisplayName, 14)}",
                IsMain = win.IsMainWindow
            });
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
}
