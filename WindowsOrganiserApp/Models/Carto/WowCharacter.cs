namespace WindowsOrganiserApp.Models.Carto;

public sealed class WowCharacter
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public WowClass Class { get; set; }
    public int Level { get; set; } = 1;
    public string? AccountId { get; set; }

    public double MapX { get; set; }
    public double MapY { get; set; }

    public List<ProfessionInfo> Professions { get; set; } = [];
    public List<CooldownEntry> Cooldowns { get; set; } = [];
    public List<QuestItemEntry> QuestItems { get; set; } = [];
    public string Note { get; set; } = string.Empty;
    public int ShardCount { get; set; }
}
