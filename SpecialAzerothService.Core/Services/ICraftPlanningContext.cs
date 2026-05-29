namespace SpecialAzerothService.Core.Services;

/// <summary>Liste de farm actuellement sélectionnée dans l’onglet Crafting.</summary>
public interface ICraftPlanningContext
{
    string? ActiveListId { get; }
    event Action? ActiveListChanged;
    event Action? ListItemsChanged;

    void SetActiveList(string? listId);
    void NotifyListItemsChanged();
}
