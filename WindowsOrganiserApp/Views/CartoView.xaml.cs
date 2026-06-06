using System.ComponentModel;
using System.Threading;
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
using WindowsOrganiserApp.Services;
using SpecialAzerothService.Core.Models.Carto;
using WindowsOrganiserApp;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

public partial class CartoView : UserControl
{
    private CartoViewModel? Vm => DataContext as CartoViewModel;
    private bool _isPanning;
    private Point _panStart;
    private double _panStartScrollH;
    private double _panStartScrollV;
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
    private int _rosterPanelBuildGeneration;
    private DispatcherTimer? _mapMarkersDebounce;
    private int _lastMarkerRevision = int.MinValue;
    private bool _mapLayoutEventsWired;
    private bool _suppressPanelToggleEvents;
    private CartoPanel? _returnPanelAfterCharacter;

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
                _subscribedVm.CharactersRescanned -= OnCharactersRescanned;
                _subscribedVm.RosterRefreshRequested -= OnRosterRefreshRequested;
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
                vm.CharactersRescanned += OnCharactersRescanned;
                vm.RosterRefreshRequested += OnRosterRefreshRequested;
                ApplyRightPanelLayout();
                SyncPanelToolbarToggles();
                UpdateMapCursor();
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

            Dispatcher.BeginInvoke(ActivateCartoTab, DispatcherPriority.Loaded);
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

        WireCapitalSlots();
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
        SyncPanelToolbarToggles();
    }

    private void MapImage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0 && (_cartoUiLive || _cartoInit.IsComplete))
            RequestMapMarkersRefresh();
        ScheduleZoneEditorRedraw();
    }

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
        RedrawCapitalMaps();
    }

    private void CartoView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        if (Vm?.IsCharacterDetailOpen == true)
        {
            NavigateBackFromCharacterDetail();
            e.Handled = true;
            return;
        }

        if (CharPopup.IsOpen)
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
        if (!_cartoUiLive && !_cartoInit.IsComplete)
            return;

        _mapMarkersDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _mapMarkersDebounce.Stop();
        _mapMarkersDebounce.Tick -= MapMarkersDebounce_Tick;
        _mapMarkersDebounce.Tick += MapMarkersDebounce_Tick;
        _mapMarkersDebounce.Start();
    }

    /// <summary>Dessine les marqueurs tout de suite (démarrage / retour onglet Carto).</summary>
    private void PaintMapMarkers(bool force = false)
    {
        if (Vm == null)
            return;

        if (!force && !IsVisible)
            return;

        _mapMarkersDebounce?.Stop();

        var revision = ComputeMarkerRevision();
        if (!force && revision == _lastMarkerRevision)
            return;

        _lastMarkerRevision = revision;
        RedrawMarkers();
        if (CartoRuntimeOptions.ShowCapitalMaps)
            RedrawCapitalMaps();
        if (Vm.IsZoneEditMode)
            RedrawZoneEditor();
    }

    private int ComputeMarkerRevision()
    {
        if (Vm == null)
            return 0;

        var hash = new HashCode();
        hash.Add(Vm.SelectedCharacter?.Id);
        hash.Add(CartoRuntimeOptions.ShowCapitalMaps);
        hash.Add((int)Math.Round(MapWidth));
        hash.Add((int)Math.Round(MapHeight));

        foreach (var ch in Vm.FilteredCharacters)
        {
            hash.Add(ch.Id);
            hash.Add(ch.IsPlacedOnMap);
            hash.Add(ch.IsHidden);
            hash.Add(ch.Status);
            hash.Add(ch.Class);
            hash.Add(ch.ShardCount);
            if (Vm.TryGetMarkerPosition(ch, out var x, out var y))
            {
                hash.Add((int)Math.Round(x * 10000));
                hash.Add((int)Math.Round(y * 10000));
            }
        }

        return hash.ToHashCode();
    }

    private void MapMarkersDebounce_Tick(object? sender, EventArgs e)
    {
        _mapMarkersDebounce?.Stop();
        PaintMapMarkers();
    }

    private void UpdateMapCursor()
    {
        if (MapWorldHost == null || Vm == null)
            return;

        if (_isPanning)
        {
            Mouse.OverrideCursor = Cursors.SizeAll;
            return;
        }

        Mouse.OverrideCursor = null;
        MapWorldHost.Cursor = Vm.IsPlacingZone ? Cursors.Cross : Cursors.Arrow;
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

    private DateTime _lastCooldownBarRefreshUtc = DateTime.MinValue;

    private static readonly SolidColorBrush TimerExpiredBrush = new(Color.FromRgb(100, 200, 100));
    private static readonly SolidColorBrush TimerRunningBrush = Brushes.DeepSkyBlue;
    private static readonly SolidColorBrush TimerPausedBrush = Brushes.Gold;

    private void OnViewModelSecondTick(object? sender, EventArgs e)
    {
        if (!IsVisible)
            return;

        if (Vm?.IsTimersPanelOpen == true)
            UpdateTimerCountdowns();

        if (CharacterRosterHost?.Visibility != Visibility.Visible
            && Vm?.IsCharacterDetailOpen != true
            && Vm?.IsCooldownRosterOpen != true
            && !CharPopup.IsOpen)
            return;

        var now = DateTime.UtcNow;
        if ((now - _lastCooldownBarRefreshUtc).TotalSeconds < 2.5)
            return;

        _lastCooldownBarRefreshUtc = now;
        UpdateCooldownProgressBars();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CartoViewModel vm)
            return;

        if (e.PropertyName is nameof(CartoViewModel.IsZoneEditMode)
            or nameof(CartoViewModel.IsZonesPanelOpen)
            or nameof(CartoViewModel.ShowMapOverlays)
            or nameof(CartoViewModel.OverlayChanged)
            or nameof(CartoViewModel.SelectedZoneRect)
            or nameof(CartoViewModel.SelectedDungeonMarker)
            or nameof(CartoViewModel.IsPlacingDungeonMarker)
            or nameof(CartoViewModel.ZoneToAddMapId)
            or nameof(CartoViewModel.IsPlacingZone)
            or nameof(CartoViewModel.IsPlacingCharacter)
            or nameof(CartoViewModel.IsPlacingTimer))
        {
            if (e.PropertyName is nameof(CartoViewModel.IsZonesPanelOpen) && vm.IsZonesPanelOpen)
                WireCapitalSlots();

            UpdateMapCursor();

            if (e.PropertyName is nameof(CartoViewModel.OverlayChanged)
                && (vm.ShowAllianceFlightPaths || vm.ShowHordeFlightPaths))
                RedrawOverlays();
            ScheduleZoneEditorRedraw();
            if (e.PropertyName is not nameof(CartoViewModel.SelectedZoneRect)
                && e.PropertyName is not nameof(CartoViewModel.SelectedDungeonMarker))
                RequestMapMarkersRefresh();
            return;
        }

        if (e.PropertyName is nameof(CartoViewModel.MapZoom))
            return;

        if (e.PropertyName is nameof(CartoViewModel.IsRosterOpen)
            or nameof(CartoViewModel.IsCooldownRosterOpen)
            or nameof(CartoViewModel.IsCharacterDetailOpen)
            or nameof(CartoViewModel.IsItemSearchOpen)
            or nameof(CartoViewModel.IsTimersPanelOpen)
            or nameof(CartoViewModel.IsZonesPanelOpen)
            or nameof(CartoViewModel.IsSettingsPanelOpen))
        {
            ApplyRightPanelLayout();
            SyncPanelToolbarToggles();
            return;
        }

        var isLoadEvent = e.PropertyName == CartoViewModel.CharactersLoadedPropertyName;
        if (!_cartoUiLive && !isLoadEvent)
            return;

        if (e.PropertyName is nameof(CartoViewModel.FilteredCharacters))
        {
            RequestMapMarkersRefresh();
        }
        else if (e.PropertyName is nameof(CartoViewModel.CartoUsers))
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
                RebuildAllRosterPanels();
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

        Dispatcher.BeginInvoke(RebuildAllRosterPanels, DispatcherPriority.Background);
    }

    private void OnCharactersRescanned(object? sender, EventArgs e)
    {
        if (Vm == null)
            return;

        ScheduleRosterRebuild();
        Dispatcher.BeginInvoke(() =>
        {
            RedrawMarkers();
            ApplyRightPanelLayout();
            SyncPanelToolbarToggles();
        }, DispatcherPriority.Background);
    }

    private void OnRosterRefreshRequested(object? sender, EventArgs e) => ScheduleRosterRebuild();

    private void CharacterRosterScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scroller || e.Handled)
            return;

        scroller.ScrollToVerticalOffset(scroller.VerticalOffset - e.Delta);
        e.Handled = true;
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

        if (CartoRuntimeOptions.ShowMapOverlays || Vm.IsPlacingZone)
        {
        foreach (var zone in Vm.ZoneRects
                     .OrderBy(z => ReferenceEquals(z, Vm.SelectedZoneRect) ? 1 : 0))
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
        }

        RedrawDungeonMarkers();
    }

    private const string DungeonMarkerTag = "dungeon-marker";
    private const double DungeonMarkerSize = 6;
    private const double DungeonMarkerSizeSelected = 8;
    private const double DungeonMarkerHitPx = 10;

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

        if (!CartoRuntimeOptions.ShowMapOverlays && !Vm.IsPlacingDungeonMarker)
            return;

        var w = MapWidth;
        var h = MapHeight;
        if (w <= 0 || h <= 0)
            return;

        foreach (var marker in Vm.DungeonMarkers)
        {
            if (marker.MapX <= 0 && marker.MapY <= 0)
                continue;

            var selected = ReferenceEquals(marker, Vm.SelectedDungeonMarker);
            var size = selected ? DungeonMarkerSizeSelected : DungeonMarkerSize;
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(selected ? Color.FromRgb(0xBB, 0x99, 0xFF) : Color.FromRgb(0x88, 0x66, 0xCC)),
                Stroke = selected ? Brushes.White : new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                StrokeThickness = selected ? 1.5 : 1,
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
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1),
                    Child = new TextBlock
                    {
                        Text = marker.DisplayName,
                        FontSize = 8,
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

        const double hitPx = DungeonMarkerHitPx;
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

    private IEnumerable<CartoZoneRectItem> EnumerateZoneRectsForHitTest()
    {
        if (Vm == null)
            yield break;

        if (Vm.SelectedZoneRect != null)
            yield return Vm.SelectedZoneRect;

        for (var i = Vm.ZoneRects.Count - 1; i >= 0; i--)
        {
            var z = Vm.ZoneRects[i];
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

        foreach (var z in EnumerateZoneRectsForHitTest())
        {
            if (!IsZoneResizeHit(z, nx, ny, handleN))
                continue;

            zone = z;
            isResizeHandle = true;
            return true;
        }

        foreach (var z in EnumerateZoneRectsForHitTest())
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

    private void CaptureAllRosterExpandState()
    {
        CaptureRosterExpandState(CharacterRosterRoot);
        CaptureRosterExpandState(CooldownRosterRoot);
    }

    private void CaptureRosterExpandState(StackPanel? root)
    {
        if (root == null)
            return;

        foreach (var userExp in root.Children
                     .OfType<Border>()
                     .Select(b => b.Child)
                     .OfType<Expander>()
                     .Concat(CharacterRosterRoot.Children.OfType<Expander>()))
        {
            if (userExp.Tag is not string userId)
                continue;

            CaptureExpanderKey(userExp, RosterExpandKeys.User(userId));

            if (userExp.Content is not StackPanel userPanel)
                continue;

            foreach (var child in userPanel.Children)
            {
                if (child is Expander accountExp && accountExp.Tag is string accountId)
                {
                    CaptureExpanderKey(accountExp, RosterExpandKeys.Account(userId, accountId));
                    if (accountExp.Content is StackPanel accountPanel)
                        CaptureCategoryExpanders(userId, accountId, accountPanel);
                }
                else if (child is Border { Tag: CharacterStatus category } shell
                         && shell.Child is Expander catExp)
                {
                    CaptureExpanderKey(catExp, RosterExpandKeys.Category(userId, "", category));
                }
            }
        }
    }

    private void CaptureCategoryExpanders(string userId, string accountId, StackPanel panel)
    {
        foreach (var child in panel.Children)
        {
            if (child is not Border { Tag: CharacterStatus category } shell
                || shell.Child is not Expander catExp)
                continue;

            CaptureExpanderKey(catExp, RosterExpandKeys.Category(userId, accountId, category));
        }
    }

    private void CaptureExpanderKey(Expander expander, string key)
    {
        if (expander.IsExpanded)
        {
            _rosterExpandedKeys.Add(key);
            _rosterCollapsedKeys.Remove(key);
        }
        else
        {
            _rosterCollapsedKeys.Add(key);
            _rosterExpandedKeys.Remove(key);
        }
    }

    private void RenderCharacterRosterFromTree(int buildId)
    {
        if (buildId != _rosterPanelBuildGeneration || CharacterRosterRoot == null || Vm == null)
            return;

        Vm.RefreshRosterTree(
            (key, defaultExpanded) => IsRosterExpanded(key, defaultExpanded),
            requestViewRefresh: false);

        _suppressRosterExpandEvents = true;
        try
        {
            CartoRosterTreeRenderer.Render(
                CharacterRosterRoot,
                Vm.RosterTreeRoots,
                new CartoRosterTreeRenderer.Host
                {
                    ViewModel = Vm,
                    BuildUserExpander = BuildUserExpander,
                    BuildAccountExpander = BuildAccountExpander,
                    BuildCategoryExpander = BuildCategoryExpanderForUser,
                    StretchPanel = panel => CartoRosterPanelUi.StretchWidth(panel)
                },
                buildId,
                id => id == _rosterPanelBuildGeneration);
        }
        finally
        {
            _suppressRosterExpandEvents = false;
        }

        if (buildId == _rosterPanelBuildGeneration)
            SyncRosterListWidth();
    }

    private bool IsRosterExpanded(string expandKey, bool defaultExpanded)
    {
        if (_rosterCollapsedKeys.Contains(expandKey))
            return false;
        if (_rosterExpandedKeys.Contains(expandKey))
            return true;
        return defaultExpanded;
    }

    private void CharacterRosterList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CharacterRosterList?.SelectedItem is WowCharacter ch)
            OpenCharacterDetail(ch);
    }

    private void RebuildCharacterRoster() => RebuildAllRosterPanels();

    private void RebuildCooldownRoster() => RebuildAllRosterPanels();

    private void RebuildAllRosterPanels()
    {
        if (Vm == null || !Vm.CharactersLoaded)
            return;

        if (CartoRuntimeOptions.UseSimpleCharacterList)
        {
            CharacterRosterList?.Items.Refresh();
            return;
        }

        CaptureAllRosterExpandState();

        var buildId = Interlocked.Increment(ref _rosterPanelBuildGeneration);
        RenderCharacterRosterFromTree(buildId);
        if (buildId != _rosterPanelBuildGeneration)
            return;

        RebuildRosterPanel(
            CooldownRosterRoot,
            c =>
            {
                Vm.ApplySyncEnrichment(c);
                var sync = Vm.FindWowSyncCharacter(c);
                return CartoCharacterPresentation.HasTrackedProfession(c, sync)
                       && CartoCooldownDisplay.HasDisplayableCooldowns(c, sync);
            },
            cooldownCategoriesOnly: true,
            buildId);
    }

    private void RebuildRosterPanel(
        StackPanel? root,
        Func<WowCharacter, bool>? characterFilter,
        bool cooldownCategoriesOnly,
        int buildId)
    {
        if (buildId != _rosterPanelBuildGeneration)
            return;
        if (CartoRuntimeOptions.UseSimpleCharacterList)
        {
            return;
        }

        if (root == null || Vm == null) return;
        root.Children.Clear();
        _suppressRosterExpandEvents = true;
        try
        {
            var allLocalChars = Vm.Characters
                .Where(c => characterFilter?.Invoke(c) ?? true)
                .ToList();

            if (allLocalChars.Count == 0)
            {
                root.Children.Add(new TextBlock
                {
                    Text = cooldownCategoriesOnly
                        ? "Aucun personnage avec recette CD connue.\nDéployez l'addon WowSync v1.4+, connectez-vous en jeu (/reload)."
                        : "Aucun personnage.\nParamètres → WowSync + comptes WoW, puis Actualiser.",
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
                if (buildId != _rosterPanelBuildGeneration)
                    return;

                var userCharsAll = allLocalChars
                    .Where(c => Vm.GetUserIdForCharacter(c) == user.Id)
                    .ToList();

                if (cooldownCategoriesOnly && userCharsAll.Count == 0)
                    continue;

                foreach (var c in userCharsAll)
                    assignedCharIds.Add(c.Id);

                var userPanel = CartoRosterPanelUi.StretchWidth(new StackPanel());
                if (cooldownCategoriesOnly)
                    PopulateCooldownCharacters(userPanel, userCharsAll, buildId);
                else
                    PopulateUserAccountsAndCategories(userPanel, user, userCharsAll, buildId);
                root.Children.Add(BuildUserExpander(user, userCharsAll, userPanel, cooldownCategoriesOnly));
            }

            if (buildId != _rosterPanelBuildGeneration)
                return;

            var orphans = allLocalChars.Where(c => !assignedCharIds.Contains(c.Id)).ToList();
            if (orphans.Count > 0)
            {
                var fallbackUser = users.FirstOrDefault(CartoViewModel.IsDefaultCartoUser)
                                   ?? users.FirstOrDefault();
                if (fallbackUser != null)
                {
                    var userPanel = CartoRosterPanelUi.StretchWidth(new StackPanel());
                    if (cooldownCategoriesOnly)
                        PopulateCooldownCharacters(userPanel, orphans, buildId);
                    else
                        PopulateUserAccountsAndCategories(userPanel, fallbackUser, orphans, buildId);
                    root.Children.Add(BuildUserExpander(fallbackUser, orphans, userPanel, cooldownCategoriesOnly));
                }
                else
                {
                    var flatPanel = CartoRosterPanelUi.StretchWidth(new StackPanel());
                    if (cooldownCategoriesOnly)
                        PopulateCooldownCharacters(flatPanel, orphans, buildId);
                    else
                    {
                        foreach (var ch in orphans.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            if (buildId != _rosterPanelBuildGeneration)
                                return;
                            flatPanel.Children.Add(BuildStatusDockChip(ch, cooldownRoster: false));
                        }
                    }

                    root.Children.Add(flatPanel);
                }
            }
        }
        finally
        {
            _suppressRosterExpandEvents = false;
            if (buildId == _rosterPanelBuildGeneration && root == CooldownRosterRoot)
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

    private void PopulateCooldownCharacters(StackPanel panel, IReadOnlyList<WowCharacter> chars, int buildId)
    {
        foreach (var ch in chars.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (buildId != _rosterPanelBuildGeneration)
                return;
            panel.Children.Add(BuildStatusDockChip(ch, cooldownRoster: true));
        }
    }

    private void PopulateUserAccountsAndCategories(
        StackPanel panel,
        CartoUser user,
        IReadOnlyList<WowCharacter> userChars,
        int buildId)
    {
        var accountIds = userChars
            .Select(c => c.AccountId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => Vm.Accounts.FirstOrDefault(a => a.Id == id)?.Name ?? id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var accountId in accountIds)
        {
            if (buildId != _rosterPanelBuildGeneration)
                return;

            var account = Vm.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (account == null)
                continue;

            var accountChars = userChars
                .Where(c => string.Equals(c.AccountId, accountId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var accountPanel = CartoRosterPanelUi.StretchWidth(new StackPanel());
            PopulateAccountCategories(accountPanel, user, account, accountChars, buildId);
            panel.Children.Add(BuildAccountExpander(user, account, accountPanel, buildId));
        }
    }

    private void PopulateAccountCategories(
        StackPanel panel,
        CartoUser user,
        WowAccount account,
        IReadOnlyList<WowCharacter> accountChars,
        int buildId)
    {
        foreach (var status in CartoViewModel.RosterCategoryStatuses)
        {
            if (buildId != _rosterPanelBuildGeneration)
                return;

            var statuses = CartoViewModel.StatusesForRosterCategory(status).ToHashSet();
            var inFrame = accountChars
                .Where(c => statuses.Contains(c.Status))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var title = CartoViewModel.RosterCategoryTitle(status);
            var totalInCategory = Vm.CountLocalCharactersInCategory(user.Id, status);
            panel.Children.Add(BuildCategoryExpanderForUser(
                user, title, status, inFrame, totalInCategory, account.Id, buildId));
        }
    }

    private Expander BuildAccountExpander(
        CartoUser user,
        WowAccount account,
        StackPanel content,
        int buildId)
    {
        var accountBrush = new SolidColorBrush(Color.FromRgb(190, 175, 130));
        var gold = Vm.GetAccountGoldCopper(account.SourceFolder);
        var accountMapToggle = CartoRosterIcons.CreateMapSubtreeVisibilityToggle(
            () => Vm.IsAccountVisibleOnMap(account),
            account.Name,
            () =>
            {
                Vm.ToggleAccountMapVisibility(account);
                RedrawMarkers();
                CartoRosterIcons.RefreshMapVisibilityToggles(CharacterRosterRoot);
            });
        var header = CartoRosterPanelUi.BuildUserTitleRow(account.Name, accountBrush, null, gold, accountMapToggle);

        var expandKey = RosterExpandKeys.Account(user.Id, account.Id);
        var expander = CartoRosterPanelUi.StretchExpander(new Expander
        {
            Tag = account.Id,
            Header = header,
            Content = CartoRosterPanelUi.StretchWidth(content),
            IsExpanded = IsRosterExpanded(expandKey, defaultExpanded: true),
            Margin = new Thickness(6, 0, 0, 4)
        });
        if (Application.Current?.TryFindResource("CartoCategoryExpander") is Style style)
            expander.Style = style;

        expander.Expanded += (_, _) => TrackRosterExpanded(expandKey);
        expander.Collapsed += (_, _) => TrackRosterCollapsed(expandKey);
        return expander;
    }

    private UIElement BuildUserExpander(
        CartoUser user,
        IReadOnlyList<WowCharacter> userCharacters,
        UIElement content,
        bool cooldownPanel)
    {
        var userBrush = CartoCharacterPresentation.GetUserHeaderBrush(user, Vm);
        UIElement? rightRail = null;
        if (cooldownPanel)
        {
            var (inProgress, ready) = CartoCooldownDisplay.CountCooldownStatuses(
                userCharacters,
                ch =>
                {
                    Vm.ApplySyncEnrichment(ch);
                    return Vm.FindWowSyncCharacter(ch);
                });
            if (inProgress > 0 || ready > 0)
                rightRail = CartoRosterPanelUi.BuildCooldownSummaryRail(inProgress, ready);
        }

        UIElement? userMapToggle = cooldownPanel
            ? null
            : CartoRosterIcons.CreateMapSubtreeVisibilityToggle(
                () => Vm.IsUserVisibleOnMap(user),
                user.Name,
                () =>
                {
                    Vm.ToggleUserMapVisibility(user);
                    RedrawMarkers();
                    CartoRosterIcons.RefreshMapVisibilityToggles(CharacterRosterRoot);
                });

        var header = CartoRosterPanelUi.BuildUserTitleRow(
            user.Name,
            userBrush,
            rightRail,
            cooldownPanel ? 0 : Vm.GetUserTotalGoldCopper(user.Id),
            userMapToggle);

        var userKey = RosterExpandKeys.User(user.Id);
        var expander = CartoRosterPanelUi.StretchExpander(new Expander
        {
            IsExpanded = IsRosterExpanded(userKey, defaultExpanded: true),
            Header = header,
            Content = content,
            Tag = user.Id
        });
        if (Application.Current?.TryFindResource("CartoUserExpander") is Style userStyle)
            expander.Style = userStyle;

        expander.Expanded += (_, _) => TrackRosterExpanded(userKey);
        expander.Collapsed += (_, _) => TrackRosterCollapsed(userKey);
        return CartoRosterPanelUi.WrapUserOwnerFrame(expander);
    }

    private Border BuildCategoryExpanderForUser(
        CartoUser user,
        string title,
        CharacterStatus category,
        IReadOnlyList<WowCharacter> characters,
        int totalInCategory,
        string? accountId = null,
        int buildId = 0)
    {
        var shell = CartoRosterPanelUi.WrapCategoryFrame(
            category,
            BuildCategoryExpanderContent(user, title, category, characters, totalInCategory, accountId, buildId));
        shell.Tag = category;
        shell.AllowDrop = true;
        shell.DragOver += (_, e) =>
        {
            if (shell.Tag is CharacterStatus status)
                StatusFrame_DragOver(status, e);
        };
        shell.Drop += (_, e) =>
        {
            if (shell.Tag is CharacterStatus status)
                StatusFrame_Drop(status, e);
        };
        shell.DragLeave += (_, _) => SetHighlightedDropFrame(null);
        return shell;
    }

    private Expander BuildCategoryExpanderContent(
        CartoUser user,
        string title,
        CharacterStatus category,
        IReadOnlyList<WowCharacter> characters,
        int totalInCategory,
        string? accountId = null,
        int buildId = 0)
    {
        var catMapToggle = CartoRosterIcons.CreateMapSubtreeVisibilityToggle(
            () => Vm.IsCategoryVisibleOnMap(user, category),
            title,
            () =>
            {
                Vm.ToggleCategoryMapVisibility(user, category);
                RedrawMarkers();
                CartoRosterIcons.RefreshMapVisibilityToggles(CharacterRosterRoot);
            });

        var headerPanel = CartoRosterPanelUi.StretchWidth(new StackPanel
        {
            Children =
            {
                CartoRosterPanelUi.BuildCategoryTitleRow(
                    category,
                    title,
                    Vm.GetCategoryGoldCopper(Vm.GetLocalCharactersForUserCategory(user.Id, category)),
                    catMapToggle)
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
                if (buildId != 0 && buildId != _rosterPanelBuildGeneration)
                    break;
                content.Children.Add(BuildStatusDockChip(ch));
            }
        }

        var defaultCatExpanded = totalInCategory > 0 && category == CharacterStatus.Main;
        var catKey = RosterExpandKeys.Category(user.Id, accountId ?? "", category);
        var expander = CartoRosterPanelUi.StretchExpander(new Expander
        {
            Tag = category,
            Header = headerPanel,
            Content = content,
            IsExpanded = IsRosterExpanded(catKey, defaultCatExpanded)
        });
        expander.Expanded += (_, _) => TrackRosterExpanded(catKey);
        expander.Collapsed += (_, _) => TrackRosterCollapsed(catKey);

        if (Application.Current?.TryFindResource("CartoCategoryExpander") is Style style)
            expander.Style = style;

        return expander;
    }

    private Border BuildStatusDockChip(WowCharacter ch, bool cooldownRoster = false)
    {
        void OpenDetail(WowCharacter c)
        {
            Vm.SelectedCharacter = c;
            RedrawMarkers();
            OpenCharacterDetail(c);
        }

        var callbacks = new CartoDockCardCallbacks
        {
            ToggleMapVisibility = cooldownRoster
                ? null
                : c =>
                {
                    Vm.ToggleCharacterMapVisibilityCommand.Execute(c);
                    RedrawMarkers();
                    CartoRosterIcons.RefreshMapVisibilityToggles(CharacterRosterRoot);
                },
            OpenDetails = OpenDetail,
            DragStart = cooldownRoster ? null : (c, card, e) =>
            {
                _chipDragCharacter = c;
                _chipDragStart = e.GetPosition(card);
                _chipDragStarted = false;
                card.CaptureMouse();
                e.Handled = true;
            },
            DragMove = cooldownRoster ? null : (c, card, e) =>
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
            DragEnd = cooldownRoster ? null : (c, card, e) =>
            {
                if (_chipDragCharacter != c) return;
                _chipDragCharacter = null;
                card.ReleaseMouseCapture();
                if (!_chipDragStarted && e.ChangedButton == MouseButton.Left)
                    OpenDetail(c);
                _chipDragStarted = false;
            }
        };

        UIElement? mapEye = cooldownRoster || callbacks.ToggleMapVisibility == null
            ? null
            : CartoRosterIcons.CreateMapVisibilityToggle(ch, callbacks.ToggleMapVisibility);

        var card = CartoRosterPanelUi.StretchWidth(CartoCharacterDockCard.Build(
            ch,
            Vm,
            callbacks,
            cooldownRoster ? new CartoDockCardOptions { CooldownRosterOnly = true } : null,
            mapEye));
        card.Tag = ch;

        if (cooldownRoster)
        {
            card.MouseLeftButtonDown += (_, e) =>
            {
                OpenDetail(ch);
                e.Handled = true;
            };
        }
        else
        {
            card.MouseLeftButtonDown += (_, e) => callbacks.DragStart?.Invoke(ch, card, e);
            card.MouseMove += (_, e) => callbacks.DragMove?.Invoke(ch, card, e);
            card.MouseLeftButtonUp += (_, e) => callbacks.DragEnd?.Invoke(ch, card, e);
        }

        return card;
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

    private static int MapLabelPriority(WowCharacter ch, bool isSelected, bool isTpBoy) =>
        isSelected ? 1000 : isTpBoy ? 200 : 0;

    private static void AddMapLabelLeaderLine(
        Canvas canvas,
        WowCharacter ch,
        double anchorX,
        double anchorY,
        double dotRadius,
        double labelLeft,
        double labelTop,
        double labelWidth,
        Brush stroke)
    {
        var (x1, y1, x2, y2) = CartoMapLabelLayout.GetLeaderSegment(
            anchorX, anchorY, dotRadius, labelLeft, labelTop, labelWidth, MapLabelHeight);

        var line = new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = stroke,
            StrokeThickness = 0.9,
            Opacity = 0.65,
            IsHitTestVisible = false,
            Tag = ch,
            SnapsToDevicePixels = true
        };
        Panel.SetZIndex(line, 12);
        canvas.Children.Add(line);
    }

    private void RedrawMarkers()
    {
        if (Vm == null) return;

        for (int i = MapCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (MapCanvas.Children[i] is Ellipse { Tag: WowCharacter }
                || MapCanvas.Children[i] is Border { Tag: WowCharacter }
                || MapCanvas.Children[i] is Line { Tag: WowCharacter }
                || MapCanvas.Children[i] is Border { Tag: "shard-label" }
                || MapCanvas.Children[i] is Border { Tag: "quest-icons" })
                MapCanvas.Children.RemoveAt(i);
        }

        var mapW = MapWidth;
        var mapH = MapHeight;

        var toDraw = Vm.FilteredCharacters
            .Where(c => !CartoRuntimeOptions.ShowCapitalMaps || !TryGetCharacterCapitalMapId(c, Vm, out _))
            .Where(c => Vm.TryGetMarkerPosition(c, out _, out _))
            .OrderBy(c => c.Status == CharacterStatus.TpBoy ? 1 : 0)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var labelRequests = new List<CartoMapLabelLayout.LabelRequest>();
        var drawItems = new List<(WowCharacter Ch, double PixX, double PixY, double Size, double LabelW, bool IsTpBoy, bool IsSelected, SolidColorBrush Brush)>();

        foreach (var ch in toDraw)
        {
            if (!Vm.TryGetMarkerPosition(ch, out var mapX, out var mapY))
                continue;

            var isTpBoy = ch.Status == CharacterStatus.TpBoy;
            var isSelected = ch == Vm.SelectedCharacter;
            var brush = GetClassBrush(WowClassColors.GetHexColor(ch.Class));
            var size = GetMapMarkerDotSize(isSelected, isTpBoy);
            var pixX = mapX * mapW;
            var pixY = mapY * mapH;
            var inlineShard = isTpBoy && ch.Class == WowClass.Demoniste && ch.ShardCount > 0;
            var labelW = EstimateLabelWidth(GetMapLabelText(ch, Vm), inlineShard);

            labelRequests.Add(new CartoMapLabelLayout.LabelRequest
            {
                Key = ch.Id,
                AnchorX = pixX,
                AnchorY = pixY,
                Width = labelW,
                Height = MapLabelHeight,
                DotRadius = size / 2,
                Priority = MapLabelPriority(ch, isSelected, isTpBoy)
            });
            drawItems.Add((ch, pixX, pixY, size, labelW, isTpBoy, isSelected, brush));
        }

        var labelPositions = CartoMapLabelLayout.Resolve(labelRequests, mapW, mapH)
            .ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var item in drawItems)
        {
            var ch = item.Ch;
            Brush strokeBrush = item.IsSelected ? Brushes.White
                : item.IsTpBoy ? TpBoyStrokeBrush
                : DefaultStrokeBrush;

            var marker = new Ellipse
            {
                Width = item.Size,
                Height = item.Size,
                Fill = item.Brush,
                Stroke = strokeBrush,
                StrokeThickness = item.IsTpBoy ? 1.5 : (item.IsSelected ? 1.5 : 1),
                Cursor = Cursors.Hand,
                Tag = ch,
                ToolTip = ch.Name
            };
            Panel.SetZIndex(marker, item.IsTpBoy ? 14 : 10);
            Canvas.SetLeft(marker, item.PixX - item.Size / 2);
            Canvas.SetTop(marker, item.PixY - item.Size / 2);
            MapCanvas.Children.Add(marker);

            double labelLeft, labelTop;
            if (labelPositions.TryGetValue(ch.Id, out var pos))
            {
                labelLeft = pos.Left;
                labelTop = pos.Top;
            }
            else
            {
                labelTop = item.PixY - item.Size / 2 - MapLabelHeight - CartoMapLabelLayout.GapAboveDot;
                labelLeft = Math.Clamp(item.PixX - item.LabelW / 2, 0, Math.Max(0, mapW - item.LabelW));
                labelTop = Math.Clamp(labelTop, 0, Math.Max(0, mapH - MapLabelHeight));
            }

            AddMapLabelLeaderLine(
                MapCanvas, ch, item.PixX, item.PixY, item.Size / 2,
                labelLeft, labelTop, item.LabelW, item.Brush);

            var label = BuildMapCharacterLabel(ch, Vm, item.Brush, item.IsSelected, item.IsTpBoy, out _);
            Canvas.SetLeft(label, labelLeft);
            Canvas.SetTop(label, labelTop);
            MapCanvas.Children.Add(label);
            Panel.SetZIndex(label, item.IsTpBoy ? 18 : 15);
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
                b.Click += (_, _) => { action(); RefreshMapTimersOnly(); };
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

        if (Vm?.IsCharacterDetailOpen == true && CharacterDetailHost != null)
            CartoCooldownDisplay.UpdateAll(CharacterDetailHost);
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
                    tb.Foreground = TimerExpiredBrush;
                    tb.FontSize = 10;
                    tb.FontWeight = FontWeights.Normal;
                }
                else if (timer.IsRunning)
                {
                    tb.Text = FormatTimeSpan((TimeSpan?)timer.Remaining);
                    tb.Foreground = TimerRunningBrush;
                    tb.FontSize = 12;
                    tb.FontWeight = FontWeights.Bold;
                }
                else
                {
                    tb.Text = "⏸ Pause";
                    tb.Foreground = TimerPausedBrush;
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

    private void MapBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm == null || e.ChangedButton != MouseButton.Left)
            return;

        if (IsScrollBarChrome(e.OriginalSource))
            return;

        if (Vm.IsPlacingZone)
            return;

        if (!Vm.IsZonesPanelOpen && TryProcessCapitalMapPointer(e))
        {
            e.Handled = true;
            return;
        }

        if (!TryShouldPanMapOnPointerDown(e))
            return;

        BeginMapPan(e);
    }

    private void MapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm == null || e.Handled)
            return;

        if (Vm.IsPlacingZone && Vm.IsZoneEditMode)
        {
            var mapPos = e.GetPosition(MapImage);
            if (Vm.TryAddZoneAt(mapPos.X / MapWidth, mapPos.Y / MapHeight))
                RedrawZoneEditor();

            e.Handled = true;
            return;
        }

        if (Vm.IsPlacingDungeonMarker)
        {
            var pos = e.GetPosition(MapImage);
            if (Vm.TryPlaceDungeonMarkerAt(pos.X / MapWidth, pos.Y / MapHeight))
                RedrawZoneEditor();

            e.Handled = true;
            return;
        }

        if (Vm.IsZoneEditMode && CartoRuntimeOptions.ShowMapOverlays)
        {
            var mapPos = e.GetPosition(MapImage);

            if (TryHitDungeonMarker(mapPos, out var dungeonHit) && dungeonHit != null)
            {
                CancelMapPan();
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
                CancelMapPan();
                Vm.SelectedZoneRect = hit;
                Vm.SelectedDungeonMarker = null;
                _zoneDragItem = hit;
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
        }

        if (GetCharacterFromEventSource(e.OriginalSource) is { } ch)
        {
            CancelMapPan();
            if (_tooltipCharacter == ch && Vm.IsCharacterDetailOpen)
                NavigateBackFromCharacterDetail();
            else
            {
                Vm.SelectedCharacter = ch;
                RedrawMarkers();
                OpenCharacterDetail(ch, fromMap: true);
            }

            e.Handled = true;
            return;
        }

        if (e.OriginalSource is Ellipse { Tag: MapTimer timer })
        {
            CancelMapPan();
            _draggingTimer = timer;
            _isDragging = false;
            _panStart = e.GetPosition(MapBorder);
            MapBorder.CaptureMouse();
            e.Handled = true;
        }
    }

    private bool TryShouldPanMapOnPointerDown(MouseButtonEventArgs e)
    {
        if (GetCharacterFromEventSource(e.OriginalSource) != null)
            return false;

        if (e.OriginalSource is Ellipse { Tag: MapTimer })
            return false;

        return true;
    }

    private void CancelMapPan()
    {
        _isPanning = false;
        if (MapBorder.IsMouseCaptured)
            MapBorder.ReleaseMouseCapture();
        UpdateMapCursor();
    }

    private void BeginMapPan(MouseButtonEventArgs e)
    {
        if (Vm == null || MapScroll == null)
            return;

        if (Vm.IsCharacterDetailOpen)
            NavigateBackFromCharacterDetail();
        else if (CharPopup.IsOpen)
            CloseCharacterTooltip();

        _isPanning = true;
        _isDragging = false;
        _panStart = e.GetPosition(MapBorder);
        _panStartScrollH = MapScroll.HorizontalOffset;
        _panStartScrollV = MapScroll.VerticalOffset;
        MapBorder.CaptureMouse();
        UpdateMapCursor();
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
                _zoneDragItem.Top = Math.Clamp(_zoneDragStartTop + dy, 0, 1 - _zoneDragItem.Height);
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

        if (_isPanning && MapScroll != null)
        {
            var dx = pos.X - _panStart.X;
            var dy = pos.Y - _panStart.Y;
            MapScroll.ScrollToHorizontalOffset(ClampScrollOffset(_panStartScrollH + dx, MapScroll.ScrollableWidth));
            MapScroll.ScrollToVerticalOffset(ClampScrollOffset(_panStartScrollV + dy, MapScroll.ScrollableHeight));
            e.Handled = true;
        }
    }

    private static double ClampScrollOffset(double value, double max)
    {
        if (max <= 0)
            return 0;
        return Math.Clamp(value, 0, max);
    }

    private static bool IsScrollBarChrome(object? source)
    {
        if (source is not DependencyObject node)
            return false;

        while (node != null)
        {
            if (node is ScrollBar)
                return true;
            node = VisualTreeHelper.GetParent(node);
        }

        return false;
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
            _zoneResizeDrag = false;
            MapBorder.ReleaseMouseCapture();
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
            RefreshMapTimersOnly();
            e.Handled = true;
            return;
        }

        var wasPanning = _isPanning;
        _isPanning = false;
        _isDragging = false;
        MapBorder.ReleaseMouseCapture();
        if (wasPanning)
            UpdateMapCursor();
    }

    private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Vm == null || MapBorder == null || MapScroll == null)
            return;

        var pos = e.GetPosition(MapBorder);
        ApplyMapZoomAtViewportPoint(pos.X, pos.Y, CartoViewModel.WheelZoomFactorFromDelta(e.Delta));
        e.Handled = true;
    }

    private void MapZoomIn_Click(object sender, RoutedEventArgs e)
        => ApplyMapZoomAtViewportCenter(CartoViewModel.WheelZoomFactorFromDelta(120));

    private void MapZoomOut_Click(object sender, RoutedEventArgs e)
        => ApplyMapZoomAtViewportCenter(CartoViewModel.WheelZoomFactorFromDelta(-120));

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

        ApplyMapZoomAtViewportPoint(MapBorder.ActualWidth / 2, MapBorder.ActualHeight / 2, factor);
    }

    private void SyncMapViewportConstraints() => ApplyMapContentLayout();

    private void ApplyMapZoomAtViewportPoint(double viewportX, double viewportY, double factor)
    {
        if (Vm == null || MapScroll == null)
            return;

        var oldZoom = Vm.MapZoom;
        var newZoom = Math.Clamp(oldZoom * factor, CartoViewModel.MinMapZoom, CartoViewModel.MaxMapZoom);
        if (Math.Abs(newZoom - oldZoom) < 1e-9)
            return;

        var contentX = (viewportX + MapScroll.HorizontalOffset) / oldZoom;
        var contentY = (viewportY + MapScroll.VerticalOffset) / oldZoom;
        Vm.MapZoom = newZoom;

        Dispatcher.BeginInvoke(() =>
        {
            if (MapScroll == null)
                return;

            MapScroll.ScrollToHorizontalOffset(Math.Max(0, contentX * newZoom - viewportX));
            MapScroll.ScrollToVerticalOffset(Math.Max(0, contentY * newZoom - viewportY));
        }, DispatcherPriority.Loaded);
    }

    private static readonly System.Media.SoundPlayer _chimePlayer = new(@"C:\Windows\Media\chimes.wav");

    private void OnTimerExpired(MapTimer t)
    {
        try { _chimePlayer.Play(); } catch { }
        RefreshMapTimersOnly();
    }

    private void RefreshMapTimersOnly()
    {
        RedrawTimerMarkers();
        UpdateTimerCountdowns();
    }

    private void TimerRestart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MapTimer t })
        { Vm.RestartTimerCommand.Execute(t); RefreshMapTimersOnly(); }
    }

    private void TimerResume_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MapTimer t })
        { Vm.ResumeTimerCommand.Execute(t); RefreshTimerListButtons(); RefreshMapTimersOnly(); }
    }

    private void TimerStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MapTimer t })
        { Vm.StopTimerCommand.Execute(t); RefreshTimerListButtons(); RefreshMapTimersOnly(); }
    }

    private void TimerRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MapTimer t })
        { Vm.RemoveTimerCommand.Execute(t); RefreshMapTimersOnly(); }
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
            RefreshMapTimersOnly();
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
        RefreshMapTimersOnly();
    }

    private void ActionChar_Click(object sender, RoutedEventArgs e) => PopupAddChar.IsOpen = true;
    private void PanelRoster_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        ActivatePanel(CartoPanel.Roster);
    }

    private void PanelRoster_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Roster, false);
    }

    private void PanelCooldowns_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        ActivatePanel(CartoPanel.Cooldowns);
    }

    private void PanelCooldowns_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Cooldowns, false);
    }

    private void PanelSearch_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        ActivatePanel(CartoPanel.Search);
    }

    private void PanelSearch_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Search, false);
    }

    private void PanelTimers_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        ActivatePanel(CartoPanel.Timers);
    }

    private void PanelTimers_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Timers, false);
    }

    private void PanelSettings_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        ActivatePanel(CartoPanel.Settings);
    }

    private void PanelSettings_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Settings, false);
    }

    private void PanelZones_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        ActivatePanel(CartoPanel.Zones);
    }

    private void PanelZones_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressPanelToggleEvents) return;
        SetPanelOpen(CartoPanel.Zones, false);
    }

    private enum CartoPanel { Cooldowns, Roster, Search, Timers, Zones, Settings, Character }

    private CartoPanel? GetActiveListPanel()
    {
        if (Vm == null) return null;
        if (Vm.IsCharacterDetailOpen) return null;
        if (Vm.IsCooldownRosterOpen) return CartoPanel.Cooldowns;
        if (Vm.IsRosterOpen) return CartoPanel.Roster;
        if (Vm.IsItemSearchOpen) return CartoPanel.Search;
        if (Vm.IsTimersPanelOpen) return CartoPanel.Timers;
        if (Vm.IsZonesPanelOpen) return CartoPanel.Zones;
        if (Vm.IsSettingsPanelOpen) return CartoPanel.Settings;
        return null;
    }

    private void ActivatePanel(CartoPanel panel)
    {
        if (Vm == null) return;

        Vm.IsCharacterDetailOpen = false;
        Vm.IsCooldownRosterOpen = panel == CartoPanel.Cooldowns;
        Vm.IsRosterOpen = panel == CartoPanel.Roster;
        Vm.IsItemSearchOpen = panel == CartoPanel.Search;
        Vm.IsTimersPanelOpen = panel == CartoPanel.Timers;
        Vm.IsZonesPanelOpen = panel == CartoPanel.Zones;
        Vm.IsSettingsPanelOpen = panel == CartoPanel.Settings;

        SyncPanelToolbarToggles();
        ApplyRightPanelLayout();

        if (panel == CartoPanel.Cooldowns)
            _ = PopulateRosterAsync(includeCooldowns: true);
        else if (panel == CartoPanel.Roster)
            _ = PopulateRosterAsync();
    }

    private void SetPanelOpen(CartoPanel panel, bool open)
    {
        if (Vm == null) return;
        if (open)
        {
            ActivatePanel(panel);
            return;
        }

        switch (panel)
        {
            case CartoPanel.Roster: Vm.IsRosterOpen = false; break;
            case CartoPanel.Cooldowns: Vm.IsCooldownRosterOpen = false; break;
            case CartoPanel.Search: Vm.IsItemSearchOpen = false; break;
            case CartoPanel.Timers: Vm.IsTimersPanelOpen = false; break;
            case CartoPanel.Zones: Vm.IsZonesPanelOpen = false; break;
            case CartoPanel.Settings: Vm.IsSettingsPanelOpen = false; break;
        }

        SyncPanelToolbarToggles();
        ApplyRightPanelLayout();
    }

    private async Task PopulateRosterAsync(bool includeCooldowns = false)
    {
        if (Vm == null)
            return;
        if (!Vm.IsRosterOpen && !(includeCooldowns && Vm.IsCooldownRosterOpen))
            return;

        ShowRosterLoadingState(true, includeCooldowns);
        try
        {
            await Vm.EnsureCharacterDataLoadedAsync().ConfigureAwait(true);
        }
        finally
        {
            if (Vm?.IsRosterOpen == true || (includeCooldowns && Vm?.IsCooldownRosterOpen == true))
                RebuildAllRosterPanels();
        }
    }

    private void ShowRosterLoadingState(bool loading, bool cooldownPanel = false)
    {
        var root = CooldownRosterRoot;
        if (root == null)
            return;

        if (!loading)
            return;

        root.Children.Clear();
        root.Children.Add(new TextBlock
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

    private void CloseCooldownRosterPanel_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        e.Handled = true;
        SetPanelOpen(CartoPanel.Cooldowns, false);
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

    private void ZoneListDelete_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void DungeonListDelete_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void ApplyRightPanelLayout()
    {
        if (Vm == null || ItemSearchPanel == null || CharacterRosterHost == null)
            return;

        var showCharacter = Vm.IsCharacterDetailOpen;
        var showCooldowns = !showCharacter && Vm.IsCooldownRosterOpen;
        var showRoster = !showCharacter && Vm.IsRosterOpen;
        var showSearch = !showCharacter && Vm.IsItemSearchOpen;
        var showTimers = !showCharacter && Vm.IsTimersPanelOpen;
        var showZones = !showCharacter && Vm.IsZonesPanelOpen;
        var showSettings = !showCharacter && Vm.IsSettingsPanelOpen;

        if (CharacterDetailHost != null)
            CharacterDetailHost.Visibility = showCharacter ? Visibility.Visible : Visibility.Collapsed;
        if (CooldownRosterHost != null)
            CooldownRosterHost.Visibility = showCooldowns ? Visibility.Visible : Visibility.Collapsed;
        CharacterRosterHost.Visibility = showRoster ? Visibility.Visible : Visibility.Collapsed;
        ItemSearchPanel.Visibility = showSearch ? Visibility.Visible : Visibility.Collapsed;
        if (TimersPanelHost != null)
            TimersPanelHost.Visibility = showTimers ? Visibility.Visible : Visibility.Collapsed;
        if (ZonesPanelHost != null)
            ZonesPanelHost.Visibility = showZones ? Visibility.Visible : Visibility.Collapsed;
        if (SettingsPanelHost != null)
            SettingsPanelHost.Visibility = showSettings ? Visibility.Visible : Visibility.Collapsed;

        if (showRoster)
        {
            var useSimpleList = CartoRuntimeOptions.UseSimpleCharacterList;
            if (CharacterRosterScroller != null)
                CharacterRosterScroller.Visibility = useSimpleList ? Visibility.Collapsed : Visibility.Visible;
            if (CharacterRosterList != null)
                CharacterRosterList.Visibility = useSimpleList ? Visibility.Visible : Visibility.Collapsed;

            if (Vm.CharactersLoaded && Vm.RosterTreeRoots.Count == 0 && !useSimpleList)
                Vm.RefreshRosterTree();
        }

        if (showCooldowns && CooldownRosterHost != null
            && Vm.CharactersLoaded && CooldownRosterRoot?.Children.Count == 0)
            RebuildCooldownRoster();

        var anyPanelOpen = showCharacter || showCooldowns || showRoster || showSearch || showTimers || showZones
                           || showSettings;

        if (RightDockHost != null)
            RightDockHost.Visibility = anyPanelOpen ? Visibility.Visible : Visibility.Collapsed;

        if (ZonesPanelHost != null)
            ZonesPanelHost.Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x10, 0x18, 0x10));

        if (showSearch)
            Vm.UpdateItemSearch();
    }

    private void SyncPanelToolbarToggles()
    {
        if (Vm == null) return;
        _suppressPanelToggleEvents = true;
        try
        {
            if (BtnPanelRoster != null) BtnPanelRoster.IsChecked = Vm.IsRosterOpen;
            if (BtnPanelCooldowns != null) BtnPanelCooldowns.IsChecked = Vm.IsCooldownRosterOpen;
            if (BtnPanelSearch != null) BtnPanelSearch.IsChecked = Vm.IsItemSearchOpen;
            if (BtnPanelTimers != null) BtnPanelTimers.IsChecked = Vm.IsTimersPanelOpen;
            if (BtnPanelZones != null) BtnPanelZones.IsChecked = Vm.IsZonesPanelOpen;
            if (BtnPanelSettings != null) BtnPanelSettings.IsChecked = Vm.IsSettingsPanelOpen;
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
        if (Vm == null || sender is not ComboBox combo || combo.DataContext is not AccountSettingRow row)
            return;

        row.RefreshOwnerDisplayName(Vm.GetOrderedUsers());
    }

    private async void RefreshAccountSettings_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        await Vm.RescanWowFromWtfAsync();
    }

    private void SaveAccountSettings_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        Vm.CloseSettingsPanelAfterSave();
        ApplyRightPanelLayout();
        RebuildAllRosterPanels();
        RedrawMarkers();
    }

    private void CloseCharacterTooltip()
    {
        CharPopup.IsOpen = false;
        CloseCharacterDetail();
    }

    private void OpenCharacterDetail(WowCharacter ch, bool fromMap = false)
    {
        if (Vm == null)
            return;

        if (!Vm.IsCharacterDetailOpen)
            _returnPanelAfterCharacter = GetActiveListPanel() ?? CartoPanel.Cooldowns;

        _tooltipCharacter = ch;
        Vm.SelectedCharacter = ch;
        RebuildCharacterDetailContent(ch);

        if (CharacterDetailTitle != null)
            CharacterDetailTitle.Text = string.IsNullOrWhiteSpace(ch.Name) ? "Personnage" : ch.Name;

        Vm.IsCharacterDetailOpen = true;
        Vm.IsCooldownRosterOpen = false;
        Vm.IsRosterOpen = false;
        Vm.IsItemSearchOpen = false;
        Vm.IsTimersPanelOpen = false;
        Vm.IsZonesPanelOpen = false;
        Vm.IsSettingsPanelOpen = false;

        SyncPanelToolbarToggles();
        ApplyRightPanelLayout();
        RedrawMarkers();

        if (fromMap && !ch.IsPlacedOnMap)
            Vm.EnsureCharactersVisibleOnMap();
    }

    private void NavigateBackFromCharacterDetail()
    {
        if (Vm == null)
            return;

        var target = _returnPanelAfterCharacter ?? CartoPanel.Cooldowns;
        _returnPanelAfterCharacter = null;
        _tooltipCharacter = null;
        Vm.SelectedCharacter = null;
        Vm.IsCharacterDetailOpen = false;
        ActivatePanel(target);
        RedrawMarkers();
    }

    private void CloseCharacterDetail()
    {
        if (Vm == null)
            return;

        Vm.IsCharacterDetailOpen = false;
        _returnPanelAfterCharacter = null;
        _tooltipCharacter = null;
        Vm.SelectedCharacter = null;
        SyncPanelToolbarToggles();
        ApplyRightPanelLayout();
        RedrawMarkers();
    }

    private void CharacterDetailBack_Click(object sender, RoutedEventArgs e) => NavigateBackFromCharacterDetail();

    private void CharacterDetailClose_Click(object sender, RoutedEventArgs e) => NavigateBackFromCharacterDetail();

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

        if (MapBorder.Visibility != Visibility.Visible || MapContainer == null)
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

    private void RebuildCharacterDetailContent(WowCharacter ch)
    {
        if (CharacterDetailContent == null || CharacterDetailActionsHost == null || CharacterDetailHeroHost == null)
            return;

        CharacterDetailContent.Children.Clear();
        CharacterDetailActionsHost.Children.Clear();
        CharacterDetailHeroHost.Child = null;

        var isWowSync = !string.IsNullOrEmpty(ch.SyncKey);
        if (isWowSync)
            Vm.ApplySyncEnrichment(ch);
        var syncData = isWowSync ? Vm.FindWowSyncCharacter(ch) : null;

        _notePopupCharacter = ch;
        CharacterDetailHeroHost.Child = BuildCharPopupHero(ch, syncData, isWowSync);
        BuildCharPopupActions(ch, isWowSync);

        var stack = CharacterDetailContent;
        var goldBrush = new SolidColorBrush(Color.FromRgb(218, 165, 32));

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
                RebuildCharacterDetailContent(ch);
            };
            ((StackPanel)shardSection.Child).Children.Add(shardBox);
            stack.Children.Add(shardSection);
        }

        if (ch.Class == WowClass.Demoniste)
            RedrawMarkers();
    }

    private void CharacterNoteBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_notePopupCharacter == null || sender is not TextBox box)
            return;

        _notePopupCharacter.Note = box.Text;
        Vm.Save();
        Vm.RefreshRosterTree();
    }

    private const double CharacterDetailSectionGap = 10;

    private UIElement BuildCharPopupHero(WowCharacter ch, WowCharacterData? syncData, bool isWowSync)
    {
        var classBrush = CartoCharacterPresentation.GetClassBrush(ch.Class);
        var nameBrush = CartoCharacterPresentation.GetCharacterNameBrush(ch, Vm);
        var accountName = Vm.GetCharacterAccountDisplayName(ch);

        var shell = new Border
        {
            Padding = new Thickness(12, 10, 12, 10),
            Background = new LinearGradientBrush(
                Color.FromRgb(48, 38, 24), Color.FromRgb(32, 26, 16), 90)
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            },
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, classBrush.Color.R, classBrush.Color.G, classBrush.Color.B)),
            BorderThickness = new Thickness(0, 0, 0, 2)
        };

        var root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

        UIElement? questContent = null;
        if (CartoCharacterPresentation.ShowQuestBody(ch))
        {
            var questRow = CartoCharacterPresentation.BuildQuestIconRow(ch, syncData, 24, horizontal: true);
            if (questRow.Children.Count > 0)
                questContent = questRow;
        }

        var identityGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        identityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        identityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        identityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var portrait = CartoCharacterPresentation.BuildPortraitIcons(
            ch,
            64,
            new Thickness(0, 0, 12, 0),
            showCooldownBars: false,
            sync: syncData);
        portrait.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        Grid.SetColumn(portrait, 0);
        identityGrid.Children.Add(portrait);

        var identityCol = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 8, 0)
        };

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
        identityCol.Children.Add(nameLine);

        if (!string.IsNullOrEmpty(accountName))
        {
            identityCol.Children.Add(new TextBlock
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
            identityCol.Children.Add(goldRow);

        Grid.SetColumn(identityCol, 1);
        identityGrid.Children.Add(identityCol);

        var actionsCol = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        if (questContent != null)
        {
            questContent.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 0));
            questContent.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
            actionsCol.Children.Add(questContent);
        }

        if (isWowSync)
        {
            actionsCol.Children.Add(CartoRosterIcons.CreateMapVisibilityToggle(ch, c =>
            {
                Vm.ToggleCharacterMapVisibilityCommand.Execute(c);
                RebuildCharacterDetailContent(c);
                RedrawMarkers();
            }));
        }

        if (actionsCol.Children.Count > 0)
        {
            Grid.SetColumn(actionsCol, 2);
            identityGrid.Children.Add(actionsCol);
        }

        root.Children.Add(identityGrid);

        var pvpPanel = CartoCharacterPresentation.BuildPvpRankDetailPanel(syncData);
        if (pvpPanel is FrameworkElement pvpFe)
        {
            pvpFe.Margin = new Thickness(0, CharacterDetailSectionGap, 0, 0);
            root.Children.Add(CartoRosterPanelUi.StretchWidth(pvpFe));
        }

        var locationSection = CartoCharacterPresentation.BuildCharacterLocationSection(
            Vm.GetCharacterZoneLabel(ch),
            Vm.GetCharacterPositionDisplay(ch));
        if (locationSection is FrameworkElement locationFe)
        {
            locationFe.Margin = new Thickness(0, CharacterDetailSectionGap, 0, 0);
            root.Children.Add(CartoRosterPanelUi.StretchWidth(locationFe));
        }

        var heroCdStrip = CartoCooldownDisplay.BuildRosterCardStrip(ch, syncData);
        if (heroCdStrip is FrameworkElement heroCdFe)
        {
            heroCdFe.Margin = new Thickness(0, CharacterDetailSectionGap, 0, 0);
            root.Children.Add(CartoRosterPanelUi.StretchWidth(heroCdFe));
        }

        var settingsBlock = BuildCharacterSettingsBlock(ch, isWowSync);
        settingsBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
        root.Children.Add(settingsBlock);

        shell.Child = root;
        return shell;
    }

    private Border BuildCharacterSettingsBlock(WowCharacter ch, bool isWowSync)
    {
        var settingsPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
        settingsPanel.Children.Add(new TextBlock
        {
            Text = "Réglages",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(220, 200, 150)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        if (isWowSync)
        {
            var syncBanner = CartoCharacterPresentation.BuildProminentSyncBanner(Vm.GetCharacterSyncLabel(ch));
            if (syncBanner != null)
                settingsPanel.Children.Add(syncBanner);
        }

        settingsPanel.Children.Add(new TextBlock
        {
            Text = "Catégorie dans le bandeau",
            FontSize = 10,
            Foreground = CartoCharacterPresentation.DimBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        settingsPanel.Children.Add(BuildCategoryCombo(ch));

        settingsPanel.Children.Add(new TextBlock
        {
            Text = "Note personnelle",
            FontSize = 10,
            Foreground = CartoCharacterPresentation.DimBrush,
            Margin = new Thickness(0, 10, 0, 4)
        });
        var noteBox = new TextBox
        {
            Text = ch.Note ?? "",
            FontSize = 11,
            MinHeight = 40,
            MaxHeight = 96,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = TryFindResource("SelectionBgBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(30, 26, 18)),
            Foreground = TryFindResource("TextBrush") as Brush ?? Brushes.White,
            BorderBrush = TryFindResource("BorderBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(90, 75, 45)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            CaretBrush = Brushes.White
        };
        noteBox.LostFocus -= CharacterNoteBox_LostFocus;
        noteBox.LostFocus += CharacterNoteBox_LostFocus;
        settingsPanel.Children.Add(noteBox);

        return new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, CharacterDetailSectionGap, 0, 0),
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(90, 20, 16, 8)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(90, 75, 45)),
            BorderThickness = new Thickness(1),
            Child = settingsPanel
        };
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
            RebuildCharacterDetailContent(ch);
            Vm.RefreshRosterTree();
            RedrawMarkers();
        };
        suppress = false;
        return statusCombo;
    }

    private void BuildCharPopupActions(WowCharacter ch, bool isWowSync)
    {
        CharacterDetailActionsHost.Children.Clear();

        if (!isWowSync)
        {
            CharacterDetailActionsHost.Children.Add(MakeActionButton("🗑 Suppr", "#FFCC3333", () =>
            {
                if (MessageBox.Show($"Supprimer \"{ch.Name}\" ?", "Confirmation",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    NavigateBackFromCharacterDetail();
                    Vm.RemoveCharacterCommand.Execute(ch);
                    RequestMapMarkersRefresh();
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

    private void StatusFrame_DragOver(CharacterStatus status, DragEventArgs e)
    {
        if (!TryGetCharacterFromDrag(e, out _))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
        SetHighlightedDropFrame(FindStatusFrameByCategory(status));
    }

    private void StatusFrame_Drop(CharacterStatus status, DragEventArgs e)
    {
        SetHighlightedDropFrame(null);
        if (!TryGetCharacterFromDrag(e, out var ch) || ch == null)
            return;

        Vm.MoveCharacterToCategoryFrame(ch, status);
        CloseCharacterTooltip();
        RequestMapMarkersRefresh();
        e.Handled = true;
    }

    private Border? FindStatusFrameByCategory(CharacterStatus status)
    {
        if (CharacterRosterRoot == null)
            return null;

        return EnumerateStatusFrames(CharacterRosterRoot)
            .FirstOrDefault(f => f.Tag is CharacterStatus s && s == status);
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
        RequestMapMarkersRefresh();
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
        _lastMarkerRevision = int.MinValue;
        RequestMapMarkersRefresh();
        OpenCharacterDetail(ch);
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
            var shardItem = new WowItem { ItemId = 6265, Name = "Fragment d'âme", Count = ch.ShardCount, Quality = 1 };
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
