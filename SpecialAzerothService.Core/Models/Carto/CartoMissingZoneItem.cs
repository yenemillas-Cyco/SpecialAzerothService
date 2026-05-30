namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>Zone WowSync utilisée par au moins un personnage mais non plaçable sur la carte monde.</summary>
public sealed class CartoMissingZoneItem
{
    public required string ZoneKey { get; init; }
    public int EffectiveMapId { get; init; }
    public int RawMapId { get; init; }
    public string Zone { get; init; } = "";
    public string SubZone { get; init; } = "";
    public string? CatalogDisplayName { get; init; }
    /// <summary>Un rectangle existe pour ce mapId (built-in, embarqué ou utilisateur).</summary>
    public bool HasMapRectangle { get; init; }
    public bool IsUserCalibrated { get; init; }
    public CartoUnplacedReason Reason { get; init; }
    public int CharacterCount { get; init; }
    public string CharacterNames { get; init; } = "";

    public string ZoneDisplay =>
        string.IsNullOrWhiteSpace(SubZone)
            ? Zone
            : string.IsNullOrWhiteSpace(Zone)
                ? SubZone
                : $"{Zone} — {SubZone}";

    public string ReasonDisplay => Reason switch
    {
        CartoUnplacedReason.NoSync => "WowSync introuvable",
        CartoUnplacedReason.CoordsZero => "Coords à 0 (addon)",
        CartoUnplacedReason.ZoneNotCalibrated => "Rectangle de zone manquant",
        CartoUnplacedReason.InInstance => "Instance — repère donjon ?",
        _ => "Non plaçable"
    };

    public string SummaryLine =>
        $"{ZoneDisplay} (map eff. {EffectiveMapId}) · {CharacterCount} perso(s) · {ReasonDisplay}";
}
