namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>Données partagées entre amis mutuels (comptes + personnages locaux).</summary>
public sealed class FriendSyncPayload
{
    public long Revision { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public List<WowAccount> Accounts { get; set; } = [];
    public List<WowCharacter> Characters { get; set; } = [];
}

/// <summary>Flux public : TP Boy (position + fragments), sans amitié.</summary>
public sealed class TpBoyPublicPayload
{
    public long Revision { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public string OwnerGuid { get; set; } = "";
    public List<TpBoyPublicEntry> Entries { get; set; } = [];
}

public sealed class TpBoyPublicEntry
{
    public string SyncKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string AccountDisplayName { get; set; } = "";
    public WowClass Class { get; set; }
    public int Level { get; set; }
    public double MapX { get; set; }
    public double MapY { get; set; }
    public string? TerrainZoneSlug { get; set; }
    public double? TerrainZoneX { get; set; }
    public double? TerrainZoneY { get; set; }
    public bool IsPlacedOnMap { get; set; }
    public int ShardCount { get; set; }
    public string LastUpdate { get; set; } = "";
}
