using SpecialAzerothService.Core.Models.Craft;

namespace SpecialAzerothService.Core.Services;

public interface ICraftListsService
{
    IReadOnlyList<CraftListDefinition> Lists { get; }
    CraftListDefinition? GetById(string id);
    CraftListDefinition CreateList(string name);
    void RenameList(string id, string name);
    void DeleteList(string id);
    void AddItem(string listId, CraftListItem item);
    void RemoveItem(string listId, int itemId, int spellId = 0);
    void SetQuantity(string listId, int itemId, int quantity, int spellId = 0);
    void Save();
    void ExportList(string listId, string filePath);
    CraftListDefinition ImportList(string filePath);
}
