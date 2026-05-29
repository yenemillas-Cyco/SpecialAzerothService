namespace SpecialAzerothService.Core.Models.Carto;

public enum CartoUnplacedReason
{
    NoSync,
    CoordsZero,
    ZoneNotCalibrated,
    InInstance
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
    public bool IsOnStack { get; init; }

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
        CartoUnplacedReason.InInstance => "Instance — repère donjon ?",
        _ => "Non placé"
    };

    public string SummaryLine =>
        $"{Name} · {AccountName} · {ZoneDisplay} (map {MapId}) · {ReasonDisplay}";
}
