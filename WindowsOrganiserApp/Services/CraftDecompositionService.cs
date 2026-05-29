using WindowsOrganiserApp.Models.Craft;

namespace WindowsOrganiserApp.Services;

public sealed class CraftDecompositionService : ICraftDecompositionService
{
    private const int MaxDepth = 12;
    private readonly ICraftCatalogLookup _catalog;

    public CraftDecompositionService(ICraftCatalogLookup catalog) => _catalog = catalog;

    public IReadOnlyDictionary<int, int> DecomposeToMaterials(IEnumerable<(int ItemId, int Quantity)> outputs)
    {
        var totals = new Dictionary<int, int>();

        foreach (var (itemId, quantity) in outputs)
        {
            if (itemId <= 0 || quantity <= 0) continue;
            DecomposeItem(itemId, quantity, totals, depth: 0, stack: []);
        }

        return totals;
    }

    public CraftDecompositionResult DecomposeWithStock(
        IEnumerable<(int ItemId, int Quantity)> outputs,
        IReadOnlyDictionary<int, int> stockTotals)
    {
        var virtualStock = new Dictionary<int, int>(stockTotals);
        var net = new Dictionary<int, int>();

        foreach (var (itemId, quantity) in outputs)
        {
            if (itemId <= 0 || quantity <= 0) continue;
            SatisfyNeed(itemId, quantity, virtualStock, net, depth: 0, stack: []);
        }

        var gross = DecomposeToMaterials(outputs);
        var materials = new Dictionary<int, MaterialRequirement>();

        foreach (var itemId in net.Keys.Union(gross.Keys))
        {
            var netQty = net.GetValueOrDefault(itemId);
            var grossQty = gross.GetValueOrDefault(itemId);
            if (netQty <= 0 && grossQty <= 0) continue;

            materials[itemId] = new MaterialRequirement
            {
                NetNeeded = netQty,
                GrossNeeded = grossQty
            };
        }

        return new CraftDecompositionResult { Materials = materials };
    }

    private void SatisfyNeed(
        int itemId,
        int quantity,
        Dictionary<int, int> virtualStock,
        Dictionary<int, int> net,
        int depth,
        HashSet<int> stack)
    {
        if (quantity <= 0) return;

        var available = virtualStock.GetValueOrDefault(itemId);
        var used = Math.Min(quantity, available);
        if (used > 0)
            virtualStock[itemId] = available - used;

        var remaining = quantity - used;
        if (remaining <= 0) return;

        if (depth > MaxDepth || !stack.Add(itemId))
        {
            Add(net, itemId, remaining);
            return;
        }

        if (_catalog.TryGetByCreatedItemId(itemId, out var lookup)
            && CraftDecompositionHelper.ShouldExpandIntoReagents(lookup))
        {
            foreach (var reagent in lookup.Entry.Reagents)
            {
                var needed = SafeMultiply(reagent.Count, remaining);
                if (needed > 0)
                    SatisfyNeed(reagent.ItemId, needed, virtualStock, net, depth + 1, stack);
            }
        }
        else
            Add(net, itemId, remaining);

        stack.Remove(itemId);
    }

    private void DecomposeItem(int itemId, int quantity, Dictionary<int, int> totals, int depth, HashSet<int> stack)
    {
        if (quantity <= 0) return;

        if (depth > MaxDepth || !stack.Add(itemId))
        {
            Add(totals, itemId, quantity);
            return;
        }

        if (_catalog.TryGetByCreatedItemId(itemId, out var lookup)
            && CraftDecompositionHelper.ShouldExpandIntoReagents(lookup))
        {
            foreach (var reagent in lookup.Entry.Reagents)
            {
                var needed = SafeMultiply(reagent.Count, quantity);
                if (needed > 0)
                    DecomposeItem(reagent.ItemId, needed, totals, depth + 1, stack);
            }
        }
        else
            Add(totals, itemId, quantity);

        stack.Remove(itemId);
    }

    private static void Add(Dictionary<int, int> totals, int itemId, int quantity)
    {
        var sum = (long)totals.GetValueOrDefault(itemId) + quantity;
        totals[itemId] = sum > int.MaxValue ? int.MaxValue : (int)sum;
    }

    private static int SafeMultiply(int a, int b)
    {
        var product = (long)a * b;
        if (product <= 0) return 0;
        return product > int.MaxValue ? int.MaxValue : (int)product;
    }
}
