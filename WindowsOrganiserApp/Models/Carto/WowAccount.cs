namespace WindowsOrganiserApp.Models.Carto;

public sealed class WowAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public override string ToString() => Name;
}
