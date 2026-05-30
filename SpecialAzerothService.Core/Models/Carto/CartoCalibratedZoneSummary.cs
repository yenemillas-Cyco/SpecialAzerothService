namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>Zone disposant d'un rectangle sur WowMap.png.</summary>
public sealed class CartoCalibratedZoneSummary
{
    public int MapId { get; init; }
    public required string DisplayName { get; init; }
    public bool IsUserPlaced { get; init; }
    public bool IsBuiltIn { get; init; }
    public bool IsEmbedded { get; init; }

    public string SourceDisplay => IsUserPlaced
        ? "Votre calibration"
        : IsBuiltIn
            ? "Intégré"
            : IsEmbedded
                ? "Embarqué"
                : "Calibration";
}
