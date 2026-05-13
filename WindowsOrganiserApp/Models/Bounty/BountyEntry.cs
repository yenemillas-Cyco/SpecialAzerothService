namespace WindowsOrganiserApp.Models.Bounty;

public sealed class BountyEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TargetName { get; set; } = string.Empty;
    public string? AltName { get; set; }
    public string TargetClass { get; set; } = string.Empty;
    public string TargetRace { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string? KilledBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<BountyContributor> Contributors { get; set; } = [];

    public int TotalGold => Contributors.Sum(c => c.GoldAmount);
    public int TotalJewels => Contributors.Sum(c => c.JewelAmount);

    public string DisplayTotal
    {
        get
        {
            var parts = new List<string>();
            if (TotalGold > 0) parts.Add($"{TotalGold}po");
            if (TotalJewels > 0) parts.Add($"{TotalJewels} bijoux zg");
            return parts.Count > 0 ? string.Join(" ou ", parts) : "0po";
        }
    }

    public string ContributorNames => string.Join(" et ", Contributors.Select(c => c.Name).Distinct());
}
