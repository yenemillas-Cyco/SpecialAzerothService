using SpecialAzerothService.Core.Models.Craft;

namespace SpecialAzerothService.Core.Services;

/// <summary>
/// Composants liés uniquement (buffs TF finis, etc.).
/// Librams et farm passent par le pool mule — pas traités ici.
/// </summary>
public sealed class ArcanumPlanningService : IArcanumPlanningService
{
    public ArcanumPlanningResult Plan(
        IReadOnlyList<ArcanumQuestDemand> demands,
        IReadOnlyList<ArcanumCharacterStock> characters)
    {
        if (demands.Count == 0)
            return new ArcanumPlanningResult { SummaryNote = "Aucun arcanum dans la liste." };

        var assignments = new List<ArcanumQuestAssignment>();

        foreach (var demand in demands)
        {
            if (!Tier3QuestCatalog.TryFindPieceByResultItemId(demand.ResultItemId, out var recipe, out _))
            {
                assignments.Add(new ArcanumQuestAssignment
                {
                    Demand = demand,
                    ErrorMessage = "Recette inconnue."
                });
                continue;
            }

            var boundMats = QuestBoundMaterialHelper.GetBoundMaterials(recipe!);
            if (boundMats.Count == 0)
                continue;

            var boundNeeds = new List<BoundMaterialNeed>();
            foreach (var mat in boundMats)
            {
                var required = SafeMultiply(mat.Quantity, demand.Quantity);
                var holders = new List<BoundMaterialCharacterHold>();

                foreach (var ch in characters)
                {
                    var bound = ch.GetBound(mat.ItemId);
                    if (bound <= 0) continue;

                    holders.Add(new BoundMaterialCharacterHold
                    {
                        CharacterName = ch.CharacterName,
                        AccountName = ch.AccountName,
                        BoundCount = bound,
                        TotalOnCharacter = ch.GetTotalOnCharacter(mat.ItemId),
                        GoldCopper = ch.GoldCopper,
                        RequiredCount = required
                    });
                }

                holders.Sort((a, b) =>
                    string.Compare(a.CharacterName, b.CharacterName, StringComparison.OrdinalIgnoreCase));

                boundNeeds.Add(new BoundMaterialNeed
                {
                    ItemId = mat.ItemId,
                    DisplayNameFr = mat.DisplayNameFr,
                    RequiredCount = required,
                    Characters = holders
                });
            }

            assignments.Add(new ArcanumQuestAssignment
            {
                Demand = demand,
                QuestGoldCostCopper = recipe!.GoldCostCopper,
                HasBoundMaterials = true,
                BoundNeeds = boundNeeds
            });
        }

        return new ArcanumPlanningResult
        {
            Assignments = assignments,
            SummaryNote =
                "Composants liés : buffs TF finis, peaux d'ombre, sang de héros, etc. — sur le crafter uniquement. "
                + "Librams, diamants et éclats : pool mule classique (À prendre — par perso)."
        };
    }

    private static int SafeMultiply(int a, int b)
    {
        var product = (long)a * b;
        if (product <= 0) return 0;
        return product > int.MaxValue ? int.MaxValue : (int)product;
    }
}
