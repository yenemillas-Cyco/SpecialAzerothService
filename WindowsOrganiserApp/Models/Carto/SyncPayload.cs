namespace WindowsOrganiserApp.Models.Carto;

public sealed class SyncPayload
{
    public List<WowAccount> Accounts { get; set; } = [];
    public List<WowCharacter> Characters { get; set; } = [];
}
