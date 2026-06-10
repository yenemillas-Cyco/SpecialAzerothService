using SpecialAzerothService.Core.Models.Craft;

namespace SpecialAzerothService.Core.Services;

/// <summary>Planification des composants liés pour les quêtes Arcanum (hors pool mule).</summary>
public interface IArcanumPlanningService
{
    ArcanumPlanningResult Plan(
        IReadOnlyList<ArcanumQuestDemand> demands,
        IReadOnlyList<ArcanumCharacterStock> characters);
}
