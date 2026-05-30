using System.Windows.Threading;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.Views;

public partial class CartoView
{
    /// <summary>
    /// Ouverture de l'onglet Carto : placement des persos + dessin des marqueurs (sans clic sur un personnage).
    /// </summary>
    public void ActivateCartoTab()
    {
        if (Vm == null)
            return;

        _cartoUiLive = true;
        _ = ActivateCartoTabAsync();
    }

    private async Task ActivateCartoTabAsync()
    {
        if (Vm == null)
            return;

        try
        {
            await Vm.EnsureCharacterDataLoadedAsync().ConfigureAwait(true);

            await Dispatcher.InvokeAsync(() =>
            {
                if (Vm == null)
                    return;

                if (Vm.CharactersLoaded)
                    Vm.PrepareMapDisplay();

                if (!_cartoInit.IsComplete)
                {
                    if (_cartoInit.Phase == CartoInitPhase.NotStarted)
                        StartCartoSession(ignoreVisibility: true);
                    else
                        ResumeDeferredCartoInitIfNeeded();
                    return;
                }

                EnsureMapImageOnUi();
                TryApplyInitialMapFit();
                RefreshMapCharactersWhenReady();
                PaintMapMarkers(force: true);
                RedrawTimerMarkers();
            }, DispatcherPriority.Loaded);

            await Dispatcher.InvokeAsync(
                () => PaintMapMarkers(force: true),
                DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ActivateCartoTab: {ex}");
        }
    }
}
