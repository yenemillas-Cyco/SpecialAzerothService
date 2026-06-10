using SpecialAzerothService.Core.Models.Reputation;

namespace SpecialAzerothService.Core.Services;

public static class ReputationRouteHelper
{
    public static int GetReputationPerTurnIn(ReputationTurnInRoute route, bool useHonorToken)
    {
        useHonorToken = useHonorToken && route.HonorTokenReputation > 0;
        return route.BaseReputation + (useHonorToken ? route.HonorTokenReputation : 0);
    }

    public static int CountTurnInsFromPool(ReputationTurnInRoute route, IReadOnlyDictionary<int, int> itemCounts)
    {
        if (route.UsesFixedRequirements)
        {
            var turnIns = int.MaxValue;
            foreach (var req in route.Requirements)
            {
                itemCounts.TryGetValue(req.ItemId, out var count);
                turnIns = Math.Min(turnIns, count / req.QuantityPerTurnIn);
            }

            return turnIns == int.MaxValue ? 0 : turnIns;
        }

        var poolTotal = route.AcceptedItems.Sum(i => itemCounts.GetValueOrDefault(i.ItemId));
        if (route.ItemsPerTurnIn <= 0) return 0;
        return poolTotal / route.ItemsPerTurnIn;
    }

    public static int CountTurnInsFromPool(ReputationTurnInRoute route, int pooledItemCount)
    {
        if (route.UsesFixedRequirements || route.ItemsPerTurnIn <= 0) return 0;
        return pooledItemCount / route.ItemsPerTurnIn;
    }

    public static IReadOnlyList<ReputationItemNeed> BuildItemBreakdown(
        ReputationTurnInRoute route,
        int turnInCount)
    {
        if (turnInCount <= 0) return [];

        if (route.UsesFixedRequirements)
        {
            return route.Requirements
                .Select(req => new ReputationItemNeed
                {
                    ItemId = req.ItemId,
                    NameFr = req.NameFr,
                    QuantityNeeded = turnInCount * req.QuantityPerTurnIn,
                })
                .ToList();
        }

        if (route.AcceptedItems.Count == 1)
        {
            var item = route.AcceptedItems[0];
            return
            [
                new ReputationItemNeed
                {
                    ItemId = item.ItemId,
                    NameFr = item.NameFr,
                    QuantityNeeded = turnInCount * route.ItemsPerTurnIn,
                },
            ];
        }

        return
        [
            new ReputationItemNeed
            {
                ItemId = 0,
                NameFr = route.ItemUnitLabelFr,
                QuantityNeeded = turnInCount * route.ItemsPerTurnIn,
            },
        ];
    }

    public static IReadOnlyList<(int ItemId, string NameFr)> GetStockItemIds(ReputationTurnInRoute route)
    {
        if (route.UsesFixedRequirements)
            return route.Requirements.Select(r => (r.ItemId, r.NameFr)).ToList();

        return route.AcceptedItems.Select(i => (i.ItemId, i.NameFr)).ToList();
    }
}
