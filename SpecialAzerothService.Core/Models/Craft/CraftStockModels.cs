namespace SpecialAzerothService.Core.Models.Craft;

public sealed class CraftStockOwnerInfo
{
    public required string UserId { get; init; }
    public required string OwnerName { get; init; }
}

public sealed class CraftStockCharacterHold
{
    public string CharacterName { get; init; } = "";
    public string AccountName { get; init; } = "";
    public int Count { get; init; }
    public string Label => $"{CharacterName} ({Count})";
}

public sealed class CraftCharacterStock
{
    public string CharacterName { get; init; } = "";
    public string AccountName { get; init; } = "";
    public Dictionary<int, int> Inventory { get; init; } = [];
    public Dictionary<int, int> Bank { get; init; } = [];

    public int GetCount(int itemId, CraftPickupSource source) =>
        source == CraftPickupSource.Inventory
            ? Inventory.GetValueOrDefault(itemId)
            : Bank.GetValueOrDefault(itemId);
}

public enum CraftPickupSource
{
    Inventory,
    Bank
}

public sealed class CraftPickupLine
{
    public string CharacterName { get; init; } = "";
    public string AccountName { get; init; } = "";
    public int ItemId { get; init; }
    public int Quantity { get; init; }
    public CraftPickupSource Source { get; init; }
}

public sealed class CraftPlanningResult
{
    public required IReadOnlyDictionary<int, MaterialRequirement> Materials { get; init; }
    public required IReadOnlyList<CraftPickupLine> Pickups { get; init; }
}

public sealed class CraftPlanningOptions
{
    /// <summary>
    /// Vrai = on force la fabrication des objets de sortie (on n'utilise pas le stock d'objet fini).
    /// </summary>
    public bool ForceCraftOutputs { get; init; }

    /// <summary>
    /// Vrai = on peut piocher les composants dans les sacs/banques des persos (mules).
    /// </summary>
    public bool UseMuleStockForComponents { get; init; } = true;
}

public sealed class CraftStockSnapshot
{
    public Dictionary<int, int> TotalByItemId { get; init; } = [];
    public Dictionary<int, List<CraftStockCharacterHold>> ByItemId { get; init; } = [];
    public List<CraftCharacterStock> Characters { get; init; } = [];

    public int GetTotal(int itemId) =>
        TotalByItemId.GetValueOrDefault(itemId);

    public IReadOnlyList<CraftStockCharacterHold> GetBreakdown(int itemId) =>
        ByItemId.TryGetValue(itemId, out var list) ? list : [];
}
