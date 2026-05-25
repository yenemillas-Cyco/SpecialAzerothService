using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WindowsOrganiserApp.Models.Carto;
using WindowsOrganiserApp.Services;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

public partial class CartoView
{
    private sealed class CapitalMapSlot
    {
        public required CapitalMapDefinition Definition { get; init; }
        public required Border Host { get; init; }
        public required Image Image { get; init; }
        public required Canvas Canvas { get; init; }
        public required Grid MapHost { get; init; }
    }

    private readonly List<CapitalMapSlot> _capitalSlots = [];
    private CapitalMapSlot? _zoneDragCapitalSlot;
    private bool _capitalSlotsWired;

    private void WireCapitalSlots()
    {
        if (_capitalSlotsWired)
            return;

        UnwrapLegacyPerCapitalScrolls();

        _capitalSlots.Clear();
        RegisterCapitalSlot(1454, CapitalHost_1454, CapitalCanvas_1454, CapitalImage_1454, CapitalMapHost_1454);
        RegisterCapitalSlot(1456, CapitalHost_1456, CapitalCanvas_1456, CapitalImage_1456, CapitalMapHost_1456);
        RegisterCapitalSlot(1458, CapitalHost_1458, CapitalCanvas_1458, CapitalImage_1458, CapitalMapHost_1458);
        RegisterCapitalSlot(1453, CapitalHost_1453, CapitalCanvas_1453, CapitalImage_1453, CapitalMapHost_1453);
        RegisterCapitalSlot(1455, CapitalHost_1455, CapitalCanvas_1455, CapitalImage_1455, CapitalMapHost_1455);
        RegisterCapitalSlot(1457, CapitalHost_1457, CapitalCanvas_1457, CapitalImage_1457, CapitalMapHost_1457);

        _capitalSlotsWired = _capitalSlots.Count == CapitalMapDefinitions.All.Count;
    }

    private void UnwrapLegacyPerCapitalScrolls()
    {
        foreach (var mapHost in new[]
                 {
                     CapitalMapHost_1454, CapitalMapHost_1456, CapitalMapHost_1458,
                     CapitalMapHost_1453, CapitalMapHost_1455, CapitalMapHost_1457
                 })
        {
            if (mapHost?.Parent is not ScrollViewer scroll || scroll.Parent is not Grid parent)
                continue;

            var row = Grid.GetRow(scroll);
            scroll.Content = null;
            parent.Children.Remove(scroll);
            Grid.SetRow(mapHost, row);
            parent.Children.Add(mapHost);
        }

        if (CapitalsGrid?.Parent is ScrollViewer capitalsScroll && CapitalsDock != null
            && capitalsScroll != MapScroll)
        {
            capitalsScroll.Content = null;
            CapitalsDock.Children.Clear();
            CapitalsDock.Children.Add(CapitalsGrid);
        }

        EnsureMapScrollTree();
    }

    private void EnsureMapScrollTree()
    {
        if (MapScroll == null || MapContentFrame == null || MapContainer == null || MapBorder == null)
            return;

        if (MapContainer.Parent != MapContentFrame)
            MapContentFrame.Child = MapContainer;

        if (MapScroll.Content != MapContentFrame)
            MapScroll.Content = MapContentFrame;

        // Garder MapHostGrid (carte + overlay) — ne pas remplacer par MapScroll seul.
        if (MapHostGrid != null)
        {
            if (MapBorder.Child != MapHostGrid)
                MapBorder.Child = MapHostGrid;
            return;
        }

        if (MapBorder.Child != MapScroll)
            MapBorder.Child = MapScroll;
    }

    private static BitmapImage LoadCapitalBitmap(string assetFileName)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri($"pack://application:,,,/Assets/Capitals/{assetFileName}");
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void ScheduleCapitalBitmapLoad(Image image, string assetFileName)
    {
        _ = Task.Run(() => LoadCapitalBitmap(assetFileName)).ContinueWith(
            t =>
            {
                if (!t.IsCompletedSuccessfully)
                    return;

                Dispatcher.BeginInvoke(() =>
                {
                    if (image.Source == null)
                        image.Source = t.Result;
                }, DispatcherPriority.Background);
            },
            TaskScheduler.Default);
    }

    private void RegisterCapitalSlot(int mapId, Border? host, Canvas? canvas, Image? image, Grid? mapHost)
    {
        if (host == null || canvas == null || image == null || mapHost == null)
            return;

        var def = CapitalMapDefinitions.All.FirstOrDefault(d => d.MapId == mapId);
        if (def == null) return;

        canvas.SetBinding(WidthProperty, new System.Windows.Data.Binding("ActualWidth") { Source = mapHost });
        canvas.SetBinding(HeightProperty, new System.Windows.Data.Binding("ActualHeight") { Source = mapHost });
        Panel.SetZIndex(image, 0);
        Panel.SetZIndex(canvas, 1);

        image.Stretch = Stretch.Uniform;
        image.HorizontalAlignment = HorizontalAlignment.Stretch;
        image.VerticalAlignment = VerticalAlignment.Stretch;
        if (image.Source == null)
            ScheduleCapitalBitmapLoad(image, def.AssetFileName);

        var slot = new CapitalMapSlot
        {
            Definition = def,
            Host = host,
            Image = image,
            Canvas = canvas,
            MapHost = mapHost
        };
        _capitalSlots.Add(slot);

        mapHost.SizeChanged += (_, _) =>
        {
            RedrawCapitalZoneEditor(slot);
            RedrawCapitalMarkers(slot);
        };

        host.MouseLeftButtonDown += (_, e) => CapitalHost_MouseDown(slot, e);
        host.MouseMove += (_, e) => CapitalHost_MouseMove(slot, e);
        host.MouseLeftButtonUp += (_, e) => CapitalHost_MouseUp(slot, e);
    }

    private static bool IsCapitalMarkerHit(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Ellipse { Tag: WowCharacter } or Border { Tag: WowCharacter })
                return true;
            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static bool TryGetCharacterCapitalMapId(WowCharacter ch, CartoViewModel vm, out int mapId)
    {
        mapId = 0;
        if (ch.IsExternal) return false;
        var sync = vm.FindWowSyncCharacter(ch);
        if (sync == null) return false;
        mapId = sync.MapId;
        return ClassicEraMapProjection.IsCapitalMap(mapId);
    }

    private static bool TryGetCapitalLocalPosition(WowCharacter ch, CartoViewModel vm, int capitalMapId, out double x, out double y)
    {
        x = 0;
        y = 0;
        var sync = vm.FindWowSyncCharacter(ch);
        if (sync == null) return false;
        if (sync.MapId != capitalMapId) return false;
        x = sync.X;
        y = sync.Y;
        ClassicEraMapProjection.NormalizeCoords(ref x, ref y);
        return x > 0 || y > 0;
    }

    private void RedrawCapitalMaps()
    {
        if (!_capitalSlotsWired)
            return;

        foreach (var slot in _capitalSlots)
        {
            RedrawCapitalZoneEditor(slot);
            RedrawCapitalMarkers(slot);
        }
    }

    private void RedrawCapitalZoneEditor(CapitalMapSlot slot)
    {
        if (Vm == null) return;

        for (var i = slot.Canvas.Children.Count - 1; i >= 0; i--)
        {
            if (slot.Canvas.Children[i] is FrameworkElement { Tag: string tag }
                && (tag == ZoneEditTag || tag == "zone-edit-handle"))
                slot.Canvas.Children.RemoveAt(i);
        }

        if (!Vm.IsZoneEditMode) return;

        var w = slot.Canvas.ActualWidth;
        var h = slot.Canvas.ActualHeight;
        if (w <= 1 || h <= 1) return;

        var focusZone = Vm.IsZonesPanelOpen && Vm.SelectedZoneRect != null;
        var zones = Vm.ZoneRects
            .Where(z => z.MapId == slot.Definition.MapId)
            .Where(z => !focusZone || ReferenceEquals(z, Vm.SelectedZoneRect))
            .OrderBy(z => ReferenceEquals(z, Vm.SelectedZoneRect) ? 1 : 0);
        foreach (var zone in zones)
            DrawZoneRectOnCanvas(slot.Canvas, zone, w, h, Vm.SelectedZoneRect);
    }

    private void DrawZoneRectOnCanvas(
        Canvas canvas,
        CartoZoneRectItem zone,
        double w,
        double h,
        CartoZoneRectItem? selectedZone)
    {
        var selected = ReferenceEquals(zone, selectedZone);
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
        canvas.Children.Add(rect);

        var label = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x10, 0x18, 0x10)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Child = new TextBlock
            {
                Text = string.IsNullOrEmpty(zone.DisplayName) ? zone.NameFr : zone.DisplayName,
                FontSize = 9,
                Foreground = Brushes.White
            },
            Tag = ZoneEditTag,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, zone.Left * w + 2);
        Canvas.SetTop(label, zone.Top * h + 2);
        Panel.SetZIndex(label, selected ? 81 : 16);
        canvas.Children.Add(label);

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
            canvas.Children.Add(handle);
        }
    }

    private void RedrawCapitalMarkers(CapitalMapSlot slot)
    {
        if (Vm == null) return;

        for (var i = slot.Canvas.Children.Count - 1; i >= 0; i--)
        {
            if (slot.Canvas.Children[i] is Ellipse { Tag: WowCharacter }
                || slot.Canvas.Children[i] is Border { Tag: WowCharacter }
                || slot.Canvas.Children[i] is Border { Tag: "shard-label" })
                slot.Canvas.Children.RemoveAt(i);
        }

        var w = slot.Canvas.ActualWidth;
        var h = slot.Canvas.ActualHeight;
        if (w <= 1 || h <= 1) return;

        var mapId = slot.Definition.MapId;

        foreach (var ch in Vm.FilteredCharacters
                     .Where(c => c.IsPlacedOnMap && !c.IsExternal)
                     .Where(c => TryGetCapitalLocalPosition(c, Vm, mapId, out _, out _)))
        {
            if (!TryGetCapitalLocalPosition(ch, Vm, mapId, out var mapX, out var mapY))
                continue;

            AddCapitalCharacterMarker(slot.Canvas, ch, w, h, mapX, mapY);
        }
    }

    private void AddCapitalCharacterMarker(
        Canvas canvas,
        WowCharacter ch,
        double mapW,
        double mapH,
        double mapX,
        double mapY)
    {
        var isTpBoy = ch.Status == CharacterStatus.TpBoy;
        var brush = GetClassBrush(WowClassColors.GetHexColor(ch.Class));
        var isSelected = ch == Vm.SelectedCharacter;
        var size = GetMapMarkerDotSize(isSelected, isTpBoy);

        Brush strokeBrush;
        if (isSelected) strokeBrush = Brushes.White;
        else if (isTpBoy) strokeBrush = new SolidColorBrush(Color.FromRgb(148, 130, 201));
        else strokeBrush = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0));

        var marker = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = brush,
            Stroke = strokeBrush,
            StrokeThickness = isTpBoy ? 1.5 : (isSelected ? 1.5 : 1),
            Cursor = Cursors.Hand,
            Tag = ch,
            ToolTip = ch.Name
        };
        Panel.SetZIndex(marker, isTpBoy ? 14 : 10);

        var pixX = mapX * mapW;
        var pixY = mapY * mapH;
        Canvas.SetLeft(marker, pixX - size / 2);
        Canvas.SetTop(marker, pixY - size / 2);
        canvas.Children.Add(marker);

        if (Vm == null) return;

        var label = BuildMapCharacterLabel(ch, Vm, brush, isSelected, isTpBoy, out var inlineShard);
        var labelW = EstimateLabelWidth(GetMapLabelText(ch, Vm), inlineShard);
        canvas.Children.Add(label);
        Panel.SetZIndex(label, isTpBoy ? 18 : 15);
        PlaceMapCharacterLabel(label, pixX, pixY, size, labelW, mapW, mapH);
    }

    private bool TryHitZoneOnCanvas(
        Point mapPos,
        double mapW,
        double mapH,
        int mapId,
        out CartoZoneRectItem? zone,
        out bool isResizeHandle)
    {
        zone = null;
        isResizeHandle = false;
        if (Vm == null || !Vm.IsZoneEditMode) return false;

        var nx = mapPos.X / mapW;
        var ny = mapPos.Y / mapH;
        const double handlePx = 22;
        var handleN = handlePx / Math.Max(mapW, mapH);

        IEnumerable<CartoZoneRectItem> Ordered()
        {
            if (Vm.SelectedZoneRect != null && Vm.SelectedZoneRect.MapId == mapId)
                yield return Vm.SelectedZoneRect;
            for (var i = Vm.ZoneRects.Count - 1; i >= 0; i--)
            {
                var z = Vm.ZoneRects[i];
                if (z.MapId != mapId || ReferenceEquals(z, Vm.SelectedZoneRect))
                    continue;
                yield return z;
            }
        }

        foreach (var z in Ordered())
        {
            var right = z.Left + z.Width;
            var bottom = z.Top + z.Height;
            if (nx >= right - handleN && nx <= right + handleN * 0.35
                && ny >= bottom - handleN && ny <= bottom + handleN * 0.35)
            {
                zone = z;
                isResizeHandle = true;
                return true;
            }
        }

        foreach (var z in Ordered())
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

    private void CapitalHost_MouseDown(CapitalMapSlot slot, MouseButtonEventArgs e)
    {
        if (Vm == null) return;

        var mapPos = e.GetPosition(slot.Canvas);
        if (Vm.IsZoneEditMode)
        {
            if (TryHitZoneOnCanvas(mapPos, slot.Canvas.ActualWidth, slot.Canvas.ActualHeight, slot.Definition.MapId, out var hit, out var resize) && hit != null)
            {
                Vm.SelectedZoneRect = hit;
                _zoneDragItem = hit;
                _zoneResizeDrag = resize;
                _zoneDragCapitalSlot = slot;
                _zoneDragStartMap = mapPos;
                _zoneDragStartLeft = hit.Left;
                _zoneDragStartTop = hit.Top;
                _zoneDragStartW = hit.Width;
                _zoneDragStartH = hit.Height;
                slot.Host.CaptureMouse();
                RedrawCapitalZoneEditor(slot);
                e.Handled = true;
            }

            return;
        }

        if (e.OriginalSource is Ellipse { Tag: WowCharacter ch })
        {
            Vm.SelectedCharacter = ch;
            RedrawCapitalMaps();
            e.Handled = true;
        }
    }

    private void CapitalHost_MouseMove(CapitalMapSlot slot, MouseEventArgs e)
    {
        if (_zoneDragItem == null || _zoneDragCapitalSlot != slot) return;
        if (!slot.Host.IsMouseCaptured) return;

        var mapPos = e.GetPosition(slot.Canvas);
        var w = slot.Canvas.ActualWidth;
        var h = slot.Canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var dx = (mapPos.X - _zoneDragStartMap.X) / w;
        var dy = (mapPos.Y - _zoneDragStartMap.Y) / h;

        if (_zoneResizeDrag)
        {
            var (minW, minH) = ClassicEraMapProjection.GetEditorMinimumZoneSize(_zoneDragItem.MapId);
            _zoneDragItem.Width = Math.Clamp(_zoneDragStartW + dx, minW, 1 - _zoneDragItem.Left);
            _zoneDragItem.Height = Math.Clamp(_zoneDragStartH + dy, minH, 1 - _zoneDragItem.Top);
        }
        else
        {
            _zoneDragItem.Left = Math.Clamp(_zoneDragStartLeft + dx, 0, 1 - _zoneDragItem.Width);
            _zoneDragItem.Top = Math.Clamp(_zoneDragStartTop + dy, 0, 1 - _zoneDragItem.Height);
        }

        RedrawCapitalZoneEditor(slot);
    }

    private void CapitalHost_MouseUp(CapitalMapSlot slot, MouseButtonEventArgs e)
    {
        if (_zoneDragCapitalSlot != slot || _zoneDragItem == null) return;

        slot.Host.ReleaseMouseCapture();
        Vm?.PersistZoneRects();
        _zoneDragItem = null;
        _zoneDragCapitalSlot = null;
        _zoneResizeDrag = false;
    }
}
