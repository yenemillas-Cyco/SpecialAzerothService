using CommunityToolkit.Mvvm.ComponentModel;

namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>Repère lieu-dit / instance sur WowMap.png (coords 0–1).</summary>
public partial class CartoDungeonMarker : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Key { get; init; } = "";

    public string NameFr { get; init; } = "";

    [ObservableProperty]
    private double _mapX;

    [ObservableProperty]
    private double _mapY;

    public string DisplayName => string.IsNullOrWhiteSpace(NameFr) ? Key : NameFr;

    public string PositionHint =>
        MapX > 0 || MapY > 0
            ? $"{MapX * 100:F1} % · {MapY * 100:F1} %"
            : "Non placé";
}
