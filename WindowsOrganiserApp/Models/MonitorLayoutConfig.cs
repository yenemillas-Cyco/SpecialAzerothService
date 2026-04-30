using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Models;

public partial class MonitorLayoutConfig : ObservableObject
{
    public MonitorInfo Monitor { get; init; } = null!;

    public ObservableCollection<PreviewRect> PreviewRects { get; } = [];

    [ObservableProperty]
    private LayoutMode _mode = LayoutMode.Main;

    [ObservableProperty]
    private MainSize _size = MainSize.Moyen;

    [ObservableProperty]
    private MainPosition _position = MainPosition.TopRight;

    [ObservableProperty]
    private bool _hasLateral = true;

    [ObservableProperty]
    private bool _hasBandeau;

    [ObservableProperty]
    private SplitOrientation _splitOrientation = SplitOrientation.Horizontal;

    partial void OnHasLateralChanged(bool value)
    {
        if (!value && !HasBandeau) HasBandeau = true;
    }

    partial void OnHasBandeauChanged(bool value)
    {
        if (!value && !HasLateral) HasLateral = true;
    }
}
