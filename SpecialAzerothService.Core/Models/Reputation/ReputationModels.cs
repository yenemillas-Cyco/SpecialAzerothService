namespace SpecialAzerothService.Core.Models.Reputation;

public enum ReputationTurnInMethod
{
    Bijoux,
    Coins,
    TurnIn,
}

public sealed class ReputationTurnInItem
{
    public int ItemId { get; init; }
    public string NameFr { get; init; } = "";
}

/// <summary>Quantité fixe d'un objet requis par remise (Confrérie du Thorium, etc.).</summary>
public sealed class ReputationTurnInRequirement
{
    public int ItemId { get; init; }
    public string NameFr { get; init; } = "";
    public int QuantityPerTurnIn { get; init; } = 1;
}

public sealed class ReputationTurnInRoute
{
    public string RouteId { get; init; } = "";
    public ReputationTurnInMethod Method { get; init; }
    public string LabelFr { get; init; } = "";
    /// <summary>Libellé court pour le choix de variante (ex. « Barres de fer »).</summary>
    public string VariantLabelFr { get; init; } = "";
    public string DescriptionFr { get; init; } = "";
    public int BaseReputation { get; init; }
    public int HonorTokenReputation { get; init; }
    public int ItemsPerTurnIn { get; init; } = 1;
    public int HonorTokenItemId { get; init; }
    public string HonorTokenNameFr { get; init; } = "";
    public string ItemUnitLabelFr { get; init; } = "objets";
    public IReadOnlyList<ReputationTurnInItem> AcceptedItems { get; init; } = [];
    public IReadOnlyList<ReputationTurnInRequirement> Requirements { get; init; } = [];
    /// <summary>Objets obtenus par remise (ex. insignes Aube d'argent).</summary>
    public IReadOnlyList<ReputationTurnInItem> TurnInRewards { get; init; } = [];

    public bool UsesFixedRequirements => Requirements.Count > 0;

    public string DisplayVariantLabel =>
        !string.IsNullOrWhiteSpace(VariantLabelFr) ? VariantLabelFr : LabelFr;
}

/// <summary>Palier de réputation (ex. Neutre → Amical) avec variantes de remise.</summary>
public sealed class ReputationFarmTier
{
    public string TierId { get; init; } = "";
    public string LabelFr { get; init; } = "";
    public int ReputationNeeded { get; init; }
    public string DescriptionFr { get; init; } = "";
    public string? DefaultVariantRouteId { get; init; }
    public IReadOnlyList<string> VariantRouteIds { get; init; } = [];
}

public sealed class ReputationFarmDefinition
{
    public string Id { get; init; } = "";
    public string FactionNameFr { get; init; } = "";
    public string LocationFr { get; init; } = "";
    public string NpcNameFr { get; init; } = "";
    public string NotesFr { get; init; } = "";
    public IReadOnlyList<ReputationTurnInRoute> Routes { get; init; } = [];
    public IReadOnlyList<ReputationFarmTier> Tiers { get; init; } = [];

    public bool IsImplemented => Routes.Count > 0;
    public bool UsesTierSelection => Tiers.Count > 0;
}

public sealed class ReputationItemNeed
{
    public int ItemId { get; init; }
    public string NameFr { get; init; } = "";
    public int QuantityNeeded { get; init; }
}

public sealed class ReputationCalculationResult
{
    public int TargetReputation { get; init; }
    public int TurnInCount { get; init; }
    public int ItemsNeeded { get; init; }
    public int HonorTokensNeeded { get; init; }
    public int ReputationGained { get; init; }
    public int ReputationPerTurnIn { get; init; }
    public ReputationTurnInMethod Method { get; init; }
    public bool UsesHonorToken { get; init; }
    public string SummaryFr { get; init; } = "";
    public IReadOnlyList<ReputationItemNeed> ItemBreakdown { get; init; } = [];
}
