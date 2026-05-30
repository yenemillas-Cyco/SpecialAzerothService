using SpecialAzerothService.Core.Models.Bounty;

namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>Obsolète — conservé pour compatibilité JSON ; utiliser <see cref="FriendSyncPayload"/>.</summary>
public sealed class SyncPayload
{
    public List<WowAccount> Accounts { get; set; } = [];
    public List<WowCharacter> Characters { get; set; } = [];
}

public sealed class BountySyncPayload
{
    public List<BountyEntry> Bounties { get; set; } = [];
    public string Rules { get; set; } = string.Empty;
}
