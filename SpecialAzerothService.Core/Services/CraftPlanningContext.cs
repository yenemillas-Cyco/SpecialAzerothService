namespace SpecialAzerothService.Core.Services;

public sealed class CraftPlanningContext : ICraftPlanningContext
{
    public string? ActiveListId { get; private set; }
    public event Action? ActiveListChanged;
    public event Action? ListItemsChanged;

    public void SetActiveList(string? listId)
    {
        if (ActiveListId == listId) return;
        ActiveListId = listId;
        ActiveListChanged?.Invoke();
    }

    public void NotifyListItemsChanged() => ListItemsChanged?.Invoke();
}
