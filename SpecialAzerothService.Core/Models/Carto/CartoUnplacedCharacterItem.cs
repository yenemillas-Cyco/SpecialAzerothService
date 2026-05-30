namespace SpecialAzerothService.Core.Models.Carto;

public enum CartoUnplacedReason
{
    NoSync,
    CoordsZero,
    ZoneNotCalibrated,
    InInstance,
    SuspiciousContinentCoords,
    SuspiciousOceanProjection,
    SuspiciousOutsideZone
}

public sealed class CartoUnplacedCharacterItem
{
    public required string SyncKey { get; init; }
    public required string Name { get; init; }
    public string AccountName { get; init; } = "";
    public string Zone { get; init; } = "";
    public string SubZone { get; init; } = "";
    public int MapId { get; init; }
    public string CoordsDisplay { get; init; } = "";
    public CartoUnplacedReason Reason { get; init; }
    public string? WarningDetail { get; init; }
    public bool IsOnStack { get; init; }

    public bool IsSuspicious => Reason is CartoUnplacedReason.SuspiciousContinentCoords
        or CartoUnplacedReason.SuspiciousOceanProjection
        or CartoUnplacedReason.SuspiciousOutsideZone;

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
        CartoUnplacedReason.ZoneNotCalibrated => "Zone non calibrée",
        CartoUnplacedReason.InInstance => "Instance — repère lieu-dit ?",
        CartoUnplacedReason.SuspiciousContinentCoords => "Coords continent (souvent en mer)",
        CartoUnplacedReason.SuspiciousOceanProjection => "Projeté sur carte continent",
        CartoUnplacedReason.SuspiciousOutsideZone => "Hors zone calibrée",
        _ => "Non placé"
    };

    public string SummaryLine =>
        string.IsNullOrWhiteSpace(WarningDetail)
            ? $"{Name} · {AccountName} · {ZoneDisplay} (map {MapId}) · {ReasonDisplay}"
            : $"{Name} · {AccountName} · {ZoneDisplay} (map {MapId}) · {ReasonDisplay} — {WarningDetail}";
}
