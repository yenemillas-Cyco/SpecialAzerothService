using SpecialAzerothService.Core.Models.Reputation;

namespace SpecialAzerothService.Core.Services;

public interface IReputationCalculatorService
{
    ReputationCalculationResult? Calculate(
        ReputationTurnInRoute route,
        int targetReputation,
        bool useHonorToken);

    ReputationCalculationResult? Calculate(
        ReputationFarmDefinition farm,
        ReputationTurnInMethod method,
        int targetReputation,
        bool useHonorToken);

    int CalculateReputationFromItemCounts(
        ReputationTurnInRoute route,
        IReadOnlyDictionary<int, int> itemCounts,
        bool useHonorToken);

    int CalculateReputationFromItemCount(
        ReputationFarmDefinition farm,
        ReputationTurnInMethod method,
        int itemCount,
        bool useHonorToken);
}

public sealed class ReputationCalculatorService : IReputationCalculatorService
{
    public ReputationCalculationResult? Calculate(
        ReputationTurnInRoute route,
        int targetReputation,
        bool useHonorToken)
    {
        if (targetReputation <= 0) return null;

        useHonorToken = useHonorToken && route.HonorTokenReputation > 0;
        var repPerTurnIn = ReputationRouteHelper.GetReputationPerTurnIn(route, useHonorToken);
        if (repPerTurnIn <= 0) return null;

        var turnIns = (targetReputation + repPerTurnIn - 1) / repPerTurnIn;
        var breakdown = ReputationRouteHelper.BuildItemBreakdown(route, turnIns);
        var tokensNeeded = useHonorToken ? turnIns : 0;
        var gained = turnIns * repPerTurnIn;
        var primaryNeeded = breakdown.FirstOrDefault()?.QuantityNeeded ?? turnIns * route.ItemsPerTurnIn;

        return new ReputationCalculationResult
        {
            TargetReputation = targetReputation,
            TurnInCount = turnIns,
            ItemsNeeded = primaryNeeded,
            HonorTokensNeeded = tokensNeeded,
            ReputationGained = gained,
            ReputationPerTurnIn = repPerTurnIn,
            Method = route.Method,
            UsesHonorToken = useHonorToken,
            ItemBreakdown = breakdown,
            SummaryFr = BuildSummary(route, turnIns, gained, targetReputation, repPerTurnIn, useHonorToken, breakdown),
        };
    }

    public ReputationCalculationResult? Calculate(
        ReputationFarmDefinition farm,
        ReputationTurnInMethod method,
        int targetReputation,
        bool useHonorToken)
    {
        var route = ReputationTurnInCatalog.TryGetRoute(farm, method);
        return route == null ? null : Calculate(route, targetReputation, useHonorToken);
    }

    public int CalculateReputationFromItemCounts(
        ReputationTurnInRoute route,
        IReadOnlyDictionary<int, int> itemCounts,
        bool useHonorToken)
    {
        var turnIns = ReputationRouteHelper.CountTurnInsFromPool(route, itemCounts);
        if (turnIns <= 0) return 0;

        useHonorToken = useHonorToken && route.HonorTokenReputation > 0;
        var repPerTurnIn = ReputationRouteHelper.GetReputationPerTurnIn(route, useHonorToken);
        return turnIns * repPerTurnIn;
    }

    public int CalculateReputationFromItemCount(
        ReputationFarmDefinition farm,
        ReputationTurnInMethod method,
        int itemCount,
        bool useHonorToken)
    {
        var route = ReputationTurnInCatalog.TryGetRoute(farm, method);
        if (route == null || itemCount <= 0) return 0;

        if (route.UsesFixedRequirements) return 0;

        var turnIns = ReputationRouteHelper.CountTurnInsFromPool(route, itemCount);
        if (turnIns <= 0) return 0;

        useHonorToken = useHonorToken && route.HonorTokenReputation > 0;
        var repPerTurnIn = ReputationRouteHelper.GetReputationPerTurnIn(route, useHonorToken);
        return turnIns * repPerTurnIn;
    }

    private static string BuildSummary(
        ReputationTurnInRoute route,
        int turnIns,
        int gained,
        int target,
        int repPerTurnIn,
        bool useHonorToken,
        IReadOnlyList<ReputationItemNeed> breakdown)
    {
        var tokenSuffix = useHonorToken
            ? $", jeton d'honneur inclus (+{route.HonorTokenReputation})"
            : "";

        if (route.UsesFixedRequirements)
        {
            var parts = breakdown
                .Select(b => $"{b.QuantityNeeded}× {b.NameFr}")
                .ToList();
            return
                $"{turnIns} remise{(turnIns > 1 ? "s" : "")} ({string.Join(" + ", parts)}) "
                + $"pour {gained} réputation (objectif {target}, {repPerTurnIn} rép./remise{tokenSuffix}).";
        }

        if (breakdown.Count == 1 && breakdown[0].ItemId > 0)
        {
            var need = breakdown[0];
            var lotLabel = route.ItemsPerTurnIn > 1
                ? $"lot de {route.ItemsPerTurnIn}"
                : "échange";
            return
                $"{need.QuantityNeeded} {route.ItemUnitLabelFr} pour {gained} réputation "
                + $"(objectif {target}, {repPerTurnIn} rép. par {lotLabel}{tokenSuffix}).";
        }

        return
            $"{breakdown.FirstOrDefault()?.QuantityNeeded ?? 0} {route.ItemUnitLabelFr} pour {gained} réputation "
            + $"(objectif {target}, {repPerTurnIn} rép. par échange{tokenSuffix}).";
    }
}
