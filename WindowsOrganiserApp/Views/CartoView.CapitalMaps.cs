using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Services;
using WindowsOrganiserApp.Services;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

public partial class CartoView
{
    private const string CapitalSelectionTag = "capital-cell-selection";

    private sealed class CapitalMapSlot
    {
        public required CapitalMapDefinition Definition { get; init; }
        public required BitmapSource Source { get; init; }
    }

    private readonly List<CapitalMapSlot> _capitalSlots = [];
    private CapitalMapSlot? _zoneDragCapitalSlot;
    private bool _capitalCompositeWired;

    private readonly record struct CapitalContentRect(double X, double Y, double Width, double Height);

    private Canvas CapitalOverlayCanvas => CapitalsCompositeCanvas;
    private Grid CapitalHitHost => CapitalsCompositeHost;

    private bool TryGetCompositeContentRect(out CapitalContentRect content)
    {
        content = default;
        if (CapitalsCompositeHost == null || CapitalsCompositeImage?.Source is not BitmapSource bmp)
            return false;

        var hostW = CapitalsCompositeHost.ActualWidth;
        var hostH = CapitalsCompositeHost.ActualHeight;
        if (hostW <= 1 || hostH <= 1 || bmp.PixelWidth < 1 || bmp.PixelHeight < 1)
            return false;

        var imgW = (double)bmp.PixelWidth;
        var imgH = (double)bmp.PixelHeight;
        var scale = Math.Min(hostW / imgW, hostH / imgH);
        var cw = imgW * scale;
        var ch = imgH * scale;
        content = new((hostW - cw) / 2, (hostH - ch) / 2, cw, ch);
        return cw > 1 && ch > 1;
    }

    private static bool TryGetCapitalCellRect(CapitalMapDefinition def, CapitalContentRect composite, out CapitalContentRect cell)
    {
        var (nx, ny, nw, nh) = CapitalMapsCompositeLayout.GetCellNormalizedRect(def.GridColumn, def.GridRow);
        cell = new(
            composite.X + nx * composite.Width,
            composite.Y + ny * composite.Height,
            nw * composite.Width,
            nh * composite.Height);
        return cell.Width > 1 && cell.Height > 1;
    }

    private bool TryGetCapitalContentRect(CapitalMapSlot slot, out CapitalContentRect content)
    {
        content = default;
        if (!TryGetCompositeContentRect(out var composite))
            return false;

        if (!TryGetCapitalCellRect(slot.Definition, composite, out var cell))
            return false;

        var bmp = slot.Source;
        if (bmp.PixelWidth < 1 || bmp.PixelHeight < 1)
            return false;

        var imgW = (double)bmp.PixelWidth;
        var imgH = (double)bmp.PixelHeight;
        var scale = Math.Min(cell.Width / imgW, cell.Height / imgH);
        var cw = imgW * scale;
        var ch = imgH * scale;
        content = new(cell.X + (cell.Width - cw) / 2, cell.Y + (cell.Height - ch) / 2, cw, ch);
        return cw > 1 && ch > 1;
    }

    private static bool TryMapHostPointToNormalized(
        CapitalContentRect content,
        Point mapHostPos,
        out Point normalized)
    {
        var x = (mapHostPos.X - content.X) / content.Width;
        var y = (mapHostPos.Y - content.Y) / content.Height;
        if (x < 0 || x > 1 || y < 0 || y > 1)
        {
            normalized = default;
            return false;
        }

        normalized = new Point(x, y);
        return true;
    }

    private static Point MapHostPointToNormalizedClamped(CapitalContentRect content, Point mapHostPos) =>
        new(
            Math.Clamp((mapHostPos.X - content.X) / content.Width, 0, 1),
            Math.Clamp((mapHostPos.Y - content.Y) / content.Height, 0, 1));

    private void EnsureCapitalSlotsWired()
    {
        if (_capitalSlots.Count < CapitalMapDefinitions.All.Count)
            WireCapitalSlots();
    }

    private bool TryResolveSlotFromPoint(Point hostPos, out CapitalMapSlot? slot)
    {
        slot = null;
        if (!TryGetCompositeContentRect(out var composite))
            return false;

        if (hostPos.X < composite.X || hostPos.Y < composite.Y
            || hostPos.X > composite.X + composite.Width || hostPos.Y > composite.Y + composite.Height)
            return false;

        foreach (var def in CapitalMapDefinitions.All)
        {
            var (nx, ny, nw, nh) = CapitalMapsCompositeLayout.GetCellNormalizedRect(def.GridColumn, def.GridRow);
            var cellX = composite.X + nx * composite.Width;
            var cellY = composite.Y + ny * composite.Height;
            var cellW = nw * composite.Width;
            var cellH = nh * composite.Height;
            if (hostPos.X < cellX || hostPos.Y < cellY || hostPos.X > cellX + cellW || hostPos.Y > cellY + cellH)
                continue;

            slot = _capitalSlots.FirstOrDefault(s => s.Definition.MapId == def.MapId);
            return slot != null;
        }

        return false;
    }

    private void SyncCapitalHostSelectionHighlight()
    {
        if (CapitalOverlayCanvas == null)
            return;

        for (var i = CapitalOverlayCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (CapitalOverlayCanvas.Children[i] is FrameworkElement { Tag: string tag } && tag == CapitalSelectionTag)
                CapitalOverlayCanvas.Children.RemoveAt(i);
        }

        if (Vm == null || !Vm.IsZonesPanelOpen || Vm.SelectedZoneRect?.MapId is not int selectedMapId)
            return;

        var slot = _capitalSlots.FirstOrDefault(s => s.Definition.MapId == selectedMapId);
        if (slot == null
            || !TryGetCompositeContentRect(out var composite)
            || !TryGetCapitalCellRect(slot.Definition, composite, out var cell))
            return;

        var frame = new Rectangle
        {
            Width = Math.Max(2, cell.Width),
            Height = Math.Max(2, cell.Height),
            Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x66)),
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false,
            Tag = CapitalSelectionTag
        };
        Canvas.SetLeft(frame, cell.X);
        Canvas.SetTop(frame, cell.Y);
        Panel.SetZIndex(frame, 5);
        CapitalOverlayCanvas.Children.Add(frame);
    }

    private void WireCapitalSlots()
    {
        if (CapitalsGrid?.Parent is ScrollViewer capitalsScroll && CapitalsDock != null
            && capitalsScroll != MapScroll)
        {
            capitalsScroll.Content = null;
            CapitalsDock.Children.Clear();
            CapitalsDock.Children.Add(CapitalsGrid);
        }

        EnsureMapScrollTree();

        if (_capitalCompositeWired && _capitalSlots.Count == CapitalMapDefinitions.All.Count)
            return;

        LoadCapitalCompositeUi();
    }

    private void EnsureMapScrollTree()
    {
        if (MapScroll == null || MapContentFrame == null || MapContainer == null || MapBorder == null)
            return;

        if (MapContainer.Parent != MapContentFrame)
            MapContentFrame.Child = MapContainer;

        if (MapScroll.Content != MapContentFrame)
            MapScroll.Content = MapContentFrame;

        if (MapHostGrid != null)
        {
            if (MapBorder.Child != MapHostGrid)
                MapBorder.Child = MapHostGrid;
            return;
        }

        if (MapBorder.Child != MapScroll)
            MapBorder.Child = MapScroll;
    }

    private void LoadCapitalCompositeUi()
    {
        if (CapitalsCompositeImage == null || CapitalOverlayCanvas == null || CapitalHitHost == null)
            return;

        IReadOnlyDictionary<int, BitmapSource> sources;
        BitmapSource composite;
        try
        {
            sources = CapitalMapsCompositeBuilder.LoadCapitalSourcesFromPack();
            try
            {
                composite = CapitalMapsCompositeBuilder.LoadCompositeFromPackResource();
            }
            catch
            {
                composite = CapitalMapsCompositeBuilder.Build().Composite;
            }
        }
        catch (Exception ex)
        {
            Vm?.ZonePanelStatusMessage = $"Image capitales indisponible : {ex.Message}";
            return;
        }

        CapitalsCompositeImage.Source = composite;
        Panel.SetZIndex(CapitalsCompositeImage, 0);
        Panel.SetZIndex(CapitalOverlayCanvas, 1);

        CapitalOverlayCanvas.SetBinding(
            WidthProperty,
            new System.Windows.Data.Binding("ActualWidth") { Source = CapitalHitHost });
        CapitalOverlayCanvas.SetBinding(
            HeightProperty,
            new System.Windows.Data.Binding("ActualHeight") { Source = CapitalHitHost });

        _capitalSlots.Clear();
        foreach (var def in CapitalMapDefinitions.All)
        {
            if (!sources.TryGetValue(def.MapId, out var source))
                continue;

            _capitalSlots.Add(new CapitalMapSlot { Definition = def, Source = source });
        }

        if (!_capitalCompositeWired)
        {
            WireCapitalCompositeInput();
            CapitalHitHost.SizeChanged += (_, _) => RedrawCapitalMaps();
            _capitalCompositeWired = true;
        }

        RedrawCapitalMaps();
    }

    private void WireCapitalCompositeInput()
    {
        void OnPreviewDown(object _, MouseButtonEventArgs ev)
        {
            if (TryProcessCapitalMapPointer(ev))
                ev.Handled = true;
        }

        CapitalHitHost.AddHandler(
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnPreviewDown),
            true);
        CapitalsCompositeImage.AddHandler(
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnPreviewDown),
            true);

        MapBorder?.AddHandler(
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnPreviewDown),
            true);

        CapitalHitHost.MouseMove += (_, ev) =>
        {
            if (_zoneDragCapitalSlot == null)
                return;

            if (!CapitalHitHost.IsMouseCaptured)
                return;

            ProcessCapitalZoneDrag(_zoneDragCapitalSlot, ev.GetPosition(CapitalHitHost));
        };
        CapitalHitHost.MouseLeftButtonUp += (_, _) => EndCapitalZoneDrag();
    }

    /// <summary>Route un clic vers la mosaïque capitales (contourne volet droit / canvas overlay).</summary>
    private bool TryProcessCapitalMapPointer(MouseButtonEventArgs e)
    {
        if (Vm == null || e.Handled)
            return false;

        if (CapitalsDock?.Visibility != Visibility.Visible || CapitalHitHost == null)
            return false;

        if (CapitalHitHost.ActualWidth < 2 || CapitalHitHost.ActualHeight < 2)
            return false;

        var hostPos = e.GetPosition(CapitalHitHost);
        if (VisualTreeHelper.HitTest(CapitalHitHost, hostPos) == null)
            return false;

        if (!TryResolveSlotFromPoint(hostPos, out var slot) || slot == null)
        {
            if (Vm.IsZonesPanelOpen && Vm.IsPlacingCapitalZone)
            {
                Vm.ZonePanelStatusMessage =
                    "Cliquez sur la capitale choisie dans l'image (à droite d'Azeroth), pas sur Azeroth.";
            }

            return Vm.IsPlacingCapitalZone;
        }

        CapitalHost_MouseDown(slot, hostPos, e);
        return e.Handled;
    }

    private static bool TryGetCharacterCapitalMapId(WowCharacter ch, CartoViewModel vm, out int mapId)
    {
        mapId = 0;
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
        if (_capitalSlots.Count == 0)
            return;

        SyncCapitalHostSelectionHighlight();

        foreach (var slot in _capitalSlots)
        {
            RedrawCapitalZoneEditor(slot);
            RedrawCapitalMarkers(slot);
        }
    }

    private void RedrawCapitalZoneEditor(CapitalMapSlot slot)
    {
        if (Vm == null || CapitalOverlayCanvas == null)
            return;

        var mapId = slot.Definition.MapId;
        for (var i = CapitalOverlayCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (CapitalOverlayCanvas.Children[i] is not FrameworkElement fe)
                continue;
            if (fe.Tag as string == CapitalSelectionTag)
                continue;
            if (fe.Tag is CartoZoneRectItem z && z.MapId == mapId)
                CapitalOverlayCanvas.Children.RemoveAt(i);
            else if (fe is Border { Tag: CartoZoneRectItem lz } && lz.MapId == mapId)
                CapitalOverlayCanvas.Children.RemoveAt(i);
        }

        if (!Vm.IsZoneEditMode || !CartoRuntimeOptions.ShowWorldZoneRectOverlays || Vm.IsPlacingCapitalZone)
            return;

        if (!TryGetCapitalContentRect(slot, out var content))
            return;

        var focusZone = Vm.IsZonesPanelOpen && Vm.SelectedZoneRect != null;
        var zones = Vm.ZoneRects
            .Where(z => z.MapId == mapId)
            .Where(z => !focusZone || ReferenceEquals(z, Vm.SelectedZoneRect))
            .OrderBy(z => ReferenceEquals(z, Vm.SelectedZoneRect) ? 1 : 0);
        foreach (var zone in zones)
            DrawZoneRectOnCanvas(CapitalOverlayCanvas, zone, content.Width, content.Height, Vm.SelectedZoneRect, content.X, content.Y);
    }

    private void DrawZoneRectOnCanvas(
        Canvas canvas,
        CartoZoneRectItem zone,
        double w,
        double h,
        CartoZoneRectItem? selectedZone,
        double offsetX = 0,
        double offsetY = 0)
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
            Tag = zone,
            IsHitTestVisible = selected,
            Cursor = selected ? Cursors.SizeAll : Cursors.Arrow
        };
        Canvas.SetLeft(rect, offsetX + zone.Left * w);
        Canvas.SetTop(rect, offsetY + zone.Top * h);
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
            Tag = zone,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, offsetX + zone.Left * w + 2);
        Canvas.SetTop(label, offsetY + zone.Top * h + 2);
        Panel.SetZIndex(label, selected ? 81 : 16);
        canvas.Children.Add(label);

        if (selected)
        {
            const double handleSize = 22;
            var handle = new Ellipse
            {
                Width = handleSize,
                Height = handleSize,
                Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x66)),
                Stroke = Brushes.Black,
                StrokeThickness = 1.5,
                Tag = zone,
                IsHitTestVisible = true,
                Cursor = Cursors.SizeNWSE,
                ToolTip = "Glisser pour redimensionner"
            };
            Canvas.SetLeft(handle, offsetX + (zone.Left + zone.Width) * w - handleSize / 2);
            Canvas.SetTop(handle, offsetY + (zone.Top + zone.Height) * h - handleSize / 2);
            Panel.SetZIndex(handle, 90);
            canvas.Children.Add(handle);
        }
    }

    private void RedrawCapitalMarkers(CapitalMapSlot slot)
    {
        if (Vm == null || CapitalOverlayCanvas == null)
            return;

        var mapId = slot.Definition.MapId;
        for (var i = CapitalOverlayCanvas.Children.Count - 1; i >= 0; i--)
        {
            var child = CapitalOverlayCanvas.Children[i];
            if (child is Ellipse { Tag: WowCharacter ch })
            {
                if (TryGetCharacterCapitalMapId(ch, Vm, out var mid) && mid == mapId)
                    CapitalOverlayCanvas.Children.RemoveAt(i);
            }
            else if (child is Border { Tag: WowCharacter ch2 })
            {
                if (TryGetCharacterCapitalMapId(ch2, Vm, out var mid) && mid == mapId)
                    CapitalOverlayCanvas.Children.RemoveAt(i);
            }
            else if (child is Line { Tag: WowCharacter ch3 })
            {
                if (TryGetCharacterCapitalMapId(ch3, Vm, out var mid) && mid == mapId)
                    CapitalOverlayCanvas.Children.RemoveAt(i);
            }
            else if (child is Border { Tag: "shard-label" })
            {
                CapitalOverlayCanvas.Children.RemoveAt(i);
            }
        }

        if (!TryGetCapitalContentRect(slot, out var content))
            return;

        var chars = Vm.FilteredCharacters
            .Where(c => c.IsPlacedOnMap)
            .Where(c => TryGetCapitalLocalPosition(c, Vm, mapId, out _, out _))
            .ToList();

        var labelRequests = new List<CartoMapLabelLayout.LabelRequest>();
        var drawItems = new List<(WowCharacter Ch, double MapX, double MapY)>();

        foreach (var ch in chars)
        {
            if (!TryGetCapitalLocalPosition(ch, Vm, mapId, out var mapX, out var mapY))
                continue;

            var isTpBoy = ch.Status == CharacterStatus.TpBoy;
            var isSelected = ch == Vm.SelectedCharacter;
            var size = GetMapMarkerDotSize(isSelected, isTpBoy);
            var pixX = content.X + mapX * content.Width;
            var pixY = content.Y + mapY * content.Height;
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
            drawItems.Add((ch, mapX, mapY));
        }

        var labelPositions = CartoMapLabelLayout.Resolve(
                labelRequests,
                CapitalOverlayCanvas.ActualWidth,
                CapitalOverlayCanvas.ActualHeight)
            .ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (ch, mapX, mapY) in drawItems)
            AddCapitalCharacterMarker(CapitalOverlayCanvas, ch, content, mapX, mapY, labelPositions);
    }

    private void AddCapitalCharacterMarker(
        Canvas canvas,
        WowCharacter ch,
        CapitalContentRect content,
        double mapX,
        double mapY,
        IReadOnlyDictionary<string, CartoMapLabelLayout.LabelPosition> labelPositions)
    {
        var isTpBoy = ch.Status == CharacterStatus.TpBoy;
        var brush = GetClassBrush(WowClassColors.GetHexColor(ch.Class));
        var isSelected = ch == Vm.SelectedCharacter;
        var size = GetMapMarkerDotSize(isSelected, isTpBoy);

        Brush strokeBrush;
        if (isSelected) strokeBrush = Brushes.White;
        else if (isTpBoy) strokeBrush = new SolidColorBrush(Color.FromRgb(148, 130, 201));
        else strokeBrush = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0));

        var blockMarkerHits = Vm?.IsZonesPanelOpen == true;
        var marker = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = brush,
            Stroke = strokeBrush,
            StrokeThickness = isTpBoy ? 1.5 : (isSelected ? 1.5 : 1),
            Cursor = blockMarkerHits ? Cursors.Arrow : Cursors.Hand,
            Tag = ch,
            ToolTip = ch.Name,
            IsHitTestVisible = !blockMarkerHits
        };
        Panel.SetZIndex(marker, isTpBoy ? 14 : 10);

        var pixX = content.X + mapX * content.Width;
        var pixY = content.Y + mapY * content.Height;
        Canvas.SetLeft(marker, pixX - size / 2);
        Canvas.SetTop(marker, pixY - size / 2);
        canvas.Children.Add(marker);

        if (Vm == null) return;

        var inlineShard = isTpBoy && ch.Class == WowClass.Demoniste && ch.ShardCount > 0;
        var labelW = EstimateLabelWidth(GetMapLabelText(ch, Vm), inlineShard);
        var canvasW = canvas.ActualWidth;
        var canvasH = canvas.ActualHeight;

        double labelLeft, labelTop;
        if (labelPositions.TryGetValue(ch.Id, out var pos))
        {
            labelLeft = pos.Left;
            labelTop = pos.Top;
        }
        else
        {
            labelTop = pixY - size / 2 - MapLabelHeight - CartoMapLabelLayout.GapAboveDot;
            labelLeft = Math.Clamp(pixX - labelW / 2, 0, Math.Max(0, canvasW - labelW));
            labelTop = Math.Clamp(labelTop, 0, Math.Max(0, canvasH - MapLabelHeight));
        }

        AddMapLabelLeaderLine(canvas, ch, pixX, pixY, size / 2, labelLeft, labelTop, labelW, brush);

        var label = BuildMapCharacterLabel(ch, Vm, brush, isSelected, isTpBoy, out _);
        label.IsHitTestVisible = !blockMarkerHits;
        Canvas.SetLeft(label, labelLeft);
        Canvas.SetTop(label, labelTop);
        canvas.Children.Add(label);
        Panel.SetZIndex(label, isTpBoy ? 18 : 15);
    }

    private bool TryHitZoneOnCanvas(
        Point mapPos,
        int mapId,
        out CartoZoneRectItem? zone,
        out bool isResizeHandle)
    {
        zone = null;
        isResizeHandle = false;
        if (Vm == null || !Vm.IsZonesPanelOpen)
            return false;

        var slot = _capitalSlots.FirstOrDefault(s => s.Definition.MapId == mapId);
        if (slot == null || !TryGetCapitalContentRect(slot, out var content))
            return false;

        var norm = MapHostPointToNormalizedClamped(content, mapPos);
        var nx = norm.X;
        var ny = norm.Y;
        const double handlePx = 44;
        var handleN = handlePx / Math.Max(content.Width, content.Height);
        const double edgeSlop = 0.04;

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
            if (nx < z.Left - edgeSlop || nx > right + edgeSlop || ny < z.Top - edgeSlop || ny > bottom + edgeSlop)
                continue;

            zone = z;
            isResizeHandle = false;
            return true;
        }

        return false;
    }

    private void CapitalHost_MouseDown(CapitalMapSlot slot, Point mapPos, MouseButtonEventArgs e)
    {
        if (Vm == null)
            return;

        var mapId = slot.Definition.MapId;

        if (Vm.IsZonesPanelOpen)
        {
            if (TryHitZoneOnCanvas(mapPos, mapId, out var hit, out var resize) && hit != null)
            {
                Vm.ShowWorldZoneRectOverlays = true;
                CartoRuntimeOptions.ShowWorldZoneRectOverlays = true;
                BeginCapitalZoneDrag(slot, hit, mapPos, resize);
                e.Handled = true;
                return;
            }

            if (!Vm.IsZoneEditMode)
                return;

            if (Vm.IsPlacingCapitalZone)
            {
                var wanted = Vm.CapitalToAddMapId;
                if (wanted is int pick && pick != mapId)
                {
                    var title = CapitalMapDefinitions.All.FirstOrDefault(d => d.MapId == pick)?.Title;
                    Vm.ZonePanelStatusMessage = string.IsNullOrEmpty(title)
                        ? "Cliquez sur la capitale choisie dans l'image."
                        : $"Cliquez sur {title} dans l'image.";
                }
                else if (TryGetCapitalContentRect(slot, out var content))
                {
                    var norm = MapHostPointToNormalizedClamped(content, mapPos);
                    if (Vm.TryAddCapitalZoneAt(mapId, norm.X, norm.Y))
                    {
                        Vm.ShowWorldZoneRectOverlays = true;
                        CartoRuntimeOptions.ShowWorldZoneRectOverlays = true;
                        RedrawCapitalMaps();
                        var created = Vm.ZoneRects.First(z => z.MapId == mapId);
                        BeginCapitalZoneDrag(slot, created, mapPos, false);
                    }
                    else
                    {
                        Vm.ZonePanelStatusMessage =
                            $"{slot.Definition.Title} : impossible de créer la zone — réessayez.";
                    }
                }
                else
                {
                    Vm.ZonePanelStatusMessage =
                        $"{slot.Definition.Title} : carte en chargement ou clic hors image.";
                }

                e.Handled = true;
            }
            else
            {
                Vm.CapitalToAddMapId = mapId;
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

    private void BeginCapitalZoneDrag(
        CapitalMapSlot slot,
        CartoZoneRectItem hit,
        Point mapPos,
        bool resize)
    {
        Vm!.SelectedZoneRect = hit;
        _zoneDragItem = hit;
        _zoneResizeDrag = resize;
        _zoneDragCapitalSlot = slot;
        _zoneDragStartMap = mapPos;
        _zoneDragStartLeft = hit.Left;
        _zoneDragStartTop = hit.Top;
        _zoneDragStartW = hit.Width;
        _zoneDragStartH = hit.Height;
        CapitalHitHost.CaptureMouse();
        RedrawCapitalZoneEditor(slot);
    }

    private void ProcessCapitalZoneDrag(CapitalMapSlot slot, Point mapPos)
    {
        if (_zoneDragItem == null || _zoneDragCapitalSlot != slot)
            return;

        if (!TryGetCapitalContentRect(slot, out var content))
            return;

        var startNorm = MapHostPointToNormalizedClamped(content, _zoneDragStartMap);
        var currentNorm = MapHostPointToNormalizedClamped(content, mapPos);
        var dx = currentNorm.X - startNorm.X;
        var dy = currentNorm.Y - startNorm.Y;

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

    private void EndCapitalZoneDrag()
    {
        if (_zoneDragItem == null)
            return;

        if (Vm != null)
            Vm.IsPlacingCapitalZone = false;

        Vm?.PersistZoneRects();
        _zoneDragItem = null;
        _zoneDragCapitalSlot = null;
        _zoneResizeDrag = false;
        CapitalHitHost.ReleaseMouseCapture();
        RedrawCapitalMaps();
    }
}
