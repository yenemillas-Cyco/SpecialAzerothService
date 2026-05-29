using WindowsOrganiserApp.Models.Craft;

namespace WindowsOrganiserApp.Services;

public interface ICraftDecompositionService
{
    /// <summary>Décompose récursivement les objets craftés en matériaux primaires (comptes agrégés).</summary>
    IReadOnlyDictionary<int, int> DecomposeToMaterials(IEnumerable<(int ItemId, int Quantity)> outputs);

    /// <summary>
    /// Décompose en consommant d'abord le stock (crafts finis et intermédiaires), puis retourne
    /// les matériaux primaires encore nécessaires avec brut / net.
    /// </summary>
    CraftDecompositionResult DecomposeWithStock(
        IEnumerable<(int ItemId, int Quantity)> outputs,
        IReadOnlyDictionary<int, int> stockTotals);
}
