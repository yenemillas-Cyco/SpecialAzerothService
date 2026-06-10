namespace SpecialAzerothService.Core.Services;

/// <summary>
/// Composants de quête non transférables entre personnages (buffs TF finis, butins BoP arcanum, etc.).
/// Les organes de farm TF et les librams restent dans le pool mule.
/// </summary>
public static class QuestBoundMaterialHelper
{
  /// <summary>Consommables finis Terres Foudroyées (liés au crafter).</summary>
  public static readonly HashSet<int> TerresFoudroyeesFinishedBuffIds =
  [
    8410, // R.O.I.D.S.
    8411, // Poudre de scorpok terrestre
    8412, // Cocktail de jus de poumon
    8423, // Potion de cortex cérébral
    8424  // Gomme de gésier
  ];

  /// <summary>Butins BoP des quêtes arcanum (Scholomance, DM, BRD, etc.).</summary>
  public static readonly HashSet<int> ArcanumBoundQuestMaterialIds =
  [
    Tier3QuestCatalog.BurningEssence,
    Tier3QuestCatalog.BlackBloodOfTheTormented,
    Tier3QuestCatalog.SkinOfShadow,
    Tier3QuestCatalog.BloodOfHeroes,
    Tier3QuestCatalog.FrayedAbominationStitching,
    Tier3QuestCatalog.EyeOfKajal,
    Tier3QuestCatalog.GizzardGum
  ];

  public static bool IsNonTransferableQuestMaterial(int itemId) =>
    TerresFoudroyeesFinishedBuffIds.Contains(itemId)
    || ArcanumBoundQuestMaterialIds.Contains(itemId);

  public static bool IsLibram(int itemId) =>
    itemId is Tier3QuestCatalog.LibramRumination
      or Tier3QuestCatalog.LibramConstitution
      or Tier3QuestCatalog.LibramTenacity
      or Tier3QuestCatalog.LibramResilience
      or Tier3QuestCatalog.LibramVoracity
      or Tier3QuestCatalog.LibramRapidity
      or Tier3QuestCatalog.LibramFocus
      or Tier3QuestCatalog.LibramProtection;

  public static IReadOnlyList<Tier3Material> GetBoundMaterials(QuestPieceRecipe recipe) =>
    recipe.Materials.Where(m => IsNonTransferableQuestMaterial(m.ItemId)).ToList();

  public static bool RecipeHasBoundMaterials(QuestPieceRecipe recipe) =>
    recipe.Materials.Any(m => IsNonTransferableQuestMaterial(m.ItemId));
}
