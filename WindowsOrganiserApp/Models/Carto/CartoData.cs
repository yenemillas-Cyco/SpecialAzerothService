namespace WindowsOrganiserApp.Models.Carto;

public sealed class CartoData
{
    public List<WowAccount> Accounts { get; set; } = [];
    public List<WowCharacter> Characters { get; set; } = [];
    public List<MapTimer> Timers { get; set; } = [];
}
