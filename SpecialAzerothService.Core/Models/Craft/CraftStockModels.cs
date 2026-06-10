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
    public bool IsBound { get; init; }
    public string Label => IsBound
        ? $"{CharacterName} ({Count}, lié)"
        : $"{CharacterName} ({Count})";
}

public sealed class CraftCharacterStock
{
    public string CharacterName { get; init; } = "";
    public string AccountName { get; init; } = "";
    public Dictionary<int, int> Inventory { get; init; } = [];
    public Dictionary<int, int> Bank { get; init; } = [];
    public Dictionary<int, int> Mail { get; init; } = [];
    public Dictionary<int, int> BoundInventory { get; init; } = [];
    public Dictionary<int, int> BoundBank { get; init; } = [];
    public Dictionary<int, int> BoundMail { get; init; } = [];
    public long GoldCopper { get; set; }

    public int GetCount(int itemId, CraftPickupSource source) => source switch
    {
        CraftPickupSource.Inventory => Inventory.GetValueOrDefault(itemId),
        CraftPickupSource.Bank => Bank.GetValueOrDefault(itemId),
        CraftPickupSource.Mail => Mail.GetValueOrDefault(itemId),
        _ => 0
    };

    public int GetBound(int itemId) =>
        BoundInventory.GetValueOrDefault(itemId)
        + BoundBank.GetValueOrDefault(itemId)
        + BoundMail.GetValueOrDefault(itemId);

    /// <summary>Sac + banque + courrier (liés et non liés).</summary>
    public int GetTotalOnCharacter(int itemId) =>
        Inventory.GetValueOrDefault(itemId)
        + Bank.GetValueOrDefault(itemId)
        + Mail.GetValueOrDefault(itemId)
        + BoundInventory.GetValueOrDefault(itemId)
        + BoundBank.GetValueOrDefault(itemId)
        + BoundMail.GetValueOrDefault(itemId);

    public ArcanumCharacterStock ToArcanumStock() => new()
    {
        CharacterName = CharacterName,
        AccountName = AccountName,
        TransferableInventory = new Dictionary<int, int>(Inventory),
        TransferableBank = new Dictionary<int, int>(Bank),
        TransferableMail = new Dictionary<int, int>(Mail),
        BoundInventory = new Dictionary<int, int>(BoundInventory),
        BoundBank = new Dictionary<int, int>(BoundBank),
        BoundMail = new Dictionary<int, int>(BoundMail),
        GoldCopper = GoldCopper
    };
}

public enum CraftPickupSource
{
    Inventory,
    Bank,
    Mail
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

    public CraftCharacterStock? FindCharacter(string accountName, string characterName) =>
        Characters.FirstOrDefault(c =>
            c.CharacterName.Equals(characterName, StringComparison.OrdinalIgnoreCase)
            && c.AccountName.Equals(accountName, StringComparison.OrdinalIgnoreCase));
}
