namespace WindowsOrganiserApp.Models.Carto;

public enum CharacterStatus
{
    Main,
    Reroll,
    Banque,
    TpBoy
}

public static class CharacterStatusExtensions
{
    public static string DisplayName(this CharacterStatus s) => s switch
    {
        CharacterStatus.Main => "Main",
        CharacterStatus.Reroll => "Reroll",
        CharacterStatus.Banque => "Banque",
        CharacterStatus.TpBoy => "TP Boy",
        _ => s.ToString()
    };
}

public sealed class WowCharacter
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public WowClass Class { get; set; }
    public int Level { get; set; } = 1;
    public string? AccountId { get; set; }
    public CharacterStatus Status { get; set; } = CharacterStatus.Reroll;

    public double MapX { get; set; }
    public double MapY { get; set; }

    public List<ProfessionInfo> Professions { get; set; } = [];
    public List<CooldownEntry> Cooldowns { get; set; } = [];
    public List<QuestItemEntry> QuestItems { get; set; } = [];
    public string Note { get; set; } = string.Empty;
    public int ShardCount { get; set; }
    public bool IsHidden { get; set; }
    public bool IsLocked { get; set; }
    public bool ExcludeFromSync { get; set; }
    public bool IsExternal { get; set; }
    public string? ExternalSource { get; set; }
}
