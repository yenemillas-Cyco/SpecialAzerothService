using CommunityToolkit.Mvvm.ComponentModel;

namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>Rectangle de zone sur WowMap.png (coords 0–1).</summary>
public partial class CartoZoneRectItem : ObservableObject
{
    public int MapId { get; init; }
    public string NameFr { get; init; } = "";
    public string NameEn { get; init; } = "";
    /// <summary>Libellé affiché (capitales en anglais, zones en français).</summary>
    public string DisplayName { get; init; } = "";

    [ObservableProperty]
    private double _left;

    [ObservableProperty]
    private double _top;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;
}
