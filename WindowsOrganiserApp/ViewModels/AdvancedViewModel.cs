using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WindowsOrganiserApp.Models;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.ViewModels;

public partial class AdvancedViewModel : ObservableObject
{
    private readonly IPresetService _presetService;
    private readonly IWindowService _windowService;
    private readonly ILayoutService _layoutService;
    private readonly MainViewModel _mainVm;
    private readonly ILogger _logger;

    public AdvancedViewModel(IPresetService presetService, IWindowService windowService,
                             ILayoutService layoutService, MainViewModel mainVm, ILogger logger)
    {
        _presetService = presetService;
        _windowService = windowService;
        _layoutService = layoutService;
        _mainVm = mainVm;
        _logger = logger;

        RefreshPresets();
    }

    public ObservableCollection<AdvancedSlot> Slots { get; } = [];
    public ObservableCollection<MonitorOutline> MonitorOutlines { get; } = [];
    public ObservableCollection<LayoutPreset> Presets { get; } = [];

    [ObservableProperty]
    private AdvancedSlot? _selectedSlot;

    public void MoveWindowToAssignedMonitor(WindowInfo window)
    {
        var slot = Slots.FirstOrDefault(s => s.Window == window);
        if (slot is null || window.AssignedMonitor is null) return;
        if (slot.MonitorDeviceName == window.AssignedMonitor.DeviceName) return;
        MoveSlotToMonitor(slot, window.AssignedMonitor);
        EnforceUniqueLeadOnMonitor(window, window.AssignedMonitor.DeviceName);
    }

    /// <summary>After a drag-drop, detect which monitor the slot center is over and re-assign.</summary>
    public void ResolveMonitorAfterDrop(AdvancedSlot slot)
    {
        var centerX = slot.CanvasX + slot.CanvasWidth / 2;
        var centerY = slot.CanvasY + slot.CanvasHeight / 2;

        foreach (var mon in MonitorOutlines)
        {
            if (centerX >= mon.CanvasX && centerX <= mon.CanvasX + mon.CanvasWidth &&
                centerY >= mon.CanvasY && centerY <= mon.CanvasY + mon.CanvasHeight)
            {
                var targetMon = _mainVm.Monitors.FirstOrDefault(m => m.DisplayLabel == mon.Label);
                if (targetMon is not null && targetMon.DeviceName != slot.MonitorDeviceName)
                {
                    slot.MonitorDeviceName = targetMon.DeviceName;
                    slot.MonitorWorkArea = targetMon.WorkArea;
                    slot.Window.AssignedMonitor = targetMon;
                    // Update internal monitor bounds for clamping
                    var monitors = _mainVm.Monitors.ToList();
                    var minX = monitors.Min(m => m.WorkArea.X);
                    var minY = monitors.Min(m => m.WorkArea.Y);
                    var maxX = monitors.Max(m => m.WorkArea.X + m.WorkArea.Width);
                    var maxY = monitors.Max(m => m.WorkArea.Y + m.WorkArea.Height);
                    var totalW = maxX - minX;
                    var totalH = maxY - minY;
                    var canvasOffX = (CanvasWidth - totalW * _globalScale) / 2;
                    var canvasOffY = (CanvasHeight - totalH * _globalScale) / 2;
                    var wa = targetMon.WorkArea;
                    var monCx = (wa.X - minX) * _globalScale + canvasOffX;
                    var monCy = (wa.Y - minY) * _globalScale + canvasOffY;
                    var monCw = wa.Width * _globalScale;
                    var monCh = wa.Height * _globalScale;
                    slot.UpdateMonitorBounds(monCx, monCy, monCw, monCh);
                    EnforceUniqueLeadOnMonitor(slot.Window, targetMon.DeviceName);
                    OnPropertyChanged(nameof(Slots));
                }
                return;
            }
        }
    }

    /// <summary>If the window arriving on a monitor is lead but that monitor already has one, remove lead from the arriving window.</summary>
    private void EnforceUniqueLeadOnMonitor(WindowInfo arriving, string targetDeviceName)
    {
        if (!arriving.IsMainWindow) return;
        var existingLead = Slots.Any(s =>
            s.MonitorDeviceName == targetDeviceName &&
            s.Window != arriving &&
            s.Window.IsMainWindow);
        if (existingLead)
            arriving.IsMainWindow = false;
    }

    private void MoveSlotToMonitor(AdvancedSlot slot, MonitorInfo newMon)
    {
        var monitors = _mainVm.Monitors.ToList();
        var minX = monitors.Min(m => m.WorkArea.X);
        var minY = monitors.Min(m => m.WorkArea.Y);
        var maxX = monitors.Max(m => m.WorkArea.X + m.WorkArea.Width);
        var maxY = monitors.Max(m => m.WorkArea.Y + m.WorkArea.Height);
        var totalW = maxX - minX;
        var totalH = maxY - minY;
        var canvasOffX = (CanvasWidth - totalW * _globalScale) / 2;
        var canvasOffY = (CanvasHeight - totalH * _globalScale) / 2;

        var wa = newMon.WorkArea;
        var monCx = (wa.X - minX) * _globalScale + canvasOffX;
        var monCy = (wa.Y - minY) * _globalScale + canvasOffY;
        var monCw = wa.Width * _globalScale;
        var monCh = wa.Height * _globalScale;

        slot.MonitorDeviceName = newMon.DeviceName;
        slot.MonitorWorkArea = wa;
        slot.SetCanvasDirect(monCx, monCy,
            Math.Min(slot.CanvasWidth, monCw),
            Math.Min(slot.CanvasHeight, monCh),
            _globalScale, monCx, monCy, monCw, monCh);
        _canvasModified = true;
        OnPropertyChanged(nameof(Slots));
    }

    [ObservableProperty]
    private LayoutPreset? _selectedPreset;

    [ObservableProperty]
    private string _newPresetName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public double CanvasWidth { get; set; } = 600;
    public double CanvasHeight { get; set; } = 340;

    // Bounding box of all monitors on canvas (for drag clamping)
    public double GlobalBoundsLeft { get; private set; }
    public double GlobalBoundsTop { get; private set; }
    public double GlobalBoundsRight { get; private set; }
    public double GlobalBoundsBottom { get; private set; }

    private double _globalScale;
    private int _globalOffsetX;
    private int _globalOffsetY;
    private bool _canvasModified;

    public void MarkCanvasModified() => _canvasModified = true;

    /// <summary>Layout-relevant settings changed (lead, monitor assignment) — next Apply should recalculate.</summary>
    public void MarkCanvasStale()
    {
        _canvasModified = false;
        OnPropertyChanged(nameof(Slots));
    }

    public void RefreshFromMain()
    {
        _canvasModified = false;
        Slots.Clear();
        MonitorOutlines.Clear();

        var windows = _mainVm.AvailableWindows.Where(w => w.IsSelected).ToList();
        var monitors = _mainVm.Monitors.ToList();
        if (monitors.Count == 0) return;

        // Bounding box of all monitors (virtual desktop)
        var minX = monitors.Min(m => m.WorkArea.X);
        var minY = monitors.Min(m => m.WorkArea.Y);
        var maxX = monitors.Max(m => m.WorkArea.X + m.WorkArea.Width);
        var maxY = monitors.Max(m => m.WorkArea.Y + m.WorkArea.Height);
        var totalW = maxX - minX;
        var totalH = maxY - minY;

        _globalOffsetX = minX;
        _globalOffsetY = minY;
        _globalScale = Math.Min(CanvasWidth / totalW, CanvasHeight / totalH) * 0.95;

        var canvasOffX = (CanvasWidth - totalW * _globalScale) / 2;
        var canvasOffY = (CanvasHeight - totalH * _globalScale) / 2;

        // Global bounds = bounding box of all monitors on the canvas
        GlobalBoundsLeft = canvasOffX;
        GlobalBoundsTop = canvasOffY;
        GlobalBoundsRight = canvasOffX + totalW * _globalScale;
        GlobalBoundsBottom = canvasOffY + totalH * _globalScale;

        // Monitor outlines — build a lookup so we can pass the canvas origin + size to slots
        var monitorCanvasMap = new Dictionary<IntPtr, (double cx, double cy, double cw, double ch)>();
        foreach (var mon in monitors)
        {
            var wa = mon.WorkArea;
            var cx = (wa.X - minX) * _globalScale + canvasOffX;
            var cy = (wa.Y - minY) * _globalScale + canvasOffY;
            var cw = wa.Width * _globalScale;
            var ch = wa.Height * _globalScale;
            monitorCanvasMap[mon.Handle] = (cx, cy, cw, ch);
            MonitorOutlines.Add(new MonitorOutline
            {
                CanvasX = cx,
                CanvasY = cy,
                CanvasWidth = wa.Width * _globalScale,
                CanvasHeight = wa.Height * _globalScale,
                Label = mon.DisplayLabel
            });
        }

        if (windows.Count == 0)
        {
            SelectedSlot = null;
            OnPropertyChanged(nameof(Slots));
            return;
        }

        // Read actual window positions from the OS
        foreach (var win in windows)
        {
            var actualRect = _windowService.GetWindowRect(win.Handle);

            // Find which monitor this window is actually on
            var bestMon = monitors[0];
            var bestOverlap = 0L;
            foreach (var mon in monitors)
            {
                var wa = mon.WorkArea;
                var ox = Math.Max(0, Math.Min(actualRect.X + actualRect.Width, wa.X + wa.Width) - Math.Max(actualRect.X, wa.X));
                var oy = Math.Max(0, Math.Min(actualRect.Y + actualRect.Height, wa.Y + wa.Height) - Math.Max(actualRect.Y, wa.Y));
                var overlap = (long)ox * oy;
                if (overlap > bestOverlap) { bestOverlap = overlap; bestMon = mon; }
            }

            var workArea = bestMon.WorkArea;
            var monEntry = monitorCanvasMap[bestMon.Handle];

            var realX = actualRect.X - workArea.X;
            var realY = actualRect.Y - workArea.Y;
            var realW = Math.Max(160, actualRect.Width);
            var realH = Math.Max(160, actualRect.Height);

            // If window is minimized or off-screen, give it a reasonable default
            if (actualRect.Width <= 0 || actualRect.Height <= 0)
            {
                realX = 0;
                realY = 0;
                realW = Math.Max(160, workArea.Width / 3);
                realH = Math.Max(160, workArea.Height / 3);
            }

            var slot = new AdvancedSlot
            {
                Window = win,
                MonitorDeviceName = bestMon.DeviceName,
                MonitorWorkArea = workArea,
                RealX = realX,
                RealY = realY,
                RealWidth = realW,
                RealHeight = realH
            };

            var canvasW = Math.Max(60, realW * _globalScale);
            var canvasH = Math.Max(40, realH * _globalScale);

            slot.SetCanvasDirect(
                monEntry.cx + realX * _globalScale,
                monEntry.cy + realY * _globalScale,
                canvasW,
                canvasH,
                _globalScale,
                monEntry.cx, monEntry.cy, monEntry.cw, monEntry.ch);

            slot.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Slots));
            Slots.Add(slot);
        }

        // Auto-assign leader only if none is set for each monitor
        foreach (var monGroup in Slots.GroupBy(s => s.MonitorDeviceName))
        {
            if (monGroup.Any(s => s.Window.IsMainWindow)) continue;
            var largest = monGroup
                .OrderByDescending(s => (long)s.RealWidth * s.RealHeight)
                .ThenBy(s => s.Window.LaunchOrder)
                .FirstOrDefault();
            if (largest is not null)
                largest.Window.IsMainWindow = true;
        }

        SelectedSlot = Slots.FirstOrDefault();
        OnPropertyChanged(nameof(Slots));
    }

    // --- Auto-layout commands (per monitor) ---

    [RelayCommand]
    private void AutoLayoutMain(MonitorInfo? monitor)
    {
        if (monitor is null) return;
        var cfg = _mainVm.MonitorConfigs.FirstOrDefault(c => c.Monitor.Handle == monitor.Handle);
        ApplyAutoLayoutForMonitor(monitor, (windows, wa) =>
            _layoutService.CalculateMainLayout(windows, wa,
                cfg?.Size ?? MainSize.Grand,
                cfg?.Position ?? MainPosition.TopLeft,
                cfg?.HasLateral ?? true,
                cfg?.HasBandeau ?? false));
    }

    [RelayCommand]
    private void AutoLayoutSplitH(MonitorInfo? monitor) =>
        ApplyAutoLayoutForMonitor(monitor, (windows, wa) =>
            _layoutService.CalculateSplitLayout(windows, wa, SplitOrientation.Vertical));

    [RelayCommand]
    private void AutoLayoutSplitV(MonitorInfo? monitor) =>
        ApplyAutoLayoutForMonitor(monitor, (windows, wa) =>
            _layoutService.CalculateSplitLayout(windows, wa, SplitOrientation.Horizontal));

    private void ApplyAutoLayoutForMonitor(MonitorInfo? monitor, Func<List<WindowInfo>, WindowRect, Dictionary<IntPtr, WindowRect>> layoutFunc)
    {
        if (monitor is null) return;
        var windows = _mainVm.AvailableWindows
            .Where(w => w.IsSelected && w.AssignedMonitor?.Handle == monitor.Handle)
            .ToList();
        if (windows.Count == 0) return;

        var monitors = _mainVm.Monitors.ToList();
        var wa = monitor.WorkArea;
        var layout = layoutFunc(windows, wa);

        var minX = monitors.Min(m => m.WorkArea.X);
        var minY = monitors.Min(m => m.WorkArea.Y);
        var canvasOffX = (CanvasWidth - (monitors.Max(m => m.WorkArea.X + m.WorkArea.Width) - minX) * _globalScale) / 2;
        var canvasOffY = (CanvasHeight - (monitors.Max(m => m.WorkArea.Y + m.WorkArea.Height) - minY) * _globalScale) / 2;
        var monCx = (wa.X - minX) * _globalScale + canvasOffX;
        var monCy = (wa.Y - minY) * _globalScale + canvasOffY;
        var monCw = wa.Width * _globalScale;
        var monCh = wa.Height * _globalScale;

        // Remove existing slots for this monitor
        var toRemove = Slots.Where(s => s.MonitorDeviceName == monitor.DeviceName).ToList();
        foreach (var s in toRemove) Slots.Remove(s);

        // Add new slots
        foreach (var kvp in layout)
        {
            var win = windows.FirstOrDefault(w => w.Handle == kvp.Key);
            if (win is null) continue;

            var rect = kvp.Value;
            var realX = rect.X - wa.X;
            var realY = rect.Y - wa.Y;
            var slot = new AdvancedSlot
            {
                Window = win,
                MonitorDeviceName = monitor.DeviceName,
                MonitorWorkArea = wa,
                RealX = realX, RealY = realY,
                RealWidth = rect.Width, RealHeight = rect.Height
            };
            slot.SetCanvasDirect(
                monCx + realX * _globalScale,
                monCy + realY * _globalScale,
                rect.Width * _globalScale,
                rect.Height * _globalScale,
                _globalScale, monCx, monCy, monCw, monCh);
            slot.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Slots));
            Slots.Add(slot);
        }

        SelectedSlot = Slots.FirstOrDefault();
        _canvasModified = true;
        OnPropertyChanged(nameof(Slots));
        // No status message — silent preview update
    }

    private double _copiedCanvasW;
    private double _copiedCanvasH;

    [ObservableProperty]
    private bool _hasCopiedSize;

    [ObservableProperty]
    private bool _isSwapMode;

    [RelayCommand]
    private void StartSwap()
    {
        if (SelectedSlot is null || Slots.Count < 2) return;
        IsSwapMode = true;
        StatusMessage = $"Cliquez sur la fenêtre à inverser avec {SelectedSlot.Window.DisplayName}";
    }

    public void CompleteSwap(AdvancedSlot target)
    {
        if (SelectedSlot is null || target == SelectedSlot)
        {
            IsSwapMode = false;
            return;
        }

        var srcX = SelectedSlot.CanvasX;
        var srcY = SelectedSlot.CanvasY;
        var srcW = SelectedSlot.CanvasWidth;
        var srcH = SelectedSlot.CanvasHeight;

        var dstX = target.CanvasX;
        var dstY = target.CanvasY;
        var dstW = target.CanvasWidth;
        var dstH = target.CanvasHeight;

        SelectedSlot.SetCanvasRect(dstX, dstY, dstX + dstW, dstY + dstH);
        target.SetCanvasRect(srcX, srcY, srcX + srcW, srcY + srcH);

        IsSwapMode = false;
        _canvasModified = true;
        OnPropertyChanged(nameof(Slots));
        StatusMessage = $"Positions inversées : {SelectedSlot.Window.DisplayName} ↔ {target.Window.DisplayName}";
    }

    [RelayCommand]
    private void CopySize()
    {
        if (SelectedSlot is null) { StatusMessage = "⚠ Sélectionnez d'abord une fenêtre sur le canvas"; return; }
        _copiedCanvasW = SelectedSlot.CanvasWidth;
        _copiedCanvasH = SelectedSlot.CanvasHeight;
        HasCopiedSize = true;
        StatusMessage = $"Taille copiée ({SelectedSlot.RealWidth}×{SelectedSlot.RealHeight})";
    }

    [RelayCommand]
    private void PasteSize()
    {
        if (SelectedSlot is null) { StatusMessage = "⚠ Sélectionnez d'abord une fenêtre sur le canvas"; return; }
        if (!HasCopiedSize) { StatusMessage = "⚠ Copiez d'abord une taille"; return; }
        SelectedSlot.SetCanvasRect(
            SelectedSlot.CanvasX, SelectedSlot.CanvasY,
            SelectedSlot.CanvasX + _copiedCanvasW,
            SelectedSlot.CanvasY + _copiedCanvasH);
        _canvasModified = true;
        OnPropertyChanged(nameof(Slots));
        StatusMessage = $"Taille collée sur {SelectedSlot.Window.DisplayName}";
    }

    [RelayCommand]
    private void ApplySizeToAll()
    {
        if (SelectedSlot is null || Slots.Count < 2) return;
        var srcW = SelectedSlot.CanvasWidth;
        var srcH = SelectedSlot.CanvasHeight;
        foreach (var slot in Slots)
        {
            if (slot == SelectedSlot) continue;
            slot.SetCanvasRect(slot.CanvasX, slot.CanvasY,
                slot.CanvasX + srcW, slot.CanvasY + srcH);
        }
        _canvasModified = true;
        OnPropertyChanged(nameof(Slots));
        StatusMessage = $"Taille appliquée à {Slots.Count - 1} fenêtre(s)";
    }


    [RelayCommand]
    private void ApplyAdvanced()
    {
        if (Slots.Count == 0)
        {
            StatusMessage = "⚠ Aucune fenêtre sur le canvas — sélectionnez des fenêtres dans la liste";
            return;
        }

        if (!_canvasModified)
        {
            _logger.Information("ApplyAdvanced: canvas not modified — running auto-layout first");
            foreach (var monitor in _mainVm.Monitors.ToList())
                AutoLayoutForSingleMonitor(monitor);
            _canvasModified = true;
        }

        _logger.Information("ApplyAdvanced: applying {SlotCount} slots", Slots.Count);
        var applied = ApplyForMonitorSlots(Slots);
        StatusMessage = applied > 0
            ? $"Layout appliqué à {applied} fenêtre(s)"
            : "⚠ Aucune fenêtre à appliquer";
    }

    [RelayCommand]
    private void ApplyMonitor(MonitorInfo monitor)
    {
        if (monitor is null) return;
        var monSlots = Slots.Where(s => s.MonitorDeviceName == monitor.DeviceName).ToList();
        var applied = ApplyForMonitorSlots(monSlots);
        StatusMessage = applied > 0
            ? $"Layout appliqué à {applied} fenêtre(s) sur {monitor.DisplayLabel}"
            : $"⚠ Aucune fenêtre assignée à {monitor.DisplayLabel}";
    }

    public void AutoLayoutForMonitor(MonitorInfo monitor) => AutoLayoutForSingleMonitor(monitor);

    private void AutoLayoutForSingleMonitor(MonitorInfo monitor)
    {
        var cfg = _mainVm.MonitorConfigs.FirstOrDefault(c => c.Monitor.Handle == monitor.Handle);
        if (cfg?.Mode == LayoutMode.Split)
        {
            ApplyAutoLayoutForMonitor(monitor, (windows, wa) =>
                _layoutService.CalculateSplitLayout(windows, wa, cfg.SplitOrientation));
        }
        else
        {
            ApplyAutoLayoutForMonitor(monitor, (windows, wa) =>
                _layoutService.CalculateMainLayout(windows, wa,
                    cfg?.Size ?? MainSize.Grand,
                    cfg?.Position ?? MainPosition.TopLeft,
                    cfg?.HasLateral ?? true,
                    cfg?.HasBandeau ?? false));
        }
    }

    private int ApplyForMonitorSlots(IReadOnlyList<AdvancedSlot> slots)
    {
        var applied = 0;
        foreach (var slot in slots)
        {
            var monitor = _mainVm.Monitors.FirstOrDefault(m => m.DeviceName == slot.MonitorDeviceName);
            if (monitor is null)
            {
                _logger.Warning("ApplyForMonitorSlots: no monitor found for DeviceName={Dev}", slot.MonitorDeviceName);
                continue;
            }

            var workArea = monitor.WorkArea;
            var rect = new WindowRect(
                workArea.X + slot.RealX,
                workArea.Y + slot.RealY,
                slot.RealWidth,
                slot.RealHeight);
            _logger.Information("Applying slot {Win} → ({X},{Y},{W},{H})",
                slot.Window.DisplayName, rect.X, rect.Y, rect.Width, rect.Height);
            _windowService.MoveAndResize(slot.Window.Handle, rect);
            applied++;
        }
        return applied;
    }

    [RelayCommand]
    private void SavePreset()
    {
        if (string.IsNullOrWhiteSpace(NewPresetName)) return;

        var preset = new LayoutPreset
        {
            Name = NewPresetName.Trim(),
            CreatedAt = DateTime.Now,
            Positions = Slots.Select((s, i) => new AdvancedWindowPosition
            {
                SlotIndex = i,
                MonitorDeviceName = s.MonitorDeviceName,
                X = s.RealX,
                Y = s.RealY,
                Width = s.RealWidth,
                Height = s.RealHeight,
                IsMain = s.Window.IsMainWindow
            }).ToList()
        };

        _presetService.Save(preset);
        RefreshPresets();
        SelectedPreset = Presets.FirstOrDefault(p => p.Name == preset.Name);
        StatusMessage = $"Preset \"{preset.Name}\" sauvegardé";
        NewPresetName = string.Empty;
    }

    [RelayCommand]
    private void LoadPreset()
    {
        if (SelectedPreset is null) return;

        var windows = _mainVm.AvailableWindows.Where(w => w.IsSelected).ToList();
        var positions = SelectedPreset.Positions;

        // Rebuild monitor canvas map for coordinate conversion
        var monitors = _mainVm.Monitors.ToList();
        if (monitors.Count == 0) return;

        var mMinX = monitors.Min(m => m.WorkArea.X);
        var mMinY = monitors.Min(m => m.WorkArea.Y);
        var mMaxX = monitors.Max(m => m.WorkArea.X + m.WorkArea.Width);
        var mMaxY = monitors.Max(m => m.WorkArea.Y + m.WorkArea.Height);
        var cOffX = (CanvasWidth - (mMaxX - mMinX) * _globalScale) / 2;
        var cOffY = (CanvasHeight - (mMaxY - mMinY) * _globalScale) / 2;

        Slots.Clear();
        for (var i = 0; i < Math.Min(positions.Count, windows.Count); i++)
        {
            var pos = positions[i];
            var win = windows[i];
            var monitor = monitors.FirstOrDefault(m => m.DeviceName == pos.MonitorDeviceName)
                          ?? monitors.FirstOrDefault();
            if (monitor is null) continue;

            var workArea = monitor.WorkArea;
            var monCx = (workArea.X - mMinX) * _globalScale + cOffX;
            var monCy = (workArea.Y - mMinY) * _globalScale + cOffY;
            var monCw = workArea.Width * _globalScale;
            var monCh = workArea.Height * _globalScale;

            var slot = new AdvancedSlot
            {
                Window = win,
                MonitorDeviceName = monitor.DeviceName,
                MonitorWorkArea = workArea,
                RealX = pos.X,
                RealY = pos.Y,
                RealWidth = pos.Width,
                RealHeight = pos.Height
            };

            slot.SetCanvasDirect(
                monCx + pos.X * _globalScale,
                monCy + pos.Y * _globalScale,
                pos.Width * _globalScale,
                pos.Height * _globalScale,
                _globalScale,
                monCx, monCy, monCw, monCh);

            slot.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Slots));
            Slots.Add(slot);
        }

        _canvasModified = true;
        SelectedSlot = Slots.FirstOrDefault();
        StatusMessage = $"Preset \"{SelectedPreset.Name}\" chargé";
    }

    [RelayCommand]
    private void DeletePreset()
    {
        if (SelectedPreset is null) return;
        _presetService.Delete(SelectedPreset.Name);
        StatusMessage = $"Preset \"{SelectedPreset.Name}\" supprimé";
        RefreshPresets();
    }

    private void RefreshPresets()
    {
        Presets.Clear();
        foreach (var p in _presetService.LoadAll())
            Presets.Add(p);
    }
}

public partial class AdvancedSlot : ObservableObject
{
    public WindowInfo Window { get; init; } = null!;
    public string MonitorDeviceName { get; set; } = string.Empty;
    public WindowRect MonitorWorkArea { get; set; } = new(0, 0, 1920, 1080);

    [ObservableProperty] private int _realX;
    [ObservableProperty] private int _realY;
    [ObservableProperty] private int _realWidth;
    [ObservableProperty] private int _realHeight;

    [ObservableProperty] private double _canvasX;
    [ObservableProperty] private double _canvasY;
    [ObservableProperty] private double _canvasWidth;
    [ObservableProperty] private double _canvasHeight;

    [ObservableProperty] private bool _isSelected;

    private double _scale;
    private double _monCanvasX;
    private double _monCanvasY;
    private double _monCanvasW;
    private double _monCanvasH;
    private bool _syncing;

    public double MonBoundsLeft => _monCanvasX;
    public double MonBoundsTop => _monCanvasY;
    public double MonBoundsRight => _monCanvasX + _monCanvasW;
    public double MonBoundsBottom => _monCanvasY + _monCanvasH;

    /// <summary>Update monitor bounds after cross-monitor drag.</summary>
    public void UpdateMonitorBounds(double monCanvasX, double monCanvasY, double monCanvasW, double monCanvasH)
    {
        _monCanvasX = monCanvasX;
        _monCanvasY = monCanvasY;
        _monCanvasW = monCanvasW;
        _monCanvasH = monCanvasH;
    }

    /// <summary>Initial setup — sets canvas AND real coordinates.</summary>
    public void SetCanvasDirect(double cx, double cy, double cw, double ch,
                                double scale, double monCanvasX, double monCanvasY,
                                double monCanvasW, double monCanvasH)
    {
        _scale = scale;
        _monCanvasX = monCanvasX;
        _monCanvasY = monCanvasY;
        _monCanvasW = monCanvasW;
        _monCanvasH = monCanvasH;
        _syncing = true;
        CanvasX = Math.Clamp(cx, monCanvasX, monCanvasX + monCanvasW - 5);
        CanvasY = Math.Clamp(cy, monCanvasY, monCanvasY + monCanvasH - 5);
        CanvasWidth = Math.Min(cw, monCanvasX + monCanvasW - CanvasX);
        CanvasHeight = Math.Min(ch, monCanvasY + monCanvasH - CanvasY);
        if (scale > 0)
        {
            RealX = (int)((CanvasX - monCanvasX) / scale);
            RealY = (int)((CanvasY - monCanvasY) / scale);
            RealWidth = Math.Max(160, (int)(CanvasWidth / scale));
            RealHeight = Math.Max(160, (int)(CanvasHeight / scale));
        }
        _syncing = false;
    }

    /// <summary>Called during drag — clamped within global desktop bounds on canvas.</summary>
    public void SetCanvasPos(double cx, double cy, double globalMinX, double globalMinY, double globalMaxX, double globalMaxY)
    {
        _syncing = true;
        CanvasX = Math.Clamp(cx, globalMinX, globalMaxX - CanvasWidth);
        CanvasY = Math.Clamp(cy, globalMinY, globalMaxY - CanvasHeight);
        if (_scale > 0)
        {
            RealX = (int)((CanvasX - _monCanvasX) / _scale);
            RealY = (int)((CanvasY - _monCanvasY) / _scale);
        }
        _syncing = false;
    }

    /// <summary>Called during resize — clamped within monitor bounds, min 160px real.</summary>
    public void SetCanvasSize(double cw, double ch)
    {
        _syncing = true;
        var minCw = _scale > 0 ? 160 * _scale : 20;
        var minCh = _scale > 0 ? 160 * _scale : 15;
        CanvasWidth = Math.Clamp(cw, minCw, _monCanvasX + _monCanvasW - CanvasX);
        CanvasHeight = Math.Clamp(ch, minCh, _monCanvasY + _monCanvasH - CanvasY);
        if (_scale > 0)
        {
            RealWidth = Math.Max(160, (int)(CanvasWidth / _scale));
            RealHeight = Math.Max(160, (int)(CanvasHeight / _scale));
        }
        _syncing = false;
    }

    /// <summary>Atomic set of left/top/right/bottom — for corner resize (TL/TR/BL).</summary>
    public void SetCanvasRect(double left, double top, double right, double bottom)
    {
        _syncing = true;
        var ml = _monCanvasX;
        var mt = _monCanvasY;
        var mr = _monCanvasX + _monCanvasW;
        var mb = _monCanvasY + _monCanvasH;

        // Clamp within monitor bounds, ensuring min size of 160px real
        var minCw = _scale > 0 ? 160 * _scale : 20;
        var minCh = _scale > 0 ? 160 * _scale : 15;
        left = Math.Clamp(left, ml, mr - minCw);
        top = Math.Clamp(top, mt, mb - minCh);
        right = Math.Clamp(right, left + minCw, mr);
        bottom = Math.Clamp(bottom, top + minCh, mb);

        CanvasX = left;
        CanvasY = top;
        CanvasWidth = right - left;
        CanvasHeight = bottom - top;

        if (_scale > 0)
        {
            RealX = (int)((CanvasX - _monCanvasX) / _scale);
            RealY = (int)((CanvasY - _monCanvasY) / _scale);
            RealWidth = Math.Max(160, (int)(CanvasWidth / _scale));
            RealHeight = Math.Max(160, (int)(CanvasHeight / _scale));
        }
        _syncing = false;
    }

    /// <summary>Called when user edits Real values in the fine-tuning panel.</summary>
    private void SyncCanvasFromReal()
    {
        if (_syncing || _scale == 0) return;
        _syncing = true;
        // Enforce minimum 160x160 real pixels
        if (RealWidth < 160) RealWidth = 160;
        if (RealHeight < 160) RealHeight = 160;
        var cx = Math.Max(_monCanvasX, RealX * _scale + _monCanvasX);
        var cy = Math.Max(_monCanvasY, RealY * _scale + _monCanvasY);
        var maxW = _monCanvasX + _monCanvasW - cx;
        var maxH = _monCanvasY + _monCanvasH - cy;
        CanvasX = cx;
        CanvasY = cy;
        CanvasWidth = Math.Clamp(RealWidth * _scale, 20, maxW);
        CanvasHeight = Math.Clamp(RealHeight * _scale, 15, maxH);
        _syncing = false;
    }

    partial void OnRealXChanged(int value) => SyncCanvasFromReal();
    partial void OnRealYChanged(int value) => SyncCanvasFromReal();
    partial void OnRealWidthChanged(int value) => SyncCanvasFromReal();
    partial void OnRealHeightChanged(int value) => SyncCanvasFromReal();
}

public class MonitorOutline
{
    public double CanvasX { get; init; }
    public double CanvasY { get; init; }
    public double CanvasWidth { get; init; }
    public double CanvasHeight { get; init; }
    public string Label { get; init; } = string.Empty;
}
