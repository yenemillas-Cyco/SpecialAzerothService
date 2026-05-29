using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WindowsOrganiserApp.Controls;
using WindowsOrganiserApp.Models.Carto;
using WindowsOrganiserApp;
using WindowsOrganiserApp.Models.WowSync;
using WindowsOrganiserApp.Services;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

public partial class CartoView : UserControl
{
    private CartoViewModel? Vm => DataContext as CartoViewModel;
    private bool _isPanning;
    private Point _panStart;
    private double _panStartOffsetX, _panStartOffsetY;
    private WowCharacter? _tooltipCharacter;
    private bool _isDraggingCharPopup;
    private Point _charPopupDragStart;
    private double _charPopupDragBaseX;
    private double _charPopupDragBaseY;
    private readonly HashSet<string> _rosterExpandedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _rosterCollapsedKeys = new(StringComparer.OrdinalIgnoreCase);
    private CartoViewModel? _subscribedVm;
    private PropertyChangedEventHandler? _vmPropertyChangedHandler;
    private bool _suppressRosterExpandEvents;
    private static BitmapImage? _cachedWorldMap;
    private DispatcherTimer? _rosterRebuildDebounce;
    private DispatcherTimer? _mapMarkersDebounce;
    private bool _mapLayoutEventsWired;
    private bool _suppressPanelToggleEvents;

    public CartoView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_subscribedVm != null)
            {
                if (_vmPropertyChangedHandler != null)
                    _subscribedVm.PropertyChanged -= _vmPropertyChangedHandler;
                _subscribedVm.SecondTick -= OnViewModelSecondTick;
                _subscribedVm.TimerExpired -= OnTimerExpired;
            }

            _subscribedVm = null;
            _vmPropertyChangedHandler = null;

            if (DataContext is CartoViewModel vm)
            {
                _subscribedVm = vm;
                _vmPropertyChangedHandler = OnViewModelPropertyChanged;
                vm.PropertyChanged += _vmPropertyChangedHandler;
                vm.SecondTick += OnViewModelSecondTick;
                vm.TimerExpired += OnTimerExpired;
                ApplyRightPanelLayout();
            }
        };
        Loaded += OnCartoViewLoaded;
        PreviewKeyDown += CartoView_PreviewKeyDown;
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
                return;

            if (Vm != null)
                _ = Vm.EnsureCharacterDataLoadedAsync();

            // Ne pas bloquer le thread UI au moment où Carto devient visible.
            Dispatcher.BeginInvoke(StartCartoSession, DispatcherPriority.ApplicationIdle);
        };
    }

    private void OnCartoViewLoaded(object sender, RoutedEventArgs e)
    {
        if (_mapLayoutEventsWired)
            return;

        _mapLayoutEventsWired = true;

        if (CharacterRosterHost != null)
        {
            CharacterRosterHost.AllowDrop = true;
            CharacterRosterHost.DragOver += CharacterRoster_DragOver;
            CharacterRosterHost.Drop += CharacterRoster_Drop;
            CharacterRosterHost.DragLeave += (_, _) => SetHighlightedDropFrame(null);
            CharacterRosterHost.SizeChanged -= CharacterRosterHost_SizeChanged;
            CharacterRosterHost.SizeChanged += CharacterRosterHost_SizeChanged;
        }

        MapBorder.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(MapCanvas_MouseDown), true);
        EnsureMapFitLayoutHook();
        if (MapImage != null)
        {
            MapImage.SizeChanged -= MapImage_SizeChanged;
            MapImage.SizeChanged += MapImage_SizeChanged;
        }

        if (MapCanvas != null)
        {
            MapCanvas.SizeChanged -= MapCanvas_SizeChanged;
            MapCanvas.SizeChanged += MapCanvas_SizeChanged;
        }

        ApplyRightPanelLayout();
    }

    private void MapImage_SizeChanged(object sender, SizeChangedEventArgs e) =>
        ScheduleZoneEditorRedraw();

    private void MapCanvas_SizeChanged(object sender, SizeChangedEventArgs e) =>
        ScheduleZoneEditorRedraw();

    private DispatcherTimer? _zoneEditorRedrawTimer;

    private void ScheduleZoneEditorRedraw()
    {
        if (!IsLoaded)
            return;

        _zoneEditorRedrawTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _zoneEditorRedrawTimer.Stop();
        _zoneEditorRedrawTimer.Tick -= ZoneEditorRedrawTimer_Tick;
        _zoneEditorRedrawTimer.Tick += ZoneEditorRedrawTimer_Tick;
        _zoneEditorRedrawTimer.Start();
    }

    private void ZoneEditorRedrawTimer_Tick(object? sender, EventArgs e)
    {
        _zoneEditorRedrawTimer?.Stop();
        RedrawZoneEditor();
    }

    private void CartoView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && CharPopup.IsOpen)
        {
            CloseCharacterTooltip();
            e.Handled = true;
        }
    }

    private void CharPopup_Closed(object? sender, EventArgs e)
    {
        _tooltipCharacter = null;
        if (Vm != null)
            Vm.SelectedCharacter = null;
        RequestMapMarkersRefresh();
    }

    private void RequestMapMarkersRefresh()
    {
        if (!_cartoUiLive)
            return;

        _mapMarkersDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _mapMarkersDebounce.Stop();
        _mapMarkersDebounce.Tick -= MapMarkersDebounce_Tick;
        _mapMarkersDebounce.Tick += MapMarkersDebounce_Tick;
        _mapMarkersDebounce.Start();
    }

    private void MapMarkersDebounce_Tick(object? sender, EventArgs e)
    {
        _mapMarkersDebounce?.Stop();
        if (!IsVisible || Vm == null)
            return;

        RedrawMarkers();
        if (CartoRuntimeOptions.ShowCapitalMaps)
            RedrawCapitalMaps();
        if (Vm.IsZoneEditMode)
            RedrawZoneEditor();
    }

    private double MapWidth => _worldPixelW > 0 ? _worldPixelW : (MapImage.ActualWidth > 0 ? MapImage.ActualWidth : 1024);
    private double MapHeight => _worldPixelH > 0 ? _worldPixelH : (MapImage.ActualHeight > 0 ? MapImage.ActualHeight : 768);

    private bool _migrated;
    private void MigrateIfNeeded()
    {
        if (_migrated || Vm == null || MapImage.ActualWidth <= 0) return;
        if (Vm.NeedsMigration)
            Vm.MigrateCoordinates(MapImage.ActualWidth, MapImage.ActualHeight);
        _migrated = true;
    }

    private MapTimer? _draggingTimer;

    private const string CharacterDragFormat = "SpecialAzerothService.CartoCharacter";
    private WowCharacter? _chipDragCharacter;
    private Point _chipDragStart;
    private bool _chipDragStarted;
    private Border? _highlightedDropFrame;
    private bool _isDragging;
    private const string ZoneEditTag = "zone-edit";
    private CartoZoneRectItem? _zoneDragItem;
    private bool _zoneResizeDrag;
    private CartoDungeonMarker? _dungeonDragMarker;
    private Point _dungeonDragStartMap;
    private Point _zoneDragStartMap;
    private double _zoneDragStartLeft, _zoneDragStartTop, _zoneDragStartW, _zoneDragStartH;

    private void ScheduleMapRedraw() => RequestMapMarkersRefresh();

    private void OnViewModelSecondTick(object? sender, EventArgs e)
    {
        UpdateTimerCountdowns();
        UpdateCooldownProgressBars();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CartoViewModel vm)
            return;

        if (e.PropertyName is nameof(CartoViewModel.IsZoneEditMode)
            or nameof(CartoViewModel.IsZonesPanelOpen)
            or nameof(CartoViewModel.OverlayChanged)
            or nameof(CartoViewModel.SelectedZoneRect)
            or nameof(CartoViewModel.SelectedDungeonMarker)
            or nameof(CartoViewModel.IsPlacingDungeonMarker)
            or nameof(CartoViewModel.ZoneToAddMapId))
        {
            if (e.PropertyName is nameof(CartoViewModel.OverlayChanged)
                && (vm.ShowAllianceFlightPaths || vm.ShowHordeFlightPaths))
                RedrawOverlays();
            ScheduleZoneEditorRedraw();
            if (e.PropertyName is not nameof(CartoViewModel.SelectedZoneRect)
                && e.PropertyName is not nameof(CartoViewModel.SelectedDungeonMarker))
                RequestMapMarkersRefresh();
            return;
        }

        if (e.PropertyName is nameof(CartoViewModel.IsRosterOpen)
            or nameof(CartoViewModel.IsItemSearchOpen)
            or nameof(CartoViewModel.IsTimersPanelOpen)
            or nameof(CartoViewModel.IsZonesPanelOpen)
            or nameof(CartoViewModel.IsSettingsPanelOpen))
        {
            ApplyRightPanelLayout();
            return;
        }

        var isLoadEvent = e.PropertyName == CartoViewModel.CharactersLoadedPropertyName;
        if (!_cartoUiLive && !isLoadEvent)
            return;

        if (e.PropertyName is nameof(CartoViewModel.FilteredCharacters)
            or nameof(CartoViewModel.Friends))
        {
            RequestMapMarkersRefresh();
            try { SummaryGrid.Items.Refresh(); } catch { }
        }
        else if (e.PropertyName is nameof(CartoViewModel.FriendCartoUsers)
            or nameof(CartoViewModel.CartoUsers))
        {
            ScheduleRosterRebuild();
        }
        else if (e.PropertyName is nameof(CartoViewModel.SelectedCharacter))
        {
            RequestMapMarkersRefresh();
        }
        else if (e.PropertyName == nameof(CartoViewModel.Timers))
        {
            RedrawTimerMarkers();
            UpdateTimerCountdowns();
        }
        else if (isLoadEvent)
        {
            if (vm.CharactersLoaded)
                RebuildCharacterRoster();
            TryApplyCharacterUiWhenReady();
        }
    }

    private void ScheduleRosterRebuild()
    {
        if (CartoRuntimeOptions.UseSimpleCharacterList || Vm == null)
            return;

        _rosterRebuildDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _rosterRebuildDebounce.Stop();
        _rosterRebuildDebounce.Tick -= RosterRebuildDebounce_Tick;
        _rosterRebuildDebounce.Tick += RosterRebuildDebounce_Tick;
        _rosterRebuildDebounce.Start();
    }

    private void RosterRebuildDebounce_Tick(object? sender, EventArgs e)
    {
        _rosterRebuildDebounce?.Stop();
        if (!IsVisible || Vm == null || !Vm.CharactersLoaded)
            return;

        Dispatcher.BeginInvoke(RebuildCharacterRoster, DispatcherPriority.Background);
    }

    private void RedrawMapLayer()
    {
        if (Vm == null)
            return;

        if (Vm.ShowAllianceFlightPaths || Vm.ShowHordeFlightPaths)
            RedrawOverlays();
        RedrawZoneEditor();
        RedrawMarkers();
        RedrawTimerMarkers();
        if (CartoRuntimeOptions.ShowCapitalMaps)
            RedrawCapitalMaps();
        UpdateTimerCountdowns();
    }

    private void RedrawAll() => RedrawMapLayer();

    private void RedrawOverlays()
    {
        if (Vm == null) return;

        for (var i = MapCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (MapCanvas.Children[i] is FrameworkElement { Tag: "overlay" })
                MapCanvas.Children.RemoveAt(i);
        }

        var w = MapWidth;
        var h = MapHeight;
        if (w <= 0 || h <= 0) return;

        if (Vm.ShowAllianceFlightPaths || Vm.ShowHordeFlightPaths)
        {
            DrawFlightRoutes(MapOverlayData.AllianceRoutes, Vm.ShowAllianceFlightPaths,
                Color.FromArgb(0x99, 0x4A, 0x9E, 0xFF));
            DrawFlightRoutes(MapOverlayData.HordeRoutes, Vm.ShowHordeFlightPaths,
                Color.FromArgb(0x99, 0xFF, 0x44, 0x44));

            foreach (var node in MapOverlayData.FlightNodes)
            {
                var show = node.Faction switch
                {
                    Faction.Alliance => Vm.ShowAllianceFlightPaths,
                    Faction.Horde => Vm.ShowHordeFlightPaths,
                    Faction.Neutral => Vm.ShowAllianceFlightPaths || Vm.ShowHordeFlightPaths,
                    _ => false
                };
                if (!show) continue;

                var fill = node.Faction switch
                {
                    Faction.Alliance => Color.FromRgb(0x4A, 0x9E, 0xFF),
                    Faction.Horde => Color.FromRgb(0xFF, 0x44, 0x44),
                    _ => Color.FromRgb(0xFF, 0xD7, 0x00)
                };

                var dot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(fill),
                    Stroke = Brushes.Black,
                    StrokeThickness = 0.5,
                    Tag = "overlay",
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(dot, node.X * w - 3);
                Canvas.SetTop(dot, node.Y * h - 3);
                Panel.SetZIndex(dot, 1);
                MapCanvas.Children.Add(dot);
            }
        }
    }

    private void DrawFlightRoutes(FlightRoute[] routes, bool visible, Color lineColor)
    {
        if (!visible) return;

        var nodes = MapOverlayData.FlightNodes;
        var w = MapWidth;
        var h = MapHeight;
        var brush = new SolidColorBrush(lineColor);

        foreach (var route in routes)
        {
            if (route.FromIndex < 0 || route.FromIndex >= nodes.Length) continue;
            if (route.ToIndex < 0 || route.ToIndex >= nodes.Length) continue;

            var a = nodes[route.FromIndex];
            var b = nodes[route.ToIndex];
            var line = new Line
            {
                X1 = a.X * w,
                Y1 = a.Y * h,
                X2 = b.X * w,
                Y2 = b.Y * h,
                Stroke = brush,
                StrokeThickness = 1.2,
                Tag = "overlay",
                IsHitTestVisible = false
            };
            Panel.SetZIndex(line, 0);
            MapCanvas.Children.Add(line);
        }
    }

    private void RedrawZoneEditor()
    {
        for (var i = MapCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (MapCanvas.Children[i] is FrameworkElement { Tag: string tag }
                && (tag == ZoneEditTag || tag == "zone-edit-handle"))
                MapCanvas.Children.RemoveAt(i);
        }

        if (Vm == null || !Vm.IsZoneEditMode) return;

        var w = MapWidth;
        var h = MapHeight;
        if (w <= 0 || h <= 0) return;

        var focusZone = Vm.IsZonesPanelOpen && Vm.SelectedZoneRect != null;
        var zonesToDraw = Vm.ZoneRects
            .Where(z => !CartoRuntimeOptions.ShowCapitalMaps || !ClassicEraMapProjection.IsCapitalMap(z.MapId))
            .Where(z => !focusZone || ReferenceEquals(z, Vm.SelectedZoneRect))
            .OrderBy(z => ReferenceEquals(z, Vm.SelectedZoneRect) ? 1 : 0)
            .ToList();

        foreach (var zone in zonesToDraw)
        {
            var selected = ReferenceEquals(zone, Vm.SelectedZoneRect);
            var stroke = selected ? Color.FromRgb(0xFF, 0xE0, 0x66) : Color.FromRgb(0x88, 0xDD, 0x99);
            var fill = selected
                ? Color.FromArgb(0x44, 0xFF, 0xE0, 0x66)
                : Color.FromArgb(0x33, 0x88, 0xDD, 0x99);

            var rect = new Rectangle
            {
                Width = Math.Max(4, zone.Width * w),
                Height = Math.Max(4, zone.Height * h),
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = selected ? 2.5 : 1.5,
                Fill = new SolidColorBrush(fill),
                Tag = ZoneEditTag,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(rect, zone.Left * w);
            Canvas.SetTop(rect, zone.Top * h);
            Panel.SetZIndex(rect, selected ? 80 : 15);
            MapCanvas.Children.Add(rect);

            var label = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x10, 0x18, 0x10)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Child = new TextBlock
                {
                    Text = string.IsNullOrEmpty(zone.DisplayName) ? zone.NameFr : zone.DisplayName,
                    FontSize = 10,
                    Foreground = Brushes.White
                },
                Tag = ZoneEditTag,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, zone.Left * w + 2);
            Canvas.SetTop(label, zone.Top * h + 2);
            Panel.SetZIndex(label, selected ? 81 : 16);
            MapCanvas.Children.Add(label);

            if (selected)
            {
                const double handleSize = 14;
                var handle = new Ellipse
                {
                    Width = handleSize,
                    Height = handleSize,
                    Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x66)),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Tag = "zone-edit-handle",
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(handle, (zone.Left + zone.Width) * w - handleSize / 2);
                Canvas.SetTop(handle, (zone.Top + zone.Height) * h - handleSize / 2);
                Panel.SetZIndex(handle, 82);
                MapCanvas.Children.Add(handle);
            }
        }

        RedrawDungeonMarkers();
    }

    private const string DungeonMarkerTag = "dungeon-marker";

    private void RedrawDungeonMarkers()
    {
        for (var i = MapCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (MapCanvas.Children[i] is FrameworkElement { Tag: string tag }
                && (tag == DungeonMarkerTag || tag == "dungeon-marker-handle"))
                MapCanvas.Children.RemoveAt(i);
        }

        if (Vm == null || !Vm.IsZonesPanelOpen)
            return;

        var w = MapWidth;
        var h = MapHeight;
        if (w <= 0 || h <= 0)
            return;

        var focusDungeon = Vm.SelectedDungeonMarker != null;
        foreach (var marker in Vm.DungeonMarkers)
        {
            if (marker.MapX <= 0 && marker.MapY <= 0)
                continue;
            if (focusDungeon && !ReferenceEquals(marker, Vm.SelectedDungeonMarker))
                continue;

            var selected = ReferenceEquals(marker, Vm.SelectedDungeonMarker);
            var size = selected ? 14.0 : 10.0;
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(selected ? Color.FromRgb(0xBB, 0x99, 0xFF) : Color.FromRgb(0x88, 0x66, 0xCC)),
                Stroke = selected ? Brushes.White : new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                StrokeThickness = selected ? 2 : 1,
                Tag = DungeonMarkerTag,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, marker.MapX * w - size / 2);
            Canvas.SetTop(dot, marker.MapY * h - size / 2);
            Panel.SetZIndex(dot, selected ? 85 : 18);
            MapCanvas.Children.Add(dot);

            if (selected)
            {
                var label = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x28, 0x20, 0x40)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(5, 2, 5, 2),
                    Child = new TextBlock
                    {
                        Text = marker.DisplayName,
                        FontSize = 9,
                        Foreground = Brushes.White
                    },
                    Tag = DungeonMarkerTag,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, marker.MapX * w + size);
                Canvas.SetTop(label, marker.MapY * h - 8);
                Panel.SetZIndex(label, 86);
                MapCanvas.Children.Add(label);
            }
        }
    }

    private bool TryHitDungeonMarker(Point mapPos, out CartoDungeonMarker? marker)
    {
        marker = null;
        if (Vm == null)
            return false;

        const double hitPx = 16;
        var best = double.MaxValue;
        CartoDungeonMarker? bestM = null;
        foreach (var m in Vm.DungeonMarkers)
        {
            if (m.MapX <= 0 && m.MapY <= 0)
                continue;

            var cx = m.MapX * MapWidth;
            var cy = m.MapY * MapHeight;
            var d = Math.Abs(mapPos.X - cx) + Math.Abs(mapPos.Y - cy);
            if (d < hitPx && d < best)
            {
                best = d;
                bestM = m;
            }
        }

        marker = bestM;
        return marker != null;
    }

    private static bool IsZoneResizeHit(CartoZoneRectItem z, double nx, double ny, double handleN)
    {
        var right = z.Left + z.Width;
        var bottom = z.Top + z.Height;
        return nx >= right - handleN && nx <= right + handleN * 0.35
               && ny >= bottom - handleN && ny <= bottom + handleN * 0.35;
    }

    private IEnumerable<CartoZoneRectItem> EnumerateWorldZoneRectsForHitTest()
    {
        if (Vm == null)
            yield break;

        if (Vm.SelectedZoneRect != null
            && (!CartoRuntimeOptions.ShowCapitalMaps || !ClassicEraMapProjection.IsCapitalMap(Vm.SelectedZoneRect.MapId)))
            yield return Vm.SelectedZoneRect;

        for (var i = Vm.ZoneRects.Count - 1; i >= 0; i--)
        {
            var z = Vm.ZoneRects[i];
            if (CartoRuntimeOptions.ShowCapitalMaps && ClassicEraMapProjection.IsCapitalMap(z.MapId))
                continue;
            if (ReferenceEquals(z, Vm.SelectedZoneRect))
                continue;
            yield return z;
        }
    }

    private bool TryHitZone(Point mapPos, out CartoZoneRectItem? zone, out bool isResizeHandle)
    {
        zone = null;
        isResizeHandle = false;
        if (Vm == null || !Vm.IsZoneEditMode) return false;

        var nx = mapPos.X / MapWidth;
        var ny = mapPos.Y / MapHeight;
        const double handlePx = 22;
        var handleN = handlePx / Math.Max(MapWidth, MapHeight);

        foreach (var z in EnumerateWorldZoneRectsForHitTest())
        {
            if (!IsZoneResizeHit(z, nx, ny, handleN))
                continue;

            zone = z;
            isResizeHandle = true;
            return true;
        }

        foreach (var z in EnumerateWorldZoneRectsForHitTest())
        {
            var right = z.Left + z.Width;
            var bottom = z.Top + z.Height;
            if (nx < z.Left || nx > right || ny < z.Top || ny > bottom)
                continue;

            zone = z;
            isResizeHandle = false;
            return true;
        }

        return false;
    }

    private void CharacterRosterHost_SizeChanged(object sender, SizeChangedEventArgs e) =>
        SyncRosterListWidth();

    private void SyncRosterListWidth()
    {
        if (CharacterRosterRoot == null || CharacterRosterHost == null)
            return;

        var inner = CharacterRosterHost.ActualWidth - 16;
        if (inner < 120 || double.IsNaN(inner) || double.IsInfinity(inner))
        {
            CharacterRosterRoot.ClearValue(FrameworkElement.MinWidthProperty);
            return;
        }

        if (Math.Abs(CharacterRosterRoot.MinWidth - inner) > 0.5)
            CharacterRosterRoot.MinWidth = inner;

        CharacterRosterRoot.ClearValue(FrameworkElement.WidthProperty);
    }

    private void CaptureRosterExpandState()
    {
        if (CharacterRosterRoot == null)
            return;

        foreach (var userExp in CharacterRosterRoot.Children
                     .OfType<Border>()
                     .Select(b => b.Child)
                     .OfType<Expander>()
                     .Concat(CharacterRosterRoot.Children.OfType<Expander>()))
        {
            if (userExp.Tag is not string userId)
                continue;

            var userKey = RosterExpandKey(userId, null);
            if (userExp.IsExpanded)
            {
                _rosterExpandedKeys.Add(userKey);
                _rosterCollapsedKeys.Remove(userKey);
            }
            else
            {
                _rosterCollapsedKeys.Add(userKey);
                _rosterExpandedKeys.Remove(userKey);
            }

            if (userExp.Content is not StackPanel userPanel)
                continue;

            foreach (var child in userPanel.Children)
            {
                if (child is not Border { Tag: CharacterStatus category } shell)
                    continue;
                if (shell.Child is not Expander catExp)
                    continue;

                var catKey = RosterExpandKey(userId, category);
                if (catExp.IsExpanded)
                {
                    _rosterExpandedKeys.Add(catKey);
                    _rosterCollapsedKeys.Remove(catKey);
                }
                else
                {
                    _rosterCollapsedKeys.Add(catKey);
                    _rosterExpandedKeys.Remove(catKey);
                }
            }
        }
    }

    private static string RosterExpandKey(string userId, CharacterStatus? category) =>
        category == null ? $"u:{userId}" : $"u:{userId}:c:{category}";

    private bool IsRosterExpanded(string userId, CharacterStatus? category, bool defaultExpanded)
    {
        var key = RosterExpandKey(userId, category);
        if (_rosterCollapsedKeys.Contains(key))
            return false;
        if (_rosterExpandedKeys.Contains(key))
            return true;
        return defaultExpanded;
    }

    private void CharacterRosterList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CharacterRosterList?.SelectedItem is WowCharacter ch)
            ShowCharacterTooltip(ch);
    }

    private void RebuildCharacterRoster()
    {
        if (CartoRuntimeOptions.UseSimpleCharacterList)
        {
            if (CharacterRosterList != null && Vm != null)
                CharacterRosterList.Items.Refresh();
            return;
        }

        if (CharacterRosterRoot == null || Vm == null) return;
        CaptureRosterExpandState();
        CharacterRosterRoot.Children.Clear();
        _suppressRosterExpandEvents = true;
        try
        {
            var allLocalChars = Vm.Characters.Where(c => !c.IsExternal).ToList();

            if (allLocalChars.Count == 0)
            {
                CharacterRosterRoot.Children.Add(new TextBlock
                {
                    Text = "Aucun personnage.\nPanneau Addon : chemin WoW + déployer + actualiser,\nou Paramètres → lier les comptes WTF.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = TryFindResource("SubtextBrush") as Brush ?? Brushes.Gray,
                    Margin = new Thickness(4, 8, 4, 8)
                });
                return;
            }

            var users = Vm.GetOrderedUsers().ToList();
            var assignedCharIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var user in users)
            {
                var userCharsAll = allLocalChars
                    .Where(c => Vm.GetUserIdForCharacter(c) == user.Id)
                    .ToList();
                foreach (var c in userCharsAll)
                    assignedCharIds.Add(c.Id);

                var userPanel = CartoRosterPanelUi.StretchWidth(new StackPanel());
                PopulateUserCategories(userPanel, user, userCharsAll);
                var userTotal = Vm.CountLocalCharactersForUser(user.Id);
                CharacterRosterRoot.Children.Add(BuildUserExpander(user, userTotal, userPanel));
            }

            var orphans = allLocalChars.Where(c => !assignedCharIds.Contains(c.Id)).ToList();
            if (orphans.Count > 0)
            {
                var fallbackUser = users.FirstOrDefault(CartoViewModel.IsDefaultCartoUser)
                                   ?? users.FirstOrDefault();
                if (fallbackUser != null)
                {
                    var userPanel = CartoRosterPanelUi.StretchWidth(new StackPanel());
                    PopulateUserCategories(userPanel, fallbackUser, orphans);
                    var orphanTotal = Vm.CountLocalCharactersForUser(fallbackUser.Id);
                    CharacterRosterRoot.Children.Add(BuildUserExpander(fallbackUser, orphanTotal, userPanel));
                }
            }
        }
        finally
        {
            _suppressRosterExpandEvents = false;
            SyncRosterListWidth();
        }
    }

    private void TrackRosterExpanded(string key)
    {
        if (_suppressRosterExpandEvents) return;
        _rosterExpandedKeys.Add(key);
        _rosterCollapsedKeys.Remove(key);
    }

    private void TrackRosterCollapsed(string key)
    {
        if (_suppressRosterExpandEvents) return;
        _rosterCollapsedKeys.Add(key);
        _rosterExpandedKeys.Remove(key);
    }

    private void PopulateUserCategories(
        StackPanel panel,
        CartoUser user,
        IReadOnlyList<WowCharacter> chars)
    {
        foreach (var status in CartoViewModel.RosterCategoryStatuses)
        {
            var statuses = CartoViewModel.StatusesForRosterCategory(status).ToHashSet();
            var inFrame = chars
                .Where(c => statuses.Contains(c.Status))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var title = CartoViewModel.RosterCategoryTitle(status);
            var totalInCategory = Vm.CountLocalCharactersInCategory(user.Id, status);
            panel.Children.Add(BuildCategoryExpanderForUser(user, title, status, inFrame, totalInCategory));
        }
    }

    private UIElement BuildUserExpander(
        CartoUser user,
        int totalCharacterCount,
        UIElement content)
    {
        var userBrush = CartoCharacterPresentation.GetUserHeaderBrush(user, Vm);
        var userVisToggle = CartoRosterIcons.CreateSubtreeVisibilityToggle(
            () => Vm.IsUserRosterSubtreeVisible(user),
            user.Name,
            () =>
            {
                Vm.ToggleUserRosterSubtreeVisibility(user);
                RedrawMarkers();
                ApplyRosterSubtreeVisibilityUi();
            });

        var header = CartoRosterPanelUi.BuildUserTitleRow(
            user.Name,
            userBrush,
            totalCharacterCount,
            userVisToggle,
            Vm.GetUserTotalGoldCopper(user.Id));

        var expander = CartoRosterPanelUi.StretchExpander(new Expander
        {
            IsExpanded = IsRosterExpanded(user.Id, null, defaultExpanded: true),
            Header = header,
            Content = content,
            Tag = user.Id
        });
        if (Application.Current?.TryFindResource("CartoUserExpander") is Style userStyle)
            expander.Style = userStyle;

        var userKey = RosterExpandKey(user.Id, null);
        expander.Expanded += (_, _) => TrackRosterExpanded(userKey);
        expander.Collapsed += (_, _) => TrackRosterCollapsed(userKey);
        return CartoRosterPanelUi.WrapUserOwnerFrame(expander);
    }

    private static Expander BuildRosterSectionExpander(
        string title,
        Brush headerBrush,
        UIElement content,
        bool nested = false)
    {
        return new Expander
        {
            IsExpanded = true,
            Margin = nested ? new Thickness(4, 0, 0, 4) : new Thickness(0, 0, 0, 6),
            Header = new TextBlock
            {
                Text = title,
                FontSize = nested ? 11 : 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = headerBrush
            },
            Content = content
        };
    }

    private Border BuildCategoryExpanderForUser(
        CartoUser user,
        string title,
        CharacterStatus category,
        IReadOnlyList<WowCharacter> characters,
        int totalInCategory)
    {
        var shell = CartoRosterPanelUi.WrapCategoryFrame(
            category,
            BuildCategoryExpanderContent(user, title, category, characters, totalInCategory));
        shell.Tag = category;
        shell.AllowDrop = true;
        shell.DragOver += StatusFrame_DragOver;
        shell.Drop += StatusFrame_Drop;
        shell.DragLeave += (_, _) => SetHighlightedDropFrame(null);
        return shell;
    }

    private Expander BuildCategoryExpanderContent(
        CartoUser user,
        string title,
        CharacterStatus category,
        IReadOnlyList<WowCharacter> characters,
        int totalInCategory)
    {
        var categoryVisToggle = CartoRosterIcons.CreateSubtreeVisibilityToggle(
            () => Vm.IsCategoryRosterSubtreeVisible(user, category),
            title,
            () =>
            {
                Vm.ToggleCategoryRosterSubtreeVisibility(user, category);
                RedrawMarkers();
                ApplyRosterSubtreeVisibilityUi();
            });

        var headerPanel = CartoRosterPanelUi.StretchWidth(new StackPanel
        {
            Children =
            {
                CartoRosterPanelUi.BuildCategoryTitleRow(
                    category,
                    title,
                    totalInCategory,
                    categoryVisToggle,
                    Vm.GetCategoryGoldCopper(Vm.GetLocalCharactersForUserCategory(user.Id, category)))
            }
        });

        var content = CartoRosterPanelUi.StretchWidth(new StackPanel());

        if (totalInCategory == 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "Glissez un personnage ici",
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 105, 90)),
                Margin = new Thickness(0, 2, 0, 2)
            });
        }
        else
        {
            foreach (var ch in characters)
            {
                var chip = BuildStatusDockChip(ch);
                ApplyChipSubtreeVisibility(chip, ch);
                content.Children.Add(chip);
            }
        }

        var defaultCatExpanded = totalInCategory > 0 && category == CharacterStatus.Main;
        var expander = CartoRosterPanelUi.StretchExpander(new Expander
        {
            Tag = category,
            Header = headerPanel,
            Content = content,
            IsExpanded = IsRosterExpanded(user.Id, category, defaultCatExpanded)
        });
        var catKey = RosterExpandKey(user.Id, category);
        expander.Expanded += (_, _) => TrackRosterExpanded(catKey);
        expander.Collapsed += (_, _) => TrackRosterCollapsed(catKey);

        if (Application.Current?.TryFindResource("CartoCategoryExpander") is Style style)
            expander.Style = style;

        return expander;
    }

    private Border BuildStatusDockChip(WowCharacter ch)
    {
        var captured = ch;
        var callbacks = new CartoDockCardCallbacks
        {
            ToggleMapVisibility = c =>
            {
                Vm.ToggleCharacterMapVisibilityCommand.Execute(c);
                RedrawMarkers();
            },
            ToggleSync = c =>
            {
                Vm.ToggleCharacterSyncCommand.Execute(c);
            },
            OpenDetails = c =>
            {
                Vm.SelectedCharacter = c;
                RedrawMarkers();
                ShowCharacterTooltip(c);
            },
            DragStart = (c, card, e) =>
            {
                _chipDragCharacter = c;
                _chipDragStart = e.GetPosition(card);
                _chipDragStarted = false;
                card.CaptureMouse();
                e.Handled = true;
            },
            DragMove = (c, card, e) =>
            {
                if (_chipDragCharacter != c || e.LeftButton != MouseButtonState.Pressed)
                    return;
                var delta = e.GetPosition(card) - _chipDragStart;
                if (!_chipDragStarted && (Math.Abs(delta.X) > 4 || Math.Abs(delta.Y) > 4))
                {
                    _chipDragStarted = true;
                    DragDrop.DoDragDrop(card, CreateCharacterDragData(c), DragDropEffects.Move);
                    _chipDragCharacter = null;
                    _chipDragStarted = false;
                    card.ReleaseMouseCapture();
                }
            },
            DragEnd = (c, card, e) =>
            {
                if (_chipDragCharacter != c) return;
                _chipDragCharacter = null;
                card.ReleaseMouseCapture();
                if (!_chipDragStarted && e.ChangedButton == MouseButton.Left)
                {
                    Vm.SelectedCharacter = c;
                    RedrawMarkers();
                    ShowCharacterTooltip(c);
                }
                _chipDragStarted = false;
            }
        };

        var card = CartoRosterPanelUi.StretchWidth(CartoCharacterDockCard.Build(ch, Vm, callbacks));
        card.Tag = ch;
        return card;
    }

    private void ApplyChipSubtreeVisibility(Border chip, WowCharacter ch)
    {
        chip.Opacity = Vm!.IsCharacterInVisibleRosterSubtree(ch) ? 1.0 : 0.38;
        chip.IsHitTestVisible = true;
    }

    private void ApplyRosterSubtreeVisibilityUi()
    {
        if (CharacterRosterRoot == null || Vm == null)
            return;

        CartoRosterIcons.RefreshSubtreeVisibilityToggles(CharacterRosterRoot);

        foreach (var userShell in CharacterRosterRoot.Children.OfType<Border>())
        {
            if (userShell.Child is not Expander userExp || userExp.Content is not StackPanel userPanel)
                continue;

            foreach (var catShell in userPanel.Children.OfType<Border>())
            {
                if (catShell.Child is not Expander catExp || catExp.Content is not StackPanel catPanel)
                    continue;

                CartoRosterIcons.RefreshSubtreeVisibilityToggles(catExp);

                foreach (var chip in catPanel.Children.OfType<Border>())
                {
                    if (chip.Tag is WowCharacter ch)
                        ApplyChipSubtreeVisibility(chip, ch);
                }
            }

            CartoRosterIcons.RefreshSubtreeVisibilityToggles(userExp);
        }
    }

    /// <summary>Pastille carte (60 % de la taille d'origine).</summary>
    private const double MapMarkerScale = 0.60;

    private const double MapLabelFontSize = 6;
    private const double MapLabelHeight = 11;
    private static readonly Thickness MapLabelPadding = new(2, 1, 2, 1);
    private const double MapShardIconSize = 7;
    private const double MapShardCountFontSize = 6;

    private static readonly SolidColorBrush MapLabelBg = new(Color.FromArgb(200, 15, 12, 5));
    private static readonly SolidColorBrush MapShardBrush = new(Color.FromRgb(148, 130, 201));
    private static readonly Dictionary<string, SolidColorBrush> ClassBrushCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SolidColorBrush TpBoyStrokeBrush = new(Color.FromRgb(148, 130, 201));
    private static readonly SolidColorBrush DefaultStrokeBrush = new(Color.FromArgb(180, 0, 0, 0));

    static CartoView()
    {
        MapLabelBg.Freeze();
        MapShardBrush.Freeze();
        TpBoyStrokeBrush.Freeze();
        DefaultStrokeBrush.Freeze();
    }

    private static SolidColorBrush GetClassBrush(string hexColor)
    {
        if (ClassBrushCache.TryGetValue(hexColor, out var cached))
            return cached;

        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        brush.Freeze();
        ClassBrushCache[hexColor] = brush;
        return brush;
    }

    private static double GetMapMarkerDotSize(bool isSelected, bool isTpBoy) =>
        (isSelected ? (isTpBoy ? 11.0 : 10.0) : (isTpBoy ? 8.5 : 7.0)) * MapMarkerScale;

    private static double EstimateLabelWidth(string text, bool hasInlineShard)
    {
        var w = text.Length * 3.7 + MapLabelPadding.Left + MapLabelPadding.Right + 6;
        if (hasInlineShard)
            w += MapShardIconSize + 14;
        return Math.Clamp(w, 28, 240);
    }

    private static string GetMapLabelText(WowCharacter ch, CartoViewModel vm)
    {
        var lockPrefix = ch.IsLocked ? "🔒 " : "";
        if (ch.IsExternal && ch.ExternalSource != null)
        {
            var friendName = vm.GetFriendName(ch.ExternalSource)
                               ?? ch.ExternalSource[..Math.Min(8, ch.ExternalSource.Length)];
            return $"{lockPrefix}[{friendName}] {ch.Name}";
        }

        return vm.ShouldShowAccountNameForCharacter(ch)
               && vm.GetCharacterAccountDisplayName(ch) is { } accountName
            ? $"{lockPrefix}{ch.Name} ({accountName})"
            : $"{lockPrefix}{ch.Name}";
    }

    private static Border BuildMapCharacterLabel(
        WowCharacter ch,
        CartoViewModel vm,
        Brush nameBrush,
        bool isSelected,
        bool isTpBoy,
        out bool inlineShard)
    {
        inlineShard = false;
        var labelText = GetMapLabelText(ch, vm);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        row.Children.Add(new TextBlock
        {
            Text = labelText,
            FontSize = MapLabelFontSize,
            Padding = new Thickness(0),
            Foreground = nameBrush,
            FontWeight = isSelected || isTpBoy ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (isTpBoy && ch.Class == WowClass.Demoniste && ch.ShardCount > 0)
        {
            inlineShard = true;
            row.Children.Add(BuildMapInlineShardChip(ch.ShardCount));
        }

        var host = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        host.Children.Add(row);

        return new Border
        {
            Tag = ch,
            Child = host,
            Height = MapLabelHeight,
            MinHeight = MapLabelHeight,
            MaxHeight = MapLabelHeight,
            Background = MapLabelBg,
            CornerRadius = new CornerRadius(1.5),
            Padding = MapLabelPadding
        };
    }

    private static UIElement BuildMapInlineShardChip(int shardCount)
    {
        var shardItem = new WowItem
        {
            ItemId = 6265,
            Name = "Fragment d'âme",
            Count = shardCount,
            Quality = 1
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 0, 0),
            ToolTip = $"{shardCount} fragment(s) d'âme"
        };
        var icon = CartoMapQuestIcon.Create(shardItem, MapShardIconSize, bordered: false);
        icon.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(icon);
        row.Children.Add(new TextBlock
        {
            Text = shardCount.ToString(),
            FontSize = MapShardCountFontSize,
            FontWeight = FontWeights.Bold,
            Foreground = MapShardBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(1, 0, 0, 0)
        });

        return row;
    }

    /// <summary>Position fixe au-dessus de la pastille (sans calcul anti-chevauchement).</summary>
    private static void PlaceMapCharacterLabel(
        Border label, double pixX, double pixY, double dotSize, double labelW, double mapW, double mapH)
    {
        var left = pixX - labelW / 2;
        var top = pixY - dotSize / 2 - MapLabelHeight - 2;
        Canvas.SetLeft(label, Math.Clamp(left, 0, Math.Max(0, mapW - labelW)));
        Canvas.SetTop(label, Math.Clamp(top, 0, Math.Max(0, mapH - MapLabelHeight)));
    }

    private void RedrawMarkers()
    {
        if (Vm == null) return;

        for (int i = MapCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (MapCanvas.Children[i] is Ellipse { Tag: WowCharacter }
                || MapCanvas.Children[i] is Border { Tag: WowCharacter }
                || MapCanvas.Children[i] is Border { Tag: "shard-label" }
                || MapCanvas.Children[i] is Border { Tag: "quest-icons" })
                MapCanvas.Children.RemoveAt(i);
        }

        var mapW = MapWidth;
        var mapH = MapHeight;

        foreach (var ch in Vm.FilteredCharacters
                     .Where(c => Vm.TryGetMarkerPosition(c, out _, out _))
                     .OrderBy(c => c.Status == CharacterStatus.TpBoy ? 1 : 0)
                     .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (CartoRuntimeOptions.ShowCapitalMaps && TryGetCharacterCapitalMapId(ch, Vm, out _))
                continue;

            if (!Vm.TryGetMarkerPosition(ch, out var mapX, out var mapY))
                continue;

            var isTpBoy = ch.Status == CharacterStatus.TpBoy;
            var isSelected = ch == Vm.SelectedCharacter;
            var brush = GetClassBrush(WowClassColors.GetHexColor(ch.Class));
            var size = GetMapMarkerDotSize(isSelected, isTpBoy);

            Brush strokeBrush = isSelected ? Brushes.White
                : isTpBoy ? TpBoyStrokeBrush
                : ch.IsExternal ? Brushes.CornflowerBlue
                : DefaultStrokeBrush;

            var marker = new Ellipse
            {
                Width = size, Height = size,
                Fill = brush,
                Stroke = strokeBrush,
                StrokeThickness = isTpBoy ? 1.5 : (ch.IsExternal ? 1.5 : (isSelected ? 1.5 : 1)),
                Cursor = Cursors.Hand,
                Tag = ch,
                ToolTip = ch.Name
            };
            Panel.SetZIndex(marker, isTpBoy ? 14 : 10);

            var pixX = mapX * mapW;
            var pixY = mapY * mapH;

            Canvas.SetLeft(marker, pixX - size / 2);
            Canvas.SetTop(marker, pixY - size / 2);
            MapCanvas.Children.Add(marker);

            var label = BuildMapCharacterLabel(ch, Vm, brush, isSelected, isTpBoy, out var inlineShard);
            var labelW = EstimateLabelWidth(GetMapLabelText(ch, Vm), inlineShard);
            MapCanvas.Children.Add(label);
            Panel.SetZIndex(label, isTpBoy ? 18 : 15);
            PlaceMapCharacterLabel(label, pixX, pixY, size, labelW, mapW, mapH);
        }
    }

    private void RedrawTimerMarkers()
    {
        if (Vm == null) return;
        for (int i = MapCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (MapCanvas.Children[i] is FrameworkElement fe
                && (fe.Tag is "timer" || fe.Tag is MapTimer))
                MapCanvas.Children.RemoveAt(i);
        }

        foreach (var t in Vm.Timers)
        {
            Brush timerColor;
            if (t.IsRunning) timerColor = Brushes.DeepSkyBlue;
            else if (t.IsPaused) timerColor = Brushes.Gold;
            else timerColor = Brushes.LimeGreen;

            // Draggable ring
            var ring = new Ellipse
            {
                Width = 22, Height = 22,
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 180, 255)),
                Stroke = timerColor, StrokeThickness = 2.5,
                StrokeDashArray = new DoubleCollection([2, 1]),
                Tag = t, Cursor = Cursors.SizeAll, Opacity = 0.9
            };
            var tPixX = t.MapX * MapWidth;
            var tPixY = t.MapY * MapHeight;
            Canvas.SetLeft(ring, tPixX - 11);
            Canvas.SetTop(ring, tPixY - 11);
            MapCanvas.Children.Add(ring);

            // Label + countdown
            string remaining;
            if (t.IsRunning)
                remaining = FormatTimeSpan((TimeSpan?)t.Remaining);
            else if (t.IsPaused)
                remaining = $"⏸ {FormatTimeSpan((TimeSpan?)t.Remaining)}";
            else
                remaining = "⏹";

            var labelPanel = new StackPanel { Orientation = Orientation.Horizontal, Tag = "timer" };
            labelPanel.Children.Add(new TextBlock
            {
                Text = $"⏱ {t.Label}: {remaining}",
                FontSize = 8, Foreground = timerColor, VerticalAlignment = VerticalAlignment.Center
            });

            // Action buttons on map (contextual)
            Button MakeMapBtn(string text, Brush fg, Action action) {
                var b = new Button {
                    Content = text, FontSize = 9, Padding = new Thickness(3, 0, 3, 0),
                    Margin = new Thickness(3, 0, 0, 0), Cursor = Cursors.Hand,
                    Foreground = fg, Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0), Tag = t,
                    MinWidth = 0, MinHeight = 0
                };
                b.Click += (_, _) => { action(); RedrawAll(); };
                return b;
            }

            if (t.IsRunning)
            {
                labelPanel.Children.Add(MakeMapBtn("⏸", Brushes.Gold,
                    () => Vm.StopTimerCommand.Execute(t)));
            }
            else if (t.IsPaused)
            {
                labelPanel.Children.Add(MakeMapBtn("▶", Brushes.LimeGreen,
                    () => Vm.ResumeTimerCommand.Execute(t)));
            }

            labelPanel.Children.Add(MakeMapBtn("↻", Brushes.LightSkyBlue,
                () => Vm.RestartTimerCommand.Execute(t)));
            labelPanel.Children.Add(MakeMapBtn("✕", Brushes.OrangeRed,
                () => Vm.RemoveTimerCommand.Execute(t)));

            var timerLabel = new Border
            {
                Tag = "timer", Child = labelPanel,
                Background = new SolidColorBrush(Color.FromArgb(220, 10, 10, 10)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 2, 5, 2)
            };
            Canvas.SetLeft(timerLabel, tPixX - 30);
            Canvas.SetTop(timerLabel, tPixY + 13);
            MapCanvas.Children.Add(timerLabel);
        }
    }

    private void UpdateCooldownProgressBars()
    {
        if (CharacterRosterHost != null)
            CartoCooldownDisplay.UpdateAll(CharacterRosterHost);

        if (CharPopup.IsOpen && CharPopupBorder != null)
            CartoCooldownDisplay.UpdateAll(CharPopupBorder);
    }

    private void UpdateTimerCountdowns()
    {
        if (TimerList == null) return;
        foreach (var container in TimerList.Items.Cast<object>()
            .Select((_, i) => TimerList.ItemContainerGenerator.ContainerFromIndex(i))
            .OfType<ContentPresenter>())
        {
            var timer = container.Content as MapTimer;
            if (timer == null) continue;

            var tb = FindVisualChildren<TextBlock>(container)
                .FirstOrDefault(t => t.Tag == timer);
            if (tb != null)
            {
                if (timer.IsExpired && !timer.IsRunning)
                {
                    tb.Text = "Terminé";
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 100));
                    tb.FontSize = 10;
                    tb.FontWeight = FontWeights.Normal;
                }
                else if (timer.IsRunning)
                {
                    tb.Text = FormatTimeSpan((TimeSpan?)timer.Remaining);
                    tb.Foreground = Brushes.DeepSkyBlue;
                    tb.FontSize = 12;
                    tb.FontWeight = FontWeights.Bold;
                }
                else
                {
                    tb.Text = "⏸ Pause";
                    tb.Foreground = Brushes.Gold;
                    tb.FontSize = 10;
                    tb.FontWeight = FontWeights.Normal;
                }
            }

            UpdateTimerCardStyle(container, timer);
        }
    }

    private void UpdateTimerCardStyle(ContentPresenter container, MapTimer timer)
    {
        var outerBorder = FindVisualChildren<Border>(container)
            .FirstOrDefault(b => b.Tag is "timerCard");
        if (outerBorder == null) return;

        Color accent;
        if (timer.IsExpired && !timer.IsRunning)
            accent = Color.FromRgb(80, 200, 80);
        else if (timer.IsRunning)
            accent = Color.FromRgb(0, 180, 255);
        else
            accent = Color.FromRgb(255, 215, 0);

        outerBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B));

        var progressBar = FindVisualChildren<Border>(container)
            .FirstOrDefault(b => b.Name == "TimerProgressBar");
        if (progressBar != null && outerBorder.ActualWidth > 0)
        {
            double pct = 0;
            if (timer.IsRunning && timer.DurationSeconds > 0)
                pct = Math.Clamp(timer.Remaining.TotalSeconds / timer.DurationSeconds, 0, 1);
            else if (timer.IsExpired)
                pct = 1;

            progressBar.Width = outerBorder.ActualWidth * pct;
            progressBar.Background = new SolidColorBrush(accent);
            progressBar.Opacity = 0.12;
        }
    }

    private void TimerCard_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border border && border.DataContext is MapTimer timer)
        {
            Color accent;
            if (timer.IsExpired && !timer.IsRunning)
                accent = Color.FromRgb(80, 200, 80);
            else if (timer.IsRunning)
                accent = Color.FromRgb(0, 180, 255);
            else
                accent = Color.FromRgb(255, 215, 0);
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B));
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var sub in FindVisualChildren<T>(child)) yield return sub;
        }
    }

    private void MapBorder_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm == null) return;

        if (Vm.IsPlacingCharacter)
        {
            Vm.CancelPlacement();
            e.Handled = true;
            return;
        }

        if (Vm.IsPlacingTimer)
        {
            Vm.IsPlacingTimer = false;
            e.Handled = true;
        }
    }

    private void MapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm == null)
            return;

        if (e.Handled)
            return;

        if (Vm.IsPlacingDungeonMarker)
        {
            var pos = e.GetPosition(MapImage);
            if (Vm.TryPlaceDungeonMarkerAt(pos.X / MapWidth, pos.Y / MapHeight))
            {
                RedrawZoneEditor();
                e.Handled = true;
                return;
            }
        }

        if (Vm.IsZoneEditMode)
        {
            var mapPos = e.GetPosition(MapImage);
            if (TryHitDungeonMarker(mapPos, out var dungeonHit) && dungeonHit != null)
            {
                Vm.SelectedDungeonMarker = dungeonHit;
                Vm.SelectedZoneRect = null;
                _dungeonDragMarker = dungeonHit;
                _dungeonDragStartMap = mapPos;
                MapBorder.CaptureMouse();
                RedrawZoneEditor();
                e.Handled = true;
                return;
            }

            if (TryHitZone(mapPos, out var hit, out var resize) && hit != null)
            {
                Vm.SelectedZoneRect = hit;
                Vm.SelectedDungeonMarker = null;
                _zoneDragItem = hit;
                _zoneDragCapitalSlot = null;
                _zoneResizeDrag = resize;
                _zoneDragStartMap = mapPos;
                _zoneDragStartLeft = hit.Left;
                _zoneDragStartTop = hit.Top;
                _zoneDragStartW = hit.Width;
                _zoneDragStartH = hit.Height;
                MapBorder.CaptureMouse();
                RedrawZoneEditor();
                e.Handled = true;
                return;
            }

            if (Vm.TryAddZoneAt(mapPos.X / MapWidth, mapPos.Y / MapHeight))
            {
                RedrawZoneEditor();
                e.Handled = true;
                return;
            }

            Vm.SelectedZoneRect = null;
            RedrawZoneEditor();
            e.Handled = true;
            return;
        }

        if (Vm.IsPlacingTimer)
        {
            var pos = e.GetPosition(MapImage);
            Vm.PlaceTimerAt(pos.X / MapWidth, pos.Y / MapHeight);
            RedrawAll();
            e.Handled = true;
            return;
        }

        if (Vm.IsPlacingCharacter)
        {
            var pos = e.GetPosition(MapImage);
            Vm.PlaceCharacterAt(pos.X / MapWidth, pos.Y / MapHeight);
            RedrawAll();
            e.Handled = true;
            return;
        }

        // Marker click : sélection + popup (pas de déplacement manuel)
        if (GetCharacterFromEventSource(e.OriginalSource) is { } ch)
        {
            if (_tooltipCharacter == ch && CharPopup.IsOpen)
                CloseCharacterTooltip();
            else
            {
                Vm.SelectedCharacter = ch;
                RedrawMarkers();
                ShowCharacterTooltip(ch);
            }

            e.Handled = true;
            return;
        }

        // Timer ring: drag
        if (e.OriginalSource is Ellipse { Tag: MapTimer timer })
        {
            _draggingTimer = timer;
            _isDragging = false;
            _panStart = e.GetPosition(MapBorder);
            MapBorder.CaptureMouse();
            e.Handled = true;
            return;
        }

        // Close tooltip if open and clicking outside marker
        if (CharPopup.IsOpen)
            CloseCharacterTooltip();

        _isPanning = true;
        _panStart = e.GetPosition(MapBorder);
        _panStartOffsetX = Vm.MapOffsetX;
        _panStartOffsetY = Vm.MapOffsetY;
        MapBorder.CaptureMouse();
        e.Handled = true;
    }

    private void MapCanvas_RightMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Géré par MapBorder_PreviewMouseRightButtonDown
    }

    private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(MapBorder);

        if (_dungeonDragMarker != null)
        {
            var mapPos = e.GetPosition(MapImage);
            Vm.MoveSelectedDungeonMarker(mapPos.X / MapWidth, mapPos.Y / MapHeight);
            RedrawZoneEditor();
            return;
        }

        if (_zoneDragItem != null)
        {
            var mapPos = e.GetPosition(MapImage);
            var dx = (mapPos.X - _zoneDragStartMap.X) / MapWidth;
            var dy = (mapPos.Y - _zoneDragStartMap.Y) / MapHeight;

            if (_zoneResizeDrag)
            {
                var (minW, minH) = ClassicEraMapProjection.GetEditorMinimumZoneSize(_zoneDragItem.MapId);
                _zoneDragItem.Width = Math.Clamp(_zoneDragStartW + dx, minW, 1 - _zoneDragItem.Left);
                _zoneDragItem.Height = Math.Clamp(_zoneDragStartH + dy, minH, 1 - _zoneDragItem.Top);
            }
            else
            {
                _zoneDragItem.Left = Math.Clamp(_zoneDragStartLeft + dx, 0, 1 - _zoneDragStartW);
                _zoneDragItem.Top = Math.Clamp(_zoneDragStartTop + dy, 0, 1 - _zoneDragStartH);
            }

            RedrawZoneEditor();
            return;
        }

        // Drag timer
        if (_draggingTimer != null)
        {
            var delta = pos - _panStart;
            if (!_isDragging && (Math.Abs(delta.X) > 4 || Math.Abs(delta.Y) > 4))
                _isDragging = true;

            if (_isDragging)
            {
                var mapPos = e.GetPosition(MapImage);
                _draggingTimer.MapX = Math.Clamp(mapPos.X / MapWidth, 0, 1);
                _draggingTimer.MapY = Math.Clamp(mapPos.Y / MapHeight, 0, 1);
                RedrawTimerMarkers();
            }
            return;
        }

        if (_isPanning && Vm != null)
        {
            Vm.MapOffsetX = _panStartOffsetX + (pos.X - _panStart.X);
            Vm.MapOffsetY = _panStartOffsetY + (pos.Y - _panStart.Y);
        }
    }

    private void MapBorder_MouseLeave(object sender, MouseEventArgs e) { }

    private void MapCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dungeonDragMarker != null)
        {
            _dungeonDragMarker = null;
            MapBorder.ReleaseMouseCapture();
            RedrawZoneEditor();
            e.Handled = true;
            return;
        }

        if (_zoneDragItem != null)
        {
            Vm.PersistZoneRects();
            _zoneDragItem = null;
            _zoneDragCapitalSlot = null;
            _zoneResizeDrag = false;
            MapBorder.ReleaseMouseCapture();
            foreach (var slot in _capitalSlots)
                slot.Host.ReleaseMouseCapture();
            RedrawZoneEditor();
            e.Handled = true;
            return;
        }

        // End timer drag
        if (_draggingTimer != null)
        {
            if (_isDragging)
                Vm.Save();
            _draggingTimer = null;
            _isDragging = false;
            MapBorder.ReleaseMouseCapture();
            RedrawAll();
            e.Handled = true;
            return;
        }

        _isPanning = false;
        _isDragging = false;
        MapBorder.ReleaseMouseCapture();
    }

    private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Vm == null)
            return;

        var pos = e.GetPosition(MapBorder);
        var step = e.Delta / 120.0 * 48;

        if (MapScroll != null && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            MapScroll.ScrollToHorizontalOffset(Math.Max(0, MapScroll.HorizontalOffset - step));
            e.Handled = true;
            return;
        }

        if (MapScroll != null && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            MapScroll.ScrollToVerticalOffset(Math.Max(0, MapScroll.VerticalOffset - step));
            e.Handled = true;
            return;
        }

        var factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        Vm.ApplyZoomAt(pos.X, pos.Y, factor);
        e.Handled = true;
    }

    private void MapZoomIn_Click(object sender, RoutedEventArgs e)
        => ApplyMapZoomAtViewportCenter(1.25);

    private void MapZoomOut_Click(object sender, RoutedEventArgs e)
        => ApplyMapZoomAtViewportCenter(1.0 / 1.25);

    private void MapResetView_Click(object sender, RoutedEventArgs e)
    {
        MapScroll?.ScrollToHorizontalOffset(0);
        MapScroll?.ScrollToVerticalOffset(0);
        TryApplyInitialMapFit();
    }

    private void ApplyMapZoomAtViewportCenter(double factor)
    {
        if (Vm == null || MapBorder.ActualWidth <= 0)
            return;

        Vm.ApplyZoomAt(MapBorder.ActualWidth / 2, MapBorder.ActualHeight / 2, factor);
    }

    private static readonly System.Media.SoundPlayer _chimePlayer = new(@"C:\Windows\Media\chimes.wav");

    private void OnTimerExpired(MapTimer t)
    {
        try { _chimePlayer.Play(); } catch { }
        RedrawAll();
    }

    private void TimerRestart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MapTimer t })
        { Vm.RestartTimerCommand.Execute(t); RedrawAll(); }
    }

    private void TimerResume_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MapTimer t })
        { Vm.ResumeTimerCommand.Execute(t); RefreshTimerListButtons(); RedrawAll(); }
    }

    private void TimerStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MapTimer t })
        { Vm.StopTimerCommand.Execute(t); RefreshTimerListButtons(); RedrawAll(); }
    }

    private void TimerRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MapTimer t })
        { Vm.RemoveTimerCommand.Execute(t); RedrawAll(); }
    }

    private void TimerPlayPauseBtn_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is MapTimer t)
        {
            var content = btn.Content as string;
            if (content == "▶") btn.Visibility = t.IsRunning ? Visibility.Collapsed : Visibility.Visible;
            else if (content == "⏸") btn.Visibility = t.IsRunning ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void RefreshTimerListButtons()
    {
        foreach (var cp in Enumerable.Range(0, TimerList.Items.Count)
            .Select(i => TimerList.ItemContainerGenerator.ContainerFromIndex(i))
            .OfType<ContentPresenter>())
        {
            if (cp.Content is not MapTimer t) continue;
            foreach (var btn in FindVisualChildren<Button>(cp))
            {
                var content = btn.Content as string;
                if (content == "▶") btn.Visibility = t.IsRunning ? Visibility.Collapsed : Visibility.Visible;
                else if (content == "⏸") btn.Visibility = t.IsRunning ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void TimerDurationPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not StackPanel sp || sp.DataContext is not MapTimer t) return;
        var ts = TimeSpan.FromSeconds(t.DurationSeconds);
        foreach (var box in sp.Children.OfType<TextBox>())
        {
            switch (box.Tag as string)
            {
                case "h": box.Text = ((int)ts.TotalHours).ToString(); break;
                case "m": box.Text = ts.Minutes.ToString(); break;
                case "s": box.Text = ts.Seconds.ToString(); break;
            }
        }
    }

    private void TimerLabel_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is MapTimer t)
        {
            t.Label = tb.Text;
            Vm.Save();
            RedrawAll();
        }
    }

    private void TimerDuration_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not MapTimer t) return;
        var parent = tb.Parent as StackPanel;
        if (parent == null) return;

        var boxes = parent.Children.OfType<TextBox>().ToList();
        int h = 0, m = 0, s = 0;
        foreach (var box in boxes)
        {
            int.TryParse(box.Text, out var val);
            switch (box.Tag as string)
            {
                case "h": h = val; break;
                case "m": m = val; break;
                case "s": s = val; break;
            }
        }
        var total = h * 3600 + m * 60 + s;
        if (total <= 0) return;

        t.DurationSeconds = total;
        t.IsRunning = false;
        t.StartedAt = null;
        Vm.Save();
        RedrawAll();
    }

    private void ActionChar_Click(object sender, RoutedEventArgs e) => PopupAddChar.IsOpen = true;
    private void PanelRoster_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Roster, true);
        _ = PopulateRosterAsync();
    }

    private void PanelRoster_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Roster, false);
    }

    private void PanelSearch_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Search, true);
    }

    private void PanelSearch_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Search, false);
    }

    private void PanelTimers_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Timers, true);
    }

    private void PanelTimers_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Timers, false);
    }

    private void PanelSettings_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Settings, true);
    }

    private void PanelSettings_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Settings, false);
    }

    private void PanelAddon_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Addon, true);
    }

    private void PanelAddon_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Addon, false);
    }

    private void PanelZones_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Zones, true);
    }

    private void PanelZones_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Zones, false);
    }

    private enum CartoPanel { Roster, Search, Timers, Zones, Settings, Addon }

    private void SetPanelOpen(CartoPanel panel, bool open)
    {
        if (Vm == null) return;
        switch (panel)
        {
            case CartoPanel.Roster:
                Vm.IsRosterOpen = open;
                break;
            case CartoPanel.Search: Vm.IsItemSearchOpen = open; break;
            case CartoPanel.Timers: Vm.IsTimersPanelOpen = open; break;
            case CartoPanel.Zones: Vm.IsZonesPanelOpen = open; break;
            case CartoPanel.Settings: Vm.IsSettingsPanelOpen = open; break;
            case CartoPanel.Addon: Vm.IsAddonPanelOpen = open; break;
        }
        ApplyRightPanelLayout();
    }

    private async Task PopulateRosterAsync()
    {
        if (Vm == null || !Vm.IsRosterOpen)
            return;

        ShowRosterLoadingState(true);
        try
        {
            await Vm.EnsureCharacterDataLoadedAsync().ConfigureAwait(true);
        }
        finally
        {
            if (Vm?.IsRosterOpen == true)
                RebuildCharacterRoster();
        }
    }

    private void ShowRosterLoadingState(bool loading)
    {
        if (CharacterRosterRoot == null)
            return;

        if (!loading)
            return;

        CharacterRosterRoot.Children.Clear();
        CharacterRosterRoot.Children.Add(new TextBlock
        {
            Text = "Chargement des personnages…",
            FontSize = 11,
            Foreground = TryFindResource("SubtextBrush") as Brush ?? Brushes.Gray,
            Margin = new Thickness(4, 12, 4, 8)
        });
    }

    private void CloseItemSearchPanel_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        e.Handled = true;
        SetPanelOpen(CartoPanel.Search, false);
    }

    private void CloseTimersPanel_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        e.Handled = true;
        SetPanelOpen(CartoPanel.Timers, false);
    }

    private void CloseRosterPanel_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        e.Handled = true;
        SetPanelOpen(CartoPanel.Roster, false);
    }

    private void CloseSettingsPanel_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        e.Handled = true;
        SetPanelOpen(CartoPanel.Settings, false);
    }

    private void CloseZonesPanel_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        e.Handled = true;
        SetPanelOpen(CartoPanel.Zones, false);
    }

    private void CloseAddonPanel_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        e.Handled = true;
        SetPanelOpen(CartoPanel.Addon, false);
    }

    private void ZoneRectsListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm == null || sender is not ListBox listBox)
            return;

        var zone = GetListBoxItemDataContext<CartoZoneRectItem>(listBox, e.OriginalSource);
        if (zone == null)
            return;

        if (ReferenceEquals(Vm.SelectedZoneRect, zone))
        {
            Vm.SelectedZoneRect = null;
            listBox.SelectedItem = null;
            ScheduleZoneEditorRedraw();
            e.Handled = true;
        }
    }

    private void DungeonMarkersListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm == null || sender is not ListBox listBox)
            return;

        var marker = GetListBoxItemDataContext<CartoDungeonMarker>(listBox, e.OriginalSource);
        if (marker == null)
            return;

        if (ReferenceEquals(Vm.SelectedDungeonMarker, marker))
        {
            Vm.SelectedDungeonMarker = null;
            listBox.SelectedItem = null;
            ScheduleZoneEditorRedraw();
            e.Handled = true;
        }
    }

    private static T? GetListBoxItemDataContext<T>(ListBox listBox, object source) where T : class
    {
        for (var dep = source as DependencyObject; dep != null; dep = VisualTreeHelper.GetParent(dep))
        {
            if (dep is Button)
                return null;
            if (dep is ListBoxItem { DataContext: T ctx })
                return ctx;
            if (dep == listBox)
                break;
        }

        return null;
    }

    private void ZoneListDelete_Click(object sender, RoutedEventArgs e) => e.Handled = true;

    private void DungeonListDelete_Click(object sender, RoutedEventArgs e) => e.Handled = true;

    private void ApplyRightPanelLayout()
    {
        if (Vm == null || ItemSearchPanel == null || CharacterRosterHost == null)
            return;

        ItemSearchPanel.Visibility = Vm.IsItemSearchOpen ? Visibility.Visible : Visibility.Collapsed;
        if (TimersPanelHost != null)
            TimersPanelHost.Visibility = Vm.IsTimersPanelOpen ? Visibility.Visible : Visibility.Collapsed;
        if (SettingsPanelHost != null)
            SettingsPanelHost.Visibility = Vm.IsSettingsPanelOpen ? Visibility.Visible : Visibility.Collapsed;
        if (AddonPanelHost != null)
            AddonPanelHost.Visibility = Vm.IsAddonPanelOpen ? Visibility.Visible : Visibility.Collapsed;
        if (ZonesPanelHost != null)
            ZonesPanelHost.Visibility = Vm.IsZonesPanelOpen ? Visibility.Visible : Visibility.Collapsed;

        CharacterRosterHost.Visibility = Vm.IsRosterOpen ? Visibility.Visible : Visibility.Collapsed;

        if (Vm.IsRosterOpen)
        {
            CharacterRosterHost.Width = CartoRosterPanelUi.PanelWidth;
            var useSimpleList = CartoRuntimeOptions.UseSimpleCharacterList;
            if (CharacterRosterScroller != null)
                CharacterRosterScroller.Visibility = useSimpleList ? Visibility.Collapsed : Visibility.Visible;
            if (CharacterRosterList != null)
                CharacterRosterList.Visibility = useSimpleList ? Visibility.Visible : Visibility.Collapsed;

            if (Vm.CharactersLoaded && CharacterRosterRoot?.Children.Count == 0 && !useSimpleList)
                RebuildCharacterRoster();
        }

        SyncPanelToolbarToggles();

        var anyPanelOpen = Vm.IsRosterOpen || Vm.IsItemSearchOpen || Vm.IsTimersPanelOpen
                           || Vm.IsZonesPanelOpen || Vm.IsSettingsPanelOpen || Vm.IsAddonPanelOpen;

        if (RightDockHost != null)
            RightDockHost.Visibility = anyPanelOpen ? Visibility.Visible : Visibility.Collapsed;

        if (RootColGap != null)
            RootColGap.Width = anyPanelOpen ? new GridLength(8) : new GridLength(0);
        if (RootColDock != null)
            RootColDock.Width = anyPanelOpen ? GridLength.Auto : new GridLength(0);

        if (Vm.IsItemSearchOpen)
            Vm.UpdateItemSearch();
    }

    private void SyncPanelToolbarToggles()
    {
        if (Vm == null) return;
        _suppressPanelToggleEvents = true;
        try
        {
            if (BtnPanelRoster != null) BtnPanelRoster.IsChecked = Vm.IsRosterOpen;
            if (BtnPanelSearch != null) BtnPanelSearch.IsChecked = Vm.IsItemSearchOpen;
            if (BtnPanelTimers != null) BtnPanelTimers.IsChecked = Vm.IsTimersPanelOpen;
            if (BtnPanelZones != null) BtnPanelZones.IsChecked = Vm.IsZonesPanelOpen;
            if (BtnPanelSettings != null) BtnPanelSettings.IsChecked = Vm.IsSettingsPanelOpen;
            if (BtnPanelAddon != null) BtnPanelAddon.IsChecked = Vm.IsAddonPanelOpen;
        }
        finally
        {
            _suppressPanelToggleEvents = false;
        }
    }
    private void ActionTimer_Click(object sender, RoutedEventArgs e) => PopupAddTimer.IsOpen = true;
    private void PopupClose_Click(object sender, RoutedEventArgs e)
    {
        PopupAddChar.IsOpen = false;
        PopupAddTimer.IsOpen = false;
    }

    private void AccountUserCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Liaison UserId uniquement — enregistrement au clic OK.
    }

    private void RefreshAccountSettings_Click(object sender, RoutedEventArgs e) =>
        Vm.RefreshAccountSettingRows();

    private void SaveAccountSettings_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        Vm.CloseSettingsPanelAfterSave();
        ApplyRightPanelLayout();
        RebuildCharacterRoster();
        RedrawMarkers();
    }


    private void SummaryGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SummaryGrid.SelectedItem is WowCharacter ch)
            ShowCharacterTooltip(ch);
    }

    private void CloseCharacterTooltip()
    {
        CharPopup.IsOpen = false;
        _tooltipCharacter = null;
        Vm.SelectedCharacter = null;
        RedrawMarkers();
    }

    private void ShowCharacterTooltip(WowCharacter ch)
    {
        _tooltipCharacter = ch;
        RebuildTooltipContent(ch);

        if (CharPopupTitle != null)
            CharPopupTitle.Text = string.IsNullOrWhiteSpace(ch.Name) ? "Personnage" : ch.Name;

        CharPopup.PlacementTarget = RootGrid;
        CharPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;

        CharPopup.IsOpen = true;
        if (!ch.IsPlacedOnMap && !ch.IsExternal)
            PositionCharPopupLeftOfRoster();
        else
            PositionCharPopupBesideCharacter(ch);

        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            () =>
            {
                if (!ch.IsPlacedOnMap && !ch.IsExternal)
                    PositionCharPopupLeftOfRoster();
                else
                    PositionCharPopupBesideCharacter(ch);
            });
    }

    private void PositionCharPopupLeftOfRoster()
    {
        const double gap = 14;
        const double defaultPopupW = 540;
        const double defaultPopupH = 420;

        var popupW = CharPopupBorder.ActualWidth > 1 ? CharPopupBorder.ActualWidth : defaultPopupW;
        var popupH = CharPopupBorder.ActualHeight > 1 ? CharPopupBorder.ActualHeight : defaultPopupH;

        if (CharacterRosterHost == null)
        {
            PositionCharPopupAtMouse();
            return;
        }

        Point ptRoot;
        try
        {
            ptRoot = CharacterRosterHost.TransformToAncestor(RootGrid).Transform(new Point(0, 0));
        }
        catch
        {
            PositionCharPopupAtMouse();
            return;
        }

        var gridW = RootGrid.ActualWidth;
        var gridH = RootGrid.ActualHeight;

        var left = ptRoot.X - popupW - gap;
        var top = ptRoot.Y;

        if (left < 8)
            left = Math.Max(8, ptRoot.X + CharacterRosterHost.ActualWidth + gap);

        left = Math.Clamp(left, 8, Math.Max(8, gridW - popupW - 8));
        top = Math.Clamp(top, 8, Math.Max(8, gridH - popupH - 8));

        CharPopup.HorizontalOffset = left;
        CharPopup.VerticalOffset = top;
    }

    private void PositionCharPopupBesideCharacter(WowCharacter ch)
    {
        const double gap = 14;
        const double defaultPopupW = 540;
        const double defaultPopupH = 380;

        var popupW = CharPopupBorder.ActualWidth > 1 ? CharPopupBorder.ActualWidth : defaultPopupW;
        var popupH = CharPopupBorder.ActualHeight > 1 ? CharPopupBorder.ActualHeight : defaultPopupH;

        if (Vm.ShowTableView || MapBorder.Visibility != Visibility.Visible || MapContainer == null)
        {
            PositionCharPopupAtMouse();
            return;
        }

        if (!Vm.TryGetMarkerPosition(ch, out var mx, out var my))
        {
            PositionCharPopupAtMouse();
            return;
        }

        var pixX = mx * MapWidth;
        var pixY = my * MapHeight;

        Point ptRoot;
        try
        {
            ptRoot = MapContainer.TransformToAncestor(RootGrid).Transform(new Point(pixX, pixY));
        }
        catch
        {
            PositionCharPopupAtMouse();
            return;
        }

        var gridW = RootGrid.ActualWidth;
        var gridH = RootGrid.ActualHeight;

        var left = ptRoot.X + gap;
        var top = ptRoot.Y - popupH / 2;

        if (left + popupW > gridW - 8)
            left = ptRoot.X - popupW - gap;

        left = Math.Clamp(left, 8, Math.Max(8, gridW - popupW - 8));
        top = Math.Clamp(top, 8, Math.Max(8, gridH - popupH - 8));

        CharPopup.HorizontalOffset = left;
        CharPopup.VerticalOffset = top;
    }

    private void PositionCharPopupAtMouse()
    {
        var pos = Mouse.GetPosition(RootGrid);
        var popupW = CharPopupBorder.ActualWidth > 1 ? CharPopupBorder.ActualWidth : 540;
        var maxX = Math.Max(0, RootGrid.ActualWidth - popupW);
        var maxY = Math.Max(0, RootGrid.ActualHeight - 80);
        CharPopup.HorizontalOffset = Math.Clamp(pos.X + 12, 8, maxX > 8 ? maxX : pos.X);
        CharPopup.VerticalOffset = Math.Clamp(pos.Y + 12, 8, maxY > 8 ? maxY : pos.Y);
    }

    private void CharPopupDragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        _isDraggingCharPopup = true;
        _charPopupDragStart = e.GetPosition(RootGrid);
        _charPopupDragBaseX = CharPopup.HorizontalOffset;
        _charPopupDragBaseY = CharPopup.VerticalOffset;
        CharPopupDragBar.CaptureMouse();
        e.Handled = true;
    }

    private void CharPopupDragBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingCharPopup) return;
        var pos = e.GetPosition(RootGrid);
        var dx = pos.X - _charPopupDragStart.X;
        var dy = pos.Y - _charPopupDragStart.Y;
        CharPopup.HorizontalOffset = Math.Max(0, _charPopupDragBaseX + dx);
        CharPopup.VerticalOffset = Math.Max(0, _charPopupDragBaseY + dy);
    }

    private void CharPopupDragBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingCharPopup) return;
        _isDraggingCharPopup = false;
        CharPopupDragBar.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void CharPopupClose_Click(object sender, RoutedEventArgs e) => CloseCharacterTooltip();

    private WowCharacter? _notePopupCharacter;

    private void RebuildTooltipContent(WowCharacter ch)
    {
        CharPopupContent.Children.Clear();
        CharPopupActionsHost.Children.Clear();
        CharPopupHeroHost.Child = null;

        var isWowSync = !ch.IsExternal && !string.IsNullOrEmpty(ch.SyncKey);
        if (isWowSync)
            Vm.ApplySyncEnrichment(ch);
        var syncData = isWowSync ? Vm.FindWowSyncCharacter(ch) : null;

        CharPopupHeroHost.Child = BuildCharPopupHero(ch, syncData, isWowSync);
        BuildCharPopupActions(ch, isWowSync);

        _notePopupCharacter = ch;
        CharPopupNoteBox.Text = ch.Note ?? "";
        CharPopupNoteBox.IsReadOnly = ch.IsExternal;
        CharPopupNoteBox.LostFocus -= CharPopupNoteBox_LostFocus;
        CharPopupNoteBox.LostFocus += CharPopupNoteBox_LostFocus;

        var stack = CharPopupContent;
        var goldBrush = new SolidColorBrush(Color.FromRgb(218, 165, 32));

        if (CartoCharacterPresentation.ShowCooldownsBody(ch))
        {
            var cdPanel = CartoCharacterPresentation.BuildCooldownsSummary(ch, 10, syncData);
            if (cdPanel != null)
            {
                if (cdPanel is FrameworkElement fe)
                    fe.Margin = new Thickness(0, 0, 0, 8);
                stack.Children.Add(cdPanel);
            }
        }

        if (CartoCharacterPresentation.ShowInventoryBankSection(ch) && syncData != null)
        {
            var bagsSection = MakeSection("🎒 Inventaire & banque", goldBrush);
            var bagsStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 2, 0, 0)
            };
            bagsStack.Children.Add(WowItemGridPanel.Build($"Inventaire ({syncData.Inventory.Count})", syncData.Inventory));
            bagsStack.Children.Add(new Border { Height = 10 });
            bagsStack.Children.Add(WowItemGridPanel.Build($"Banque ({syncData.Bank.Count})", syncData.Bank));
            ((StackPanel)bagsSection.Child).Children.Add(bagsStack);
            stack.Children.Add(bagsSection);
        }
        else if (!isWowSync && ch.Class == WowClass.Demoniste && CartoCharacterPresentation.IsMinimalBody(ch))
        {
            var shardSection = MakeSection("💎 Fragments d'âme", new SolidColorBrush(Color.FromRgb(148, 130, 201)));
            var bgInput = new SolidColorBrush(Color.FromRgb(30, 26, 18));
            var shardBox = new TextBox
            {
                Text = ch.ShardCount.ToString(), Width = 60, FontSize = 11,
                Background = bgInput, Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(148, 130, 201)),
                BorderThickness = new Thickness(1), Padding = new Thickness(4, 2, 4, 2),
                TextAlignment = TextAlignment.Center, CaretBrush = Brushes.White
            };
            shardBox.LostFocus += (_, _) =>
            {
                if (int.TryParse(shardBox.Text, out var val) && val >= 0)
                    ch.ShardCount = val;
                else
                    shardBox.Text = ch.ShardCount.ToString();
                Vm.Save();
                RedrawMarkers();
                RebuildTooltipContent(ch);
            };
            ((StackPanel)shardSection.Child).Children.Add(shardBox);
            stack.Children.Add(shardSection);
        }

        if (ch.IsExternal)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Confidentialité : réglages fournis par l'émetteur du compte (prochaine mise à jour).",
                FontSize = 10,
                Foreground = CartoCharacterPresentation.DimBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        if (ch.Class == WowClass.Demoniste)
            RedrawMarkers();
    }

    private void CharPopupNoteBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_notePopupCharacter == null)
            return;

        _notePopupCharacter.Note = CharPopupNoteBox.Text;
        Vm.Save();
        RebuildCharacterRoster();
    }

    private UIElement BuildCharPopupHero(WowCharacter ch, WowCharacterData? syncData, bool isWowSync)
    {
        var classBrush = CartoCharacterPresentation.GetClassBrush(ch.Class);
        var nameBrush = CartoCharacterPresentation.GetCharacterNameBrush(ch, Vm);
        var accountName = Vm.GetCharacterAccountDisplayName(ch);

        var shell = new Border
        {
            Padding = new Thickness(14, 12, 14, 12),
            Background = new LinearGradientBrush(
                Color.FromRgb(48, 38, 24), Color.FromRgb(32, 26, 16), 90)
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            },
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, classBrush.Color.R, classBrush.Color.G, classBrush.Color.B)),
            BorderThickness = new Thickness(0, 0, 0, 2)
        };

        var root = new StackPanel();

        UIElement? questContent = null;
        if (CartoCharacterPresentation.ShowQuestBody(ch))
        {
            var questRow = CartoCharacterPresentation.BuildQuestIconRow(ch, syncData, 24, horizontal: true);
            if (questRow.Children.Count > 0)
                questContent = questRow;
        }

        UIElement? headerActions = null;
        if (!ch.IsExternal && isWowSync)
        {
            var toggles = new StackPanel { Orientation = Orientation.Horizontal };
            toggles.Children.Add(CartoRosterIcons.CreateMapVisibilityToggle(ch, c =>
            {
                Vm.ToggleCharacterMapVisibilityCommand.Execute(c);
                RebuildTooltipContent(c);
                RedrawMarkers();
                RebuildCharacterRoster();
            }));
            toggles.Children.Add(CartoRosterIcons.CreateSyncToggle(ch, c =>
            {
                Vm.ToggleCharacterSyncCommand.Execute(c);
                RebuildTooltipContent(c);
            }));
            headerActions = toggles;
        }

        var heroGrid = new Grid();
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var questCol = -1;
        var actionsCol = -1;
        if (questContent != null)
        {
            questCol = heroGrid.ColumnDefinitions.Count;
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        if (headerActions != null)
        {
            actionsCol = heroGrid.ColumnDefinitions.Count;
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        var portrait = CartoCharacterPresentation.BuildPortraitIcons(
            ch,
            64,
            new Thickness(0, 0, 14, 0),
            showCooldownBars: false,
            sync: syncData);
        portrait.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        Grid.SetColumn(portrait, 0);
        heroGrid.Children.Add(portrait);

        var infoCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var nameLine = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        nameLine.Inlines.Add(new System.Windows.Documents.Run(ch.Name)
        {
            FontWeight = FontWeights.Bold,
            Foreground = nameBrush,
            FontSize = 18
        });
        nameLine.Inlines.Add(new System.Windows.Documents.Run($"  Niv. {ch.Level}")
        {
            FontSize = 13,
            Foreground = Brushes.White
        });
        CartoCharacterPresentation.AppendXpToNameLine(nameLine, Vm.GetCharacterXpPercent(ch), ch.Level);
        infoCol.Children.Add(nameLine);

        if (!string.IsNullOrEmpty(accountName))
        {
            infoCol.Children.Add(new TextBlock
            {
                Text = accountName,
                FontSize = 11,
                Foreground = CartoCharacterPresentation.DimBrush,
                Margin = new Thickness(0, 3, 0, 0)
            });
        }

        var goldRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 0)
        };
        if (syncData is { Gold: > 0 })
            goldRow.Children.Add(WowCurrencyDisplay.Build(syncData.Gold, iconSize: 18, fontSize: 13));
        else if (!isWowSync)
        {
            goldRow.Children.Add(new TextBlock
            {
                Text = "— PO",
                FontSize = 11,
                Foreground = CartoCharacterPresentation.DimBrush
            });
        }

        if (ch.Class == WowClass.Demoniste && ch.ShardCount > 0)
        {
            goldRow.Children.Add(new TextBlock
            {
                Text = $"  ·  💎 {ch.ShardCount}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 130, 201)),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        if (goldRow.Children.Count > 0)
            infoCol.Children.Add(goldRow);

        var zoneLbl = CartoCharacterPresentation.BuildZoneLabel(Vm.GetCharacterZoneLabel(ch), 10, maxWidth: 220);
        if (zoneLbl != null)
        {
            zoneLbl.Margin = new Thickness(0, 4, 0, 0);
            infoCol.Children.Add(zoneLbl);
        }

        var positionText = Vm.GetCharacterPositionDisplay(ch);
        if (!string.IsNullOrWhiteSpace(positionText))
        {
            infoCol.Children.Add(new TextBlock
            {
                Text = positionText,
                FontSize = 10,
                Foreground = CartoCharacterPresentation.ZoneBrush,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 240,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        var syncLbl = CartoCharacterPresentation.BuildSyncDateLabel(Vm.GetCharacterSyncLabel(ch), 9);
        if (syncLbl != null)
        {
            syncLbl.Margin = new Thickness(0, 2, 0, 0);
            infoCol.Children.Add(syncLbl);
        }

        Grid.SetColumn(infoCol, 1);
        heroGrid.Children.Add(infoCol);

        if (questContent != null)
        {
            Grid.SetColumn(questContent, questCol);
            questContent.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            questContent.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 0, headerActions != null ? 8 : 0, 0));
            heroGrid.Children.Add(questContent);
        }

        if (headerActions != null)
        {
            Grid.SetColumn(headerActions, actionsCol);
            headerActions.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            heroGrid.Children.Add(headerActions);
        }

        root.Children.Add(heroGrid);

        var heroCdStrip = CartoCooldownDisplay.BuildRosterCardStrip(ch, syncData);
        if (heroCdStrip is FrameworkElement heroCdFe)
        {
            heroCdFe.Margin = new Thickness(0, 10, 0, 0);
            root.Children.Add(heroCdFe);
        }

        if (!ch.IsExternal)
        {
            var categoryBlock = new Border
            {
                Margin = new Thickness(0, 12, 0, 0),
                Padding = new Thickness(10, 8, 10, 8),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(90, 20, 16, 8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(90, 75, 45)),
                BorderThickness = new Thickness(1)
            };
            var categoryPanel = new StackPanel();
            categoryPanel.Children.Add(new TextBlock
            {
                Text = "Catégorie dans le bandeau",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 200, 150)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            categoryPanel.Children.Add(BuildCategoryCombo(ch));
            categoryBlock.Child = categoryPanel;
            root.Children.Add(categoryBlock);
        }

        shell.Child = root;
        return shell;
    }

    private ComboBox BuildCategoryCombo(WowCharacter ch)
    {
        var statusCombo = new ComboBox
        {
            Height = 32,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 200,
            ToolTip = "Personnages, Banque, TP Boy, Clic Boys…"
        };

        ComboBoxItem? selectedItem = null;
        foreach (var s in CartoViewModel.RosterCategoryStatuses)
        {
            var item = new ComboBoxItem
            {
                Content = CartoViewModel.RosterCategoryTitle(s),
                Tag = s,
                FontSize = 12
            };
            statusCombo.Items.Add(item);
            if (s == ch.Status || (s == CharacterStatus.Main && ch.Status == CharacterStatus.Reroll))
                selectedItem = item;
        }

        if (selectedItem != null)
            statusCombo.SelectedItem = selectedItem;

        var suppress = true;
        statusCombo.SelectionChanged += (_, _) =>
        {
            if (suppress) return;
            if (statusCombo.SelectedItem is not ComboBoxItem { Tag: CharacterStatus s })
                return;

            Vm.SetCharacterStatus(ch, s);
            RebuildTooltipContent(ch);
            RebuildCharacterRoster();
            RedrawMarkers();
        };
        suppress = false;
        return statusCombo;
    }

    private void BuildCharPopupActions(WowCharacter ch, bool isWowSync)
    {
        CharPopupActionsHost.Children.Clear();

        if (isWowSync && !ch.IsExternal)
        {
            CharPopupActionsHost.Children.Add(MakeActionButton("📍 Placer sur la carte (WowSync)", "#FF66AAFF", () =>
            {
                var error = Vm.TryPlaceCharacterFromWowSync(ch);
                if (!string.IsNullOrWhiteSpace(error))
                    MessageBox.Show(error, "Placement WowSync", MessageBoxButton.OK, MessageBoxImage.Information);

                RedrawAll();
                RebuildTooltipContent(ch);
            }));
        }

        if (!ch.IsExternal && !isWowSync)
        {
            CharPopupActionsHost.Children.Add(MakeActionButton("🗑 Suppr", "#FFCC3333", () =>
            {
                if (MessageBox.Show($"Supprimer \"{ch.Name}\" ?", "Confirmation",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    CloseCharacterTooltip();
                    Vm.RemoveCharacterCommand.Execute(ch);
                    RedrawAll();
                }
            }));
        }

    }


    private static string FormatProfession(ProfessionType type) => type switch
    {
        ProfessionType.Travail_du_cuir => "Travail du cuir",
        ProfessionType.Exploitation_miniere => "Minage",
        ProfessionType.Depecage => "Dépeçage",
        _ => type.ToString().Replace('_', ' ')
    };

    private static Border MakeSection(string title, Brush titleBrush)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(70, 58, 28)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(38, 255, 215, 0)),
            Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 8)
        };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = title, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = titleBrush, Margin = new Thickness(0, 0, 0, 6)
        });
        border.Child = sp;
        return border;
    }

    private static Button MakeSmallButton(string content, Brush fg, Action onClick)
    {
        var btn = new Button
        {
            Content = content, FontSize = 9, Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(3, 0, 0, 0), Foreground = fg,
            Background = new SolidColorBrush(Color.FromRgb(35, 30, 20)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(70, 60, 40)),
            BorderThickness = new Thickness(1), Cursor = Cursors.Hand
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button MakeActionButton(string content, string colorHex, Action onClick)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        var btn = new Button
        {
            Content = content, FontSize = 9, FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 6, 0),
            Foreground = new SolidColorBrush(color),
            Background = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1), Cursor = Cursors.Hand
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static TextBlock MakeLabel(string text, int row, int col, Brush fg)
    {
        var tb = new TextBlock
        {
            Text = text, FontSize = 10, Foreground = fg,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 2)
        };
        Grid.SetRow(tb, row);
        Grid.SetColumn(tb, col);
        return tb;
    }

    private static string FormatTimeSpan(TimeSpan? ts)
    {
        if (ts == null) return "—";
        if (ts.Value.TotalHours >= 1)
            return $"{(int)ts.Value.TotalHours}h{ts.Value.Minutes:D2}";
        if (ts.Value.TotalSeconds < 60)
            return $"{ts.Value.Seconds}s";
        return $"{(int)ts.Value.TotalMinutes}m{ts.Value.Seconds:D2}s";
    }

    private static CooldownType[] GetAvailableCooldowns(WowCharacter ch)
    {
        var profs = ch.Professions.Select(p => p.Type).ToHashSet();
        var result = new List<CooldownType>();

        if (profs.Contains(ProfessionType.Alchimie)
            && !ch.Cooldowns.Any(c => CooldownGroups.IsAlchemyTransmute(c.Type)))
        {
            result.Add(CooldownType.Arcanite);
            result.Add(CooldownType.Transmute_Elementaire);
        }
        if (profs.Contains(ProfessionType.Couture))
            result.Add(CooldownType.Mooncloth);
        if (profs.Contains(ProfessionType.Travail_du_cuir))
            result.Add(CooldownType.Sel_raffine);

        return result.Where(ct => !ch.Cooldowns.Any(c => c.Type == ct)).ToArray();
    }

    private static DataObject CreateCharacterDragData(WowCharacter ch) =>
        new(CharacterDragFormat, ch);

    private static bool TryGetCharacterFromDrag(DragEventArgs e, out WowCharacter? character)
    {
        character = null;
        if (!e.Data.GetDataPresent(CharacterDragFormat))
            return false;
        character = e.Data.GetData(CharacterDragFormat) as WowCharacter;
        return character != null;
    }

    private static WowCharacter? GetCharacterFromEventSource(object? source)
    {
        if (source is Ellipse { Tag: WowCharacter ch })
            return ch;
        if (source is Border { Tag: WowCharacter ch2 })
            return ch2;

        if (source is DependencyObject d)
        {
            var node = d;
            while (node != null)
            {
                if (node is Ellipse { Tag: WowCharacter ch3 })
                    return ch3;
                if (node is Border { Tag: WowCharacter ch4 })
                    return ch4;
                node = VisualTreeHelper.GetParent(node);
            }
        }

        return null;
    }

    private bool TryGetStatusDropTarget(out CharacterStatus category)
    {
        category = default;
        if (CharacterRosterRoot == null)
            return false;

        var pos = Mouse.GetPosition(CharacterRosterRoot);
        if (FindStatusFrameAtPoint(pos) is { Tag: CharacterStatus status })
        {
            category = status;
            return true;
        }

        return false;
    }

    private void SetHighlightedDropFrame(Border? frame)
    {
        if (_highlightedDropFrame == frame)
            return;

        if (_highlightedDropFrame != null)
            _highlightedDropFrame.BorderBrush = new SolidColorBrush(Color.FromRgb(70, 58, 32));

        _highlightedDropFrame = frame;
        if (frame != null)
            frame.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 215, 0));
    }

    private Border? FindStatusFrameAtPoint(Point positionOnRosterRoot)
    {
        if (CharacterRosterRoot == null)
            return null;

        foreach (var frame in EnumerateStatusFrames(CharacterRosterRoot))
        {
            var topLeft = frame.TranslatePoint(new Point(0, 0), CharacterRosterRoot);
            var rect = new Rect(topLeft.X, topLeft.Y, frame.ActualWidth, frame.ActualHeight);
            if (rect.Contains(positionOnRosterRoot))
                return frame;
        }

        return null;
    }

    private static IEnumerable<Border> EnumerateStatusFrames(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Border border && border.Tag is CharacterStatus)
                yield return border;

            foreach (var nested in EnumerateStatusFrames(child))
                yield return nested;
        }
    }

    private void StatusFrame_DragOver(object sender, DragEventArgs e)
    {
        if (!TryGetCharacterFromDrag(e, out _))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
        if (sender is Border frame)
            SetHighlightedDropFrame(frame);
    }

    private void StatusFrame_Drop(object sender, DragEventArgs e)
    {
        SetHighlightedDropFrame(null);
        if (!TryGetCharacterFromDrag(e, out var ch) || ch == null)
            return;

        if (sender is Border { Tag: CharacterStatus status })
            Vm.MoveCharacterToCategoryFrame(ch, status);

        CloseCharacterTooltip();
        RedrawAll();
        e.Handled = true;
    }

    private void CharacterRoster_DragOver(object sender, DragEventArgs e)
    {
        if (!TryGetCharacterFromDrag(e, out _))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            SetHighlightedDropFrame(null);
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        var pos = e.GetPosition(CharacterRosterRoot);
        SetHighlightedDropFrame(FindStatusFrameAtPoint(pos));
    }

    private void CharacterRoster_Drop(object sender, DragEventArgs e)
    {
        SetHighlightedDropFrame(null);
        if (!TryGetCharacterFromDrag(e, out var ch) || ch == null)
            return;

        var pos = e.GetPosition(CharacterRosterRoot);
        if (FindStatusFrameAtPoint(pos) is { Tag: CharacterStatus status })
            Vm.MoveCharacterToCategoryFrame(ch, status);

        CloseCharacterTooltip();
        RedrawAll();
        e.Handled = true;
    }

    private void MapBorder_DragOver(object sender, DragEventArgs e)
    {
        if (!TryGetCharacterFromDrag(e, out _))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void MapBorder_Drop(object sender, DragEventArgs e)
    {
        if (!TryGetCharacterFromDrag(e, out var ch) || ch == null)
            return;

        var pos = e.GetPosition(MapImage);
        if (MapWidth <= 0 || MapHeight <= 0)
            return;

        Vm.PlaceCharacterOnMapAt(ch, pos.X / MapWidth, pos.Y / MapHeight);
        Vm.SelectedCharacter = ch;
        RedrawAll();
        ShowCharacterTooltip(ch);
        e.Handled = true;
    }

    private static string FormatQuestItem(QuestItemType type) => type switch
    {
        QuestItemType.Tete_de_Rend => "Tête de Rend",
        QuestItemType.Tete_dOnyxia => "Tête d'Onyxia",
        QuestItemType.Tete_de_Nefarian => "Tête de Nefarian",
        QuestItemType.Coeur_de_Hakkar => "Cœur de Hakkar",
        _ => type.ToString()
    };

    private UIElement? BuildCharacterIndicators(WowCharacter ch, int fontSize = 10)
    {
        var wrap = new WrapPanel { Margin = new Thickness(0, 3, 0, 0) };
        var goldBrush = new SolidColorBrush(Color.FromRgb(255, 215, 80));
        var dimGold = new SolidColorBrush(Color.FromRgb(160, 140, 70));
        var shardBrush = new SolidColorBrush(Color.FromRgb(148, 130, 201));
        var profBrush = new SolidColorBrush(Color.FromRgb(190, 175, 140));
        var cdReady = new SolidColorBrush(Color.FromRgb(120, 220, 120));
        var cdWait = new SolidColorBrush(Color.FromRgb(100, 180, 255));

        void AddBadge(string text, Brush foreground, string tooltip)
        {
            wrap.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(45, 30, 25, 15)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 215, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(0, 0, 4, 3),
                ToolTip = tooltip,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = fontSize,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = foreground
                }
            });
        }

        foreach (var q in ch.QuestItems)
        {
            var has = q.HasItem;
            var planned = q.PlannedTurnIn != null;
            if (!has && !planned) continue;

            var (icon, label) = q.Type switch
            {
                QuestItemType.Tete_de_Rend => ("🗣", "Tête Rend"),
                QuestItemType.Tete_dOnyxia => ("🗣", "Tête Onyxia"),
                QuestItemType.Tete_de_Nefarian => ("🗣", "Tête Nefarian"),
                QuestItemType.Coeur_de_Hakkar => ("❤", "Cœur Hakkar"),
                _ => ("🏆", q.Type.ToString())
            };
            var shortLabel = q.Type switch
            {
                QuestItemType.Tete_de_Rend => "Rd",
                QuestItemType.Tete_dOnyxia => "On",
                QuestItemType.Tete_de_Nefarian => "Nf",
                QuestItemType.Coeur_de_Hakkar => "Hk",
                _ => "?"
            };
            AddBadge(
                $"{icon}{shortLabel}",
                has ? goldBrush : dimGold,
                has ? $"{label} — en sac/banque" : $"{label} — tour prévu");
        }

        if (ch.Class == WowClass.Demoniste && ch.ShardCount > 0)
        {
            var shardItem = new Models.WowSync.WowItem { ItemId = 6265, Name = "Fragment d'âme", Count = ch.ShardCount, Quality = 1 };
            wrap.Children.Add(BuildIconAmountBadge(CartoMapQuestIcon.Create(shardItem, 18), ch.ShardCount.ToString(), shardBrush,
                $"{ch.ShardCount} fragment(s) d'âme"));
        }

        if (Vm != null)
        {
            var sync = Vm.FindWowSyncCharacter(ch);
            if (sync is { Gold: > 0 })
            {
                wrap.Children.Add(BuildMoneyBadge(sync.Gold, sync.GoldDisplay));
            }
        }

        foreach (var prof in ch.Professions)
            AddBadge(ProfessionShort(prof.Type), profBrush, $"{FormatProfession(prof.Type)} ({prof.Skill})");

        foreach (var cd in ch.Cooldowns.Where(c => c.LastUsed != null))
        {
            var shortName = CdShortName(cd.Type);
            if (cd.IsReady)
                AddBadge($"✅{shortName}", cdReady, $"{cd.Type.DisplayName()} — prêt");
            else
            {
                var rem = FormatTimeSpan(cd.TimeRemaining);
                AddBadge($"⏱{shortName}", cdWait, $"{cd.Type.DisplayName()} — {rem}");
            }
        }

        return wrap.Children.Count > 0 ? wrap : null;
    }

    private static Border BuildIconAmountBadge(UIElement icon, string amount, Brush amountBrush, string tooltip)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(icon);
        row.Children.Add(new TextBlock
        {
            Text = amount,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = amountBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(3, 0, 0, 0)
        });
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(45, 30, 25, 15)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 215, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 2, 6, 2),
            Margin = new Thickness(0, 0, 4, 3),
            ToolTip = tooltip,
            Child = row
        };
    }

    private static Border BuildMoneyBadge(long copperTotal, string tooltip)
    {
        var row = WowCurrencyDisplay.Build(copperTotal, iconSize: 14, fontSize: 11);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(45, 30, 25, 15)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 215, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 2, 6, 2),
            Margin = new Thickness(0, 0, 4, 3),
            ToolTip = tooltip,
            Child = row
        };
    }

    private static string ProfessionShort(ProfessionType type) => type switch
    {
        ProfessionType.Alchimie => "Al",
        ProfessionType.Forge => "Fo",
        ProfessionType.Enchantement => "En",
        ProfessionType.Ingenierie => "In",
        ProfessionType.Herboristerie => "He",
        ProfessionType.Couture => "Co",
        ProfessionType.Travail_du_cuir => "Cu",
        ProfessionType.Exploitation_miniere => "Mi",
        ProfessionType.Depecage => "Dp",
        ProfessionType.Peche => "Pê",
        ProfessionType.Cuisine => "Ci",
        ProfessionType.Secourisme => "Se",
        _ => type.ToString()[..Math.Min(2, type.ToString().Length)]
    };

    private static string CdShortName(CooldownType t) => t switch
    {
        CooldownType.Arcanite => "Arc",
        CooldownType.Transmute_Elementaire => "Él",
        CooldownType.Mooncloth => "Lun",
        CooldownType.Sel_raffine => "Sel",
        _ => t.ToString()[..Math.Min(3, t.ToString().Length)]
    };
}
