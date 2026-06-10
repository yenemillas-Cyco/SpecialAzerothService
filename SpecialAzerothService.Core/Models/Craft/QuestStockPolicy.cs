namespace SpecialAzerothService.Core.Models.Craft;

/// <summary>Comment le plan craft doit traiter le stock multibox pour une catégorie de quêtes.</summary>
public enum QuestStockPolicy
{
    /// <summary>T3, E'ko, Terres Foudroyées — composants farmables, pool mule classique.</summary>
    MulePool = 0,

    /// <summary>
    /// Librams / Arcanums — tous les composants d'une quête doivent être sur le même personnage
    /// (pas de mélange). L'arcanum crafté est lié au crafter mais peut être posé sur l'équipement d'un autre perso.
    /// </summary>
    SingleCrafterPerQuest = 1
}
