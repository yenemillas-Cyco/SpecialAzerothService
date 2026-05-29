using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsOrganiserApp;
using SpecialAzerothService.Core.Services;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

/// <summary>
/// Initialisation Carto : 1) carte monde 2) données persos 3) UI une fois.
/// Pas de SizeChanged, pas de boucle de zoom, pas de reload si déjà Complete.
/// </summary>
public partial class CartoView
{
    private readonly CartoInitGate _cartoInit = new();
    private double _worldPixelW;
    private double _worldPixelH;
    /// <summary>Filtres / redraws carte désactivés pendant l'init (évite tempêtes PropertyChanged).</summary>
    private bool _cartoUiLive;
    private bool _pendingInitialMapFit;
    private bool _mapFitLayoutHooked;

    private void StartCartoSession()
    {
        if (!IsVisible || Vm == null)
            return;

        if (_cartoInit.IsComplete)
        {
            HideMapLoadingOverlay();
            EnsureMapImageOnUi();
            TryApplyInitialMapFit();
            RefreshMapCharactersWhenReady();
            return;
        }

        if (!_cartoInit.TryBegin())
            return;

        ShowMapLoadingOverlay();
        _ = RunCartoInitAsync();
    }

    private void ShowMapLoadingOverlay()
    {
        if (MapLoadingOverlay != null)
            MapLoadingOverlay.Visibility = Visibility.Visible;
    }

    private void HideMapLoadingOverlay()
    {
        if (MapLoadingOverlay != null)
            MapLoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private async Task RunCartoInitAsync()
    {
        try
        {
            var (pixelW, pixelH) = await Task.Run(GetOrLoadWorldMapPixels).ConfigureAwait(false);

            await Dispatcher.InvokeAsync(() =>
            {
                if (!IsVisible || Vm == null)
                    return;

                ApplyWorldMapToUi(pixelW, pixelH);
                HideMapLoadingOverlay();
            }, DispatcherPriority.Background);

            await Vm!.EnsureCharacterDataLoadedAsync().ConfigureAwait(true);

            await Dispatcher.InvokeAsync(FinishCartoInitOnUi, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Carto init: {ex}");
            _cartoInit.Reset();
            await Dispatcher.InvokeAsync(HideMapLoadingOverlay);
        }
    }

    private (int W, int H) GetOrLoadWorldMapPixels()
    {
        if (_cachedWorldMap is { PixelWidth: > 0 } c)
            return (c.PixelWidth, c.PixelHeight);

        CartoMapPreloader.EnsureLoaded();
        if (CartoMapPreloader.GetBitmap() is { } preloaded)
        {
            _cachedWorldMap = preloaded;
            return (preloaded.PixelWidth, preloaded.PixelHeight);
        }

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = CartoMapAssets.PackUri;
        bmp.EndInit();
        bmp.Freeze();
        _cachedWorldMap = bmp;
        return (bmp.PixelWidth, bmp.PixelHeight);
    }

    private void EnsureMapImageOnUi()
    {
        if (_cachedWorldMap == null || MapImage == null)
            return;

        if (!ReferenceEquals(MapImage.Source, _cachedWorldMap))
        {
            MapImage.Source = _cachedWorldMap;
            MapImage.Width = _worldPixelW > 0 ? _worldPixelW : _cachedWorldMap.PixelWidth;
            MapImage.Height = _worldPixelH > 0 ? _worldPixelH : _cachedWorldMap.PixelHeight;
        }
    }

    private void ApplyWorldMapToUi(int pixelW, int pixelH)
    {
        _worldPixelW = pixelW;
        _worldPixelH = pixelH;

        EnsureMapScrollTree();

        if (_cachedWorldMap != null && !ReferenceEquals(MapImage.Source, _cachedWorldMap))
            MapImage.Source = _cachedWorldMap;

        if (CapitalsDock != null)
            CapitalsDock.Visibility = Visibility.Collapsed;

        MapImage.Width = pixelW;
        MapImage.Height = pixelH;
        MapContainer.Width = pixelW;
        MapContainer.Height = pixelH;

        if (Vm != null && !_migrated)
        {
            if (Vm.NeedsMigration)
                Vm.MigrateCoordinates(pixelW, pixelH);
            _migrated = true;
        }

        TryApplyInitialMapFit();
    }

    private void EnsureMapFitLayoutHook()
    {
        if (_mapFitLayoutHooked || MapBorder == null)
            return;

        _mapFitLayoutHooked = true;
        MapBorder.SizeChanged += MapBorder_LayoutForInitialFit;
    }

    private void MapBorder_LayoutForInitialFit(object sender, SizeChangedEventArgs e)
    {
        if (!_pendingInitialMapFit || e.NewSize.Height < 8 || e.NewSize.Width < 8)
            return;

        TryApplyInitialMapFit();
        if (Vm?.IsZoneEditMode == true)
            RedrawZoneEditor();
    }

    /// <summary>Zoom pour afficher la carte entière dans la zone visible (une fois la taille connue).</summary>
    private void TryApplyInitialMapFit()
    {
        if (Vm == null || MapBorder == null)
            return;

        var mapH = _worldPixelH > 0 ? _worldPixelH : 768;
        var mapW = _worldPixelW > 0 ? _worldPixelW : 1024;
        var viewportH = MapBorder.ActualHeight;
        var viewportW = MapBorder.ActualWidth;

        if (viewportH < 8 || viewportW < 8)
        {
            _pendingInitialMapFit = true;
            EnsureMapFitLayoutHook();
            return;
        }

        _pendingInitialMapFit = false;

        var fitZoom = Math.Min(viewportH / mapH, viewportW / mapW);
        if (fitZoom <= 0 || double.IsNaN(fitZoom) || double.IsInfinity(fitZoom))
            fitZoom = 0.45;

        Vm.MapZoom = Math.Clamp(fitZoom, CartoViewModel.MinMapZoom, CartoViewModel.MaxMapZoom);
        Vm.MapOffsetX = 0;
        Vm.MapOffsetY = 0;
        MapScroll?.ScrollToHorizontalOffset(0);
        MapScroll?.ScrollToVerticalOffset(0);
    }

    private void FinishCartoInitOnUi()
    {
        if (Vm == null || !IsVisible)
        {
            _cartoInit.Reset();
            return;
        }

        TryApplyInitialMapFit();

        if (Vm.CharactersLoaded && Vm.Characters.Count > 0)
            RefreshMapCharactersWhenReady();
        RedrawTimerMarkers();
        PreloadCharacterRoster();
        ApplyRightPanelLayout();

        HideMapLoadingOverlay();

        _cartoInit.Complete();
        _cartoUiLive = true;
        if (Vm.IsZoneEditMode)
            RedrawZoneEditor();
    }

    /// <summary>Construit le roster en mémoire (volet fermé) pour ouverture instantanée.</summary>
    private void PreloadCharacterRoster()
    {
        if (!Vm!.CharactersLoaded)
            return;

        RebuildCharacterRoster();
    }

    private void TryApplyCharacterUiWhenReady()
    {
        if (Vm == null || !Vm.CharactersLoaded)
            return;

        RebuildCharacterRoster();
        if (_cartoInit.IsComplete)
            RequestMapMarkersRefresh();
    }

    /// <summary>Carte prête (splash ou retour onglet) : placement WowSync (une fois) + marqueurs.</summary>
    private void RefreshMapCharactersWhenReady()
    {
        if (Vm == null || !Vm.CharactersLoaded)
            return;

        if (!Vm.MapPositionsReady)
            Vm.EnsureCharactersVisibleOnMap();
        else
            Vm.RefreshMapDisplayPlacement();
        RequestMapMarkersRefresh();
    }
}
