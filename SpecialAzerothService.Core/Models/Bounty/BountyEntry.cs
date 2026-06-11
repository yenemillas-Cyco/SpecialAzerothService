using System.Text.Json.Serialization;

namespace SpecialAzerothService.Core.Models.Bounty;

public sealed class BountyEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TargetName { get; set; } = string.Empty;
    public string? AltName { get; set; }
    public string TargetClass { get; set; } = string.Empty;
    public string TargetRace { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public List<BountyContributor> Contributors { get; set; } = [];

    [JsonIgnore]
    public bool IsSelectedForExport { get; set; } = true;

    public int TotalGold => Contributors.Sum(c => c.GoldAmount);

    public string DisplayTotal => $"{TotalGold}po";

    [JsonIgnore] public BountyValueTier ValueTier => BountyTierHelper.GetTier(TotalGold);

    [JsonIgnore] public string TierLabel => BountyTierHelper.GetLabel(TotalGold);

    [JsonIgnore] public string TierForegroundHex => BountyTierHelper.GetForegroundHex(TotalGold);

    [JsonIgnore] public string TierBorderHex => BountyTierHelper.GetBorderHex(TotalGold);

    [JsonIgnore] public string TierGlowHex => BountyTierHelper.GetGlowHex(TotalGold);

    public string ContributorNames => string.Join(", ", Contributors.Select(c => c.Name).Distinct());

    public string ContributorDetails => string.Join(", ", Contributors.Select(c => $"{c.Name} ({c.GoldAmount}po)"));
}
