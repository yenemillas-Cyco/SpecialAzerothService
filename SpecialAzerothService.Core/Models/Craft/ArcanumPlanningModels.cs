namespace SpecialAzerothService.Core.Models.Craft;

/// <summary>Une quête arcanum ajoutée à la liste craft (ex. Constitution ×2).</summary>
public sealed class ArcanumQuestDemand
{
    public required int ResultItemId { get; init; }
    public int Quantity { get; init; } = 1;
    public string? TargetCharacterName { get; init; }
}

/// <summary>Stock par personnage (sac + banque + courrier, liés séparés).</summary>
public sealed class ArcanumCharacterStock
{
    public required string CharacterName { get; init; }
    public required string AccountName { get; init; }
    public Dictionary<int, int> TransferableInventory { get; init; } = [];
    public Dictionary<int, int> TransferableBank { get; init; } = [];
    public Dictionary<int, int> TransferableMail { get; init; } = [];
    public Dictionary<int, int> BoundInventory { get; init; } = [];
    public Dictionary<int, int> BoundBank { get; init; } = [];
    public Dictionary<int, int> BoundMail { get; init; } = [];
    public long GoldCopper { get; init; }

    public int GetTransferable(int itemId) =>
        TransferableInventory.GetValueOrDefault(itemId)
        + TransferableBank.GetValueOrDefault(itemId)
        + TransferableMail.GetValueOrDefault(itemId);

    public int GetBound(int itemId) =>
        BoundInventory.GetValueOrDefault(itemId)
        + BoundBank.GetValueOrDefault(itemId)
        + BoundMail.GetValueOrDefault(itemId);

    public int GetTotalOnCharacter(int itemId) => GetTransferable(itemId) + GetBound(itemId);
}

/// <summary>Un composant lié requis — persos qui le possèdent (stock lié).</summary>
public sealed class BoundMaterialNeed
{
    public int ItemId { get; init; }
    public string? DisplayNameFr { get; init; }
    public int RequiredCount { get; init; }
    public IReadOnlyList<BoundMaterialCharacterHold> Characters { get; init; } = [];
}

/// <summary>Perso détenant un composant lié pour une quête.</summary>
public sealed class BoundMaterialCharacterHold
{
    public required string CharacterName { get; init; }
    public required string AccountName { get; init; }
    public int BoundCount { get; init; }
    public int TotalOnCharacter { get; init; }
    public long GoldCopper { get; init; }
    public int PickupQuantity => Math.Min(RequiredCount, BoundCount);
    public int RequiredCount { get; init; }
}

public sealed class ArcanumPlanningResult
{
    public IReadOnlyList<ArcanumQuestAssignment> Assignments { get; init; } = [];
    public string? SummaryNote { get; init; }
}

public sealed class ArcanumQuestAssignment
{
    public required ArcanumQuestDemand Demand { get; init; }
    public int QuestGoldCostCopper { get; init; }
    public bool HasBoundMaterials { get; init; }
    public IReadOnlyList<BoundMaterialNeed> BoundNeeds { get; init; } = [];
    public string? ErrorMessage { get; init; }
}
