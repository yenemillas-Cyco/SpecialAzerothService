namespace SpecialAzerothService.Core.Models.Craft;

public sealed class CraftVendorPurchaseInfo
{
    public static CraftVendorPurchaseInfo None { get; } = new(false, 0);

    public bool IsInfiniteStock { get; }
    public int UnitPriceCopper { get; }

    public CraftVendorPurchaseInfo(bool isInfiniteStock, int unitPriceCopper)
    {
        IsInfiniteStock = isInfiniteStock;
        UnitPriceCopper = Math.Max(0, unitPriceCopper);
    }
}
