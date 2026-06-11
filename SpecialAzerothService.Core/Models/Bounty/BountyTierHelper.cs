namespace SpecialAzerothService.Core.Models.Bounty;

public enum BountyValueTier
{
    Grey,
    White,
    Green,
    Blue,
    Purple,
    Orange,
}

/// <summary>Barème visuel des primes selon le montant total en or.</summary>
public static class BountyTierHelper
{
    public static BountyValueTier GetTier(int totalGold) => totalGold switch
    {
        >= 1000 => BountyValueTier.Orange,
        >= 300 => BountyValueTier.Purple,
        >= 200 => BountyValueTier.Blue,
        >= 100 => BountyValueTier.Green,
        >= 50 => BountyValueTier.White,
        _ => BountyValueTier.Grey,
    };

    public static string GetLabel(BountyValueTier tier) => tier switch
    {
        BountyValueTier.White => "Blanc",
        BountyValueTier.Green => "Vert",
        BountyValueTier.Blue => "Bleu",
        BountyValueTier.Purple => "Violet",
        BountyValueTier.Orange => "Orange",
        _ => "Gris",
    };

    public static string GetLabel(int totalGold) => GetLabel(GetTier(totalGold));

    public static string GetForegroundHex(BountyValueTier tier) => tier switch
    {
        BountyValueTier.White => "#FFFFFF",
        BountyValueTier.Green => "#1EFF00",
        BountyValueTier.Blue => "#0070DD",
        BountyValueTier.Purple => "#A335EE",
        BountyValueTier.Orange => "#FF8000",
        _ => "#9D9D9D",
    };

    public static string GetForegroundHex(int totalGold) => GetForegroundHex(GetTier(totalGold));

    public static string GetBorderHex(BountyValueTier tier) => tier switch
    {
        BountyValueTier.White => "#80FFFFFF",
        BountyValueTier.Green => "#801EFF00",
        BountyValueTier.Blue => "#800070DD",
        BountyValueTier.Purple => "#80A335EE",
        BountyValueTier.Orange => "#80FF8000",
        _ => "#609D9D9D",
    };

    public static string GetBorderHex(int totalGold) => GetBorderHex(GetTier(totalGold));

    public static string GetGlowHex(BountyValueTier tier) => tier switch
    {
        BountyValueTier.White => "#18FFFFFF",
        BountyValueTier.Green => "#221EFF00",
        BountyValueTier.Blue => "#220070DD",
        BountyValueTier.Purple => "#22A335EE",
        BountyValueTier.Orange => "#28FF8000",
        _ => "#109D9D9D",
    };

    public static string GetGlowHex(int totalGold) => GetGlowHex(GetTier(totalGold));
}
