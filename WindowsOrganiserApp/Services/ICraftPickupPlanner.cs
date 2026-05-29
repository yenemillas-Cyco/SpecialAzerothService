using WindowsOrganiserApp.Models.Craft;

namespace WindowsOrganiserApp.Services;

public interface ICraftPickupPlanner
{
  /// <summary>
  /// Planifie matériaux (décomposition + stock) et répartit consos / retraits banque par perso Moi.
  /// </summary>
  CraftPlanningResult Plan(
      IEnumerable<(int ItemId, int SpellId, int Quantity)> outputs,
      CraftStockSnapshot stock,
      CraftPlanningOptions? options = null);
}
