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
    private sealed class CapitalMapSlot
    {
        public required CapitalMapDefinition Definition { get; init; }
        public required BitmapSource Source { get; init; }
    }

    private readonly List<CapitalMapSlot> _capitalSlots = [];
    private bool _capitalCompositeWired;

    private readonly record struct CapitalContentRect(double X, double Y, double Width, double Height);

    private Canvas? CapitalOverlayCanvas => CapitalsCompositeCanvas;

    private Image? CapitalHitHost => MapImage;

    private bool TryGetMapPixelSize(out int mapW, out int mapH)
    {
        mapW = 0;
        mapH = 0;
        if (MapImage != null && MapImage.ActualWidth > 1 && MapImage.ActualHeight > 1)
        {
            mapW = (int)Math.Round(MapImage.ActualWidth);
            mapH = (int)Math.Round(MapImage.ActualHeight);
            return true;
        }

        mapW = (int)Math.Round(MapWidth);
        mapH = (int)Math.Round(MapHeight);
        return mapW > 1 && mapH > 1;
    }

    private bool TryGetCapitalsBandRect(out CapitalContentRect content)
    {
        content = default;
        if (MapImage == null)
            return false;

        if (!TryGetMapPixelSize(out var mapW, out var mapH))
            return false;

        var (x, y, w, h) = WowMapLayout.GetCapitalsBandRect(mapW, mapH);
        content = new(x, y, w, h);
        return w > 1 && h > 1;
    }

    private bool TryGetCompositeContentRect(out CapitalContentRect content) =>
        TryGetCapitalsBandRect(out content);

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

    private bool TryGetCapitalContentRect(CapitalMapSlot slot, out CapitalContentRect content) =>
        TryGetCapitalCellPixels(slot.Definition, out content);

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

    private bool TryGetCapitalCellPixels(CapitalMapDefinition def, out CapitalContentRect cell) =>
        TryGetCapitalCellPixels(def.GridColumn, def.GridRow, out cell);

    private bool TryGetCapitalCellPixels(int gridColumn, int gridRow, out CapitalContentRect cell)
    {
        cell = default;
        if (!TryGetMapPixelSize(out var mapW, out var mapH))
            return false;

        var (cx, cy, cw, ch) = WowMapLayout.GetCapitalCellRect(mapW, mapH, gridColumn, gridRow);
        cell = new(cx, cy, cw, ch);
        return cw > 1 && ch > 1;
    }

    private bool TryResolveSlotFromPoint(Point hostPos, out CapitalMapSlot? slot)
    {
        slot = null;
        if (!TryGetMapPixelSize(out var mapW, out var mapH))
            return false;

        var (bx, by, bw, bh) = WowMapLayout.GetCapitalsBandRect(mapW, mapH);
        if (hostPos.X < bx || hostPos.Y < by || hostPos.X > bx + bw || hostPos.Y > by + bh)
            return false;
        foreach (var def in CapitalMapDefinitions.All)
        {
            var (cellX, cellY, cellW, cellH) = WowMapLayout.GetCapitalCellRect(
                mapW,
                mapH,
                def.GridColumn,
                def.GridRow);
            if (hostPos.X < cellX || hostPos.Y < cellY || hostPos.X > cellX + cellW || hostPos.Y > cellY + cellH)
                continue;

            slot = _capitalSlots.FirstOrDefault(s => s.Definition.MapId == def.MapId);
            return slot != null;
        }

        return false;
    }

    private void WireCapitalSlots()
    {
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
        if (CapitalOverlayCanvas == null || CapitalHitHost == null)
            return;

        IReadOnlyDictionary<int, BitmapSource> sources;
        try
        {
            sources = CapitalMapsCompositeBuilder.LoadCapitalSourcesFromPack();
        }
        catch (Exception ex)
        {
            Vm?.ZonePanelStatusMessage = $"Images capitales indisponibles : {ex.Message}";
            return;
        }

        Panel.SetZIndex(CapitalOverlayCanvas, 2);

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
        if (CapitalHitHost == null)
            return;

    }

    /// <summary>Route un clic vers la mosaïque capitales (contourne volet droit / canvas overlay).</summary>
    private bool TryProcessCapitalMapPointer(MouseButtonEventArgs e)
    {
        if (Vm == null || e.Handled)
            return false;

        if (DateTime.UtcNow < Vm.SuppressCapitalMapClickUntilUtc)
            return false;

        if (!CartoRuntimeOptions.ShowCapitalMaps || CapitalHitHost == null)
            return false;

        if (CapitalHitHost.ActualWidth < 2 || CapitalHitHost.ActualHeight < 2)
            return false;

        if (e.OriginalSource is DependencyObject src && IsZonesPanelChrome(src))
            return false;

        var hostPos = e.GetPosition(CapitalHitHost);

        if (!TryGetCapitalsBandRect(out var band)
            || hostPos.X < band.X || hostPos.Y < band.Y
            || hostPos.X > band.X + band.Width || hostPos.Y > band.Y + band.Height)
            return false;

        if (!TryResolveSlotFromPoint(hostPos, out var slot) || slot == null)
            return false;

        CapitalHost_MouseDown(slot, hostPos, e);
        return e.Handled;
    }

    private static bool IsZonesPanelChrome(DependencyObject node)
    {
        while (node != null)
        {
            if (node is FrameworkElement { Name: "ZonesPanelHost" or "RightDockHost" })
                return true;
            node = VisualTreeHelper.GetParent(node);
        }

        return false;
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

        foreach (var slot in _capitalSlots)
        {
            RedrawCapitalZoneEditor(slot);
            RedrawCapitalMarkers(slot);
        }
    }

    private void RedrawCapitalZoneEditor(CapitalMapSlot slot)
    {
        if (CapitalOverlayCanvas == null)
            return;

        var mapId = slot.Definition.MapId;
        for (var i = CapitalOverlayCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (CapitalOverlayCanvas.Children[i] is FrameworkElement fe
                && fe.Tag is CartoZoneRectItem z
                && z.MapId == mapId)
                CapitalOverlayCanvas.Children.RemoveAt(i);
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
            .Where(c => TryGetCharacterCapitalMapId(c, Vm, out var mid) && mid == mapId)
            .Where(c => Vm.TryGetMarkerPosition(c, out _, out _))
            .ToList();

        var mapW = MapWidth;
        var mapH = MapHeight;
        var labelRequests = new List<CartoMapLabelLayout.LabelRequest>();
        var drawItems = new List<(WowCharacter Ch, double PixX, double PixY)>();

        foreach (var ch in chars)
        {
            if (!Vm.TryGetMarkerPosition(ch, out var mapX, out var mapY))
                continue;

            var isTpBoy = ch.Status == CharacterStatus.TpBoy;
            var isSelected = ch == Vm.SelectedCharacter;
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
            drawItems.Add((ch, pixX, pixY));
        }

        var labelPositions = CartoMapLabelLayout.Resolve(
                labelRequests,
                CapitalOverlayCanvas.ActualWidth,
                CapitalOverlayCanvas.ActualHeight)
            .ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (ch, pixX, pixY) in drawItems)
            AddCapitalCharacterMarker(CapitalOverlayCanvas, ch, content, pixX, pixY, labelPositions);
    }

    private void AddCapitalCharacterMarker(
        Canvas canvas,
        WowCharacter ch,
        CapitalContentRect content,
        double pixX,
        double pixY,
        IReadOnlyDictionary<string, CartoMapLabelLayout.LabelPosition> labelPositions)
    {
        _ = content;
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

    private void CapitalHost_MouseDown(CapitalMapSlot slot, Point mapPos, MouseButtonEventArgs e)
    {
        if (Vm == null || Vm.IsZonesPanelOpen)
            return;

        if (e.OriginalSource is Ellipse { Tag: WowCharacter ch })
        {
            Vm.SelectedCharacter = ch;
            RedrawCapitalMaps();
            e.Handled = true;
        }
    }

}
