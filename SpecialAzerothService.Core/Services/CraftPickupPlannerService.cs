using SpecialAzerothService.Core.Models.Craft;

namespace SpecialAzerothService.Core.Services;

public sealed class CraftPickupPlannerService : ICraftPickupPlanner
{
    private const int MaxDepth = 12;
    private readonly ICraftCatalogLookup _catalog;
    private readonly ICraftDecompositionService _decomposition;

    public CraftPickupPlannerService(ICraftCatalogLookup catalog, ICraftDecompositionService decomposition)
    {
        _catalog = catalog;
        _decomposition = decomposition;
    }

    public CraftPlanningResult Plan(
        IEnumerable<(int ItemId, int SpellId, int Quantity)> outputs,
        CraftStockSnapshot stock,
        CraftPlanningOptions? options = null)
    {
        options ??= new CraftPlanningOptions();

        var outputList = outputs
            .Where(o => o.Quantity > 0 && (o.ItemId > 0 || o.SpellId > 0))
            .ToList();

        var pools = stock.Characters
            .Select(c => new CharPool(
                c.CharacterName,
                c.AccountName,
                new Dictionary<int, int>(c.Inventory),
                new Dictionary<int, int>(c.Bank)))
            .ToList();

        var pickups = new List<CraftPickupLine>();
        var net = new Dictionary<int, int>();

        foreach (var (itemId, spellId, quantity) in outputList)
            SatisfyOutput(itemId, spellId, quantity, pools, net, pickups, options);

        var gross = ComputeGross(outputList);
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

        return new CraftPlanningResult
        {
            Materials = materials,
            Pickups = pickups
        };
    }

    private void SatisfyOutput(
        int itemId,
        int spellId,
        int quantity,
        List<CharPool> pools,
        Dictionary<int, int> net,
        List<CraftPickupLine> pickups,
        CraftPlanningOptions options)
    {
        if (itemId > 0)
        {
            if (options.ForceCraftOutputs
                && _catalog.TryGetByCreatedItemId(itemId, out var lookup)
                && CraftDecompositionHelper.ShouldExpandIntoReagents(lookup))
            {
                foreach (var reagent in lookup.Entry.Reagents)
                {
                    var needed = SafeMultiply(reagent.Count, quantity);
                    if (needed > 0)
                        SatisfyNeed(reagent.ItemId, needed, pools, net, pickups, depth: 0, stack: [], options);
                }
            }
            else
            {
                SatisfyNeed(itemId, quantity, pools, net, pickups, depth: 0, stack: [], options);
            }
            return;
        }

        if (spellId > 0)
            SatisfySpellRecipe(spellId, quantity, pools, net, pickups, options);
    }

    private void SatisfySpellRecipe(
        int spellId,
        int quantity,
        List<CharPool> pools,
        Dictionary<int, int> net,
        List<CraftPickupLine> pickups,
        CraftPlanningOptions options)
    {
        if (!_catalog.TryGetBySpellId(spellId, out var lookup)) return;

        foreach (var reagent in lookup.Entry.Reagents)
        {
            var needed = SafeMultiply(reagent.Count, quantity);
            if (needed > 0)
                SatisfyNeed(reagent.ItemId, needed, pools, net, pickups, depth: 0, stack: [], options);
        }
    }

    private Dictionary<int, int> ComputeGross(List<(int ItemId, int SpellId, int Quantity)> outputs)
    {
        var gross = new Dictionary<int, int>();
        foreach (var (itemId, spellId, quantity) in outputs)
        {
            if (itemId > 0)
            {
                if (CraftLeatherMaterialHelper.IsLeatherTierItem(itemId))
                    AddDirectRecipeReagents(gross, itemId, quantity);
                else
                {
                    foreach (var (matId, count) in _decomposition.DecomposeToMaterials([(itemId, quantity)]))
                        Add(gross, matId, count);
                }
            }
            else if (spellId > 0 && _catalog.TryGetBySpellId(spellId, out var lookup))
            {
                foreach (var reagent in lookup.Entry.Reagents)
                {
                    var needed = SafeMultiply(reagent.Count, quantity);
                    if (needed > 0)
                        Add(gross, reagent.ItemId, needed);
                }
            }
        }

        return gross;
    }

    /// <summary>Composants directs d'une recette cuir (un tier, pas de chute en lanières).</summary>
    private void AddDirectRecipeReagents(Dictionary<int, int> totals, int itemId, int quantity)
    {
        if (quantity <= 0) return;

        if (_catalog.TryGetByCreatedItemId(itemId, out var lookup)
            && CraftDecompositionHelper.ShouldExpandIntoReagents(lookup))
        {
            foreach (var reagent in lookup.Entry.Reagents)
            {
                var needed = SafeMultiply(reagent.Count, quantity);
                if (needed > 0)
                    Add(totals, reagent.ItemId, needed);
            }
            return;
        }

        Add(totals, itemId, quantity);
    }

    private void SatisfyNeed(
        int itemId,
        int quantity,
        List<CharPool> pools,
        Dictionary<int, int> net,
        List<CraftPickupLine> pickups,
        int depth,
        HashSet<int> stack,
        CraftPlanningOptions options)
    {
        if (quantity <= 0) return;

        if (CraftLeatherMaterialHelper.IsLeatherTierItem(itemId))
            SatisfyLeatherTierNeed(itemId, quantity, pools, net, pickups, depth, stack, options);
        else
            SatisfyNeedWithDecomposition(itemId, quantity, pools, net, pickups, depth, stack, options);
    }

    /// <summary>Exception cuir : farm au tier demandé ; mules peuvent couvrir via tiers inférieurs.</summary>
    private void SatisfyLeatherTierNeed(
        int itemId,
        int quantity,
        List<CharPool> pools,
        Dictionary<int, int> net,
        List<CraftPickupLine> pickups,
        int depth,
        HashSet<int> stack,
        CraftPlanningOptions options)
    {
        var remaining = quantity;
        if (options.UseMuleStockForComponents
            && !QuestBoundMaterialHelper.IsNonTransferableQuestMaterial(itemId))
        {
            remaining = TakeFromCharacterPools(itemId, quantity, pools, pickups);
            if (remaining > 0)
                remaining = TrySatisfyViaSubComponentsFromStock(
                    itemId, remaining, pools, pickups, depth, stack, options);
        }

        if (remaining > 0)
            Add(net, itemId, remaining);
    }

    /// <summary>Décomposition catalogue classique (étoffes, barres, etc.).</summary>
    private void SatisfyNeedWithDecomposition(
        int itemId,
        int quantity,
        List<CharPool> pools,
        Dictionary<int, int> net,
        List<CraftPickupLine> pickups,
        int depth,
        HashSet<int> stack,
        CraftPlanningOptions options)
    {
        var remaining = options.UseMuleStockForComponents
            && !QuestBoundMaterialHelper.IsNonTransferableQuestMaterial(itemId)
            ? TakeFromCharacterPools(itemId, quantity, pools, pickups)
            : quantity;
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
                    SatisfyNeed(reagent.ItemId, needed, pools, net, pickups, depth + 1, stack, options);
            }
        }
        else
            Add(net, itemId, remaining);

        stack.Remove(itemId);
    }

    /// <summary>
    /// Couvre une partie du besoin via composants inférieurs sur les mules (sans toucher au net).
    /// Retourne le nombre d'unités de <paramref name="itemId"/> encore manquantes.
    /// </summary>
    private int TrySatisfyViaSubComponentsFromStock(
        int itemId,
        int quantity,
        List<CharPool> pools,
        List<CraftPickupLine> pickups,
        int depth,
        HashSet<int> stack,
        CraftPlanningOptions options)
    {
        if (quantity <= 0) return 0;
        if (!CraftLeatherMaterialHelper.IsLeatherTierItem(itemId)) return quantity;
        if (depth > MaxDepth || !stack.Add(itemId)) return quantity;

        if (!_catalog.TryGetByCreatedItemId(itemId, out var lookup)
            || !CraftDecompositionHelper.ShouldExpandIntoReagents(lookup))
        {
            stack.Remove(itemId);
            return quantity;
        }

        var maxCraftable = int.MaxValue;
        foreach (var reagent in lookup.Entry.Reagents)
        {
            var totalNeed = SafeMultiply(reagent.Count, quantity);
            if (totalNeed <= 0) continue;

            var subUnsatisfied = SatisfyNeedFromStockOnly(
                reagent.ItemId, totalNeed, pools, pickups, depth + 1, stack, options);
            var subSatisfied = totalNeed - subUnsatisfied;
            var craftable = subSatisfied / reagent.Count;
            maxCraftable = Math.Min(maxCraftable, craftable);
        }

        stack.Remove(itemId);
        if (maxCraftable == int.MaxValue) maxCraftable = 0;
        return Math.Max(0, quantity - maxCraftable);
    }

    /// <summary>Pioche sur les mules uniquement (direct + décomposition), sans alimenter le net.</summary>
    private int SatisfyNeedFromStockOnly(
        int itemId,
        int quantity,
        List<CharPool> pools,
        List<CraftPickupLine> pickups,
        int depth,
        HashSet<int> stack,
        CraftPlanningOptions options)
    {
        if (quantity <= 0) return 0;

        var remaining = TakeFromCharacterPools(itemId, quantity, pools, pickups);
        if (remaining > 0 && CraftLeatherMaterialHelper.IsLeatherTierItem(itemId))
            remaining = TrySatisfyViaSubComponentsFromStock(
                itemId, remaining, pools, pickups, depth, stack, options);

        return remaining;
    }

    private static int TakeFromCharacterPools(
        int itemId,
        int quantity,
        List<CharPool> pools,
        List<CraftPickupLine> pickups)
    {
        var remaining = quantity;

        foreach (var pool in pools
                     .OrderByDescending(p => p.TotalAvailable(itemId))
                     .ThenBy(p => p.CharacterName, StringComparer.OrdinalIgnoreCase))
        {
            if (remaining <= 0) break;

            remaining = TakeFromPoolLocation(
                pool, itemId, remaining, pickups, CraftPickupSource.Inventory);
            if (remaining <= 0) break;

            remaining = TakeFromPoolLocation(
                pool, itemId, remaining, pickups, CraftPickupSource.Bank);
        }

        return remaining;
    }

    private static int TakeFromPoolLocation(
        CharPool pool,
        int itemId,
        int remaining,
        List<CraftPickupLine> pickups,
        CraftPickupSource source)
    {
        if (remaining <= 0) return 0;

        var store = source == CraftPickupSource.Inventory ? pool.Inventory : pool.Bank;
        var available = store.GetValueOrDefault(itemId);
        if (available <= 0) return remaining;

        var take = Math.Min(remaining, available);
        store[itemId] = available - take;
        if (store[itemId] <= 0)
            store.Remove(itemId);

        pickups.Add(new CraftPickupLine
        {
            CharacterName = pool.CharacterName,
            AccountName = pool.AccountName,
            ItemId = itemId,
            Quantity = take,
            Source = source
        });

        return remaining - take;
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

    private sealed class CharPool(
        string characterName,
        string accountName,
        Dictionary<int, int> inventory,
        Dictionary<int, int> bank)
    {
        public string CharacterName { get; } = characterName;
        public string AccountName { get; } = accountName;
        public Dictionary<int, int> Inventory { get; } = inventory;
        public Dictionary<int, int> Bank { get; } = bank;

        public int TotalAvailable(int itemId) =>
            Inventory.GetValueOrDefault(itemId) + Bank.GetValueOrDefault(itemId);
    }
}
