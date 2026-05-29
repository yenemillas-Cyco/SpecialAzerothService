using WindowsOrganiserApp.Models.Bounty;

namespace WindowsOrganiserApp.Models.Carto;

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
