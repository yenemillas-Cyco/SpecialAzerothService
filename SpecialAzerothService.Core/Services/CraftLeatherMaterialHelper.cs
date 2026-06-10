namespace SpecialAzerothService.Core.Services;

/// <summary>
/// Exception farm cuir : ne pas décomposer en lanières / chutes pour « À farmer ».
/// Les mules peuvent encore utiliser les tiers inférieurs en stock.
/// Tout le reste des matériaux garde la décomposition catalogue classique.
/// </summary>
public static class CraftLeatherMaterialHelper
{
    public static readonly HashSet<int> LeatherTierItemIds =
    [
        8170, // Cuir robuste
        4304, // Cuir épais
        4234, // Cuir lourd
        2319, // Cuir moyen
        2318, // Cuir léger
    ];

    public static bool IsLeatherTierItem(int itemId) => LeatherTierItemIds.Contains(itemId);
}
