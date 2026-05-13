namespace WindowsOrganiserApp.Models.Bounty;

public sealed class BountyContributor
{
    public string Name { get; set; } = string.Empty;
    public int GoldAmount { get; set; }
    public int JewelAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
}
