using System.IO;
using System.Text.Json;
using SpecialAzerothService.Core.Models.Craft;

namespace SpecialAzerothService.Core.Services;

public sealed class CraftListsService : ICraftListsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private readonly List<CraftListDefinition> _lists = [];

    public CraftListsService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpecialAzerothService");
        Directory.CreateDirectory(appData);
        _filePath = Path.Combine(appData, "craft-lists.json");
        Load();
    }

    public IReadOnlyList<CraftListDefinition> Lists => _lists;

    public CraftListDefinition? GetById(string id) =>
        _lists.FirstOrDefault(l => l.Id == id);

    public CraftListDefinition CreateList(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrEmpty(trimmed))
            trimmed = $"Liste {_lists.Count + 1}";

        var list = new CraftListDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = trimmed
        };
        _lists.Add(list);
        Save();
        return list;
    }

    public void RenameList(string id, string name)
    {
        var list = GetById(id);
        if (list == null) return;

        var trimmed = name.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;

        list.Name = trimmed;
        Save();
    }

    public void DeleteList(string id)
    {
        _lists.RemoveAll(l => l.Id == id);
        Save();
    }

    public void AddItem(string listId, CraftListItem item)
    {
        var list = GetById(listId);
        if (list == null || !CraftListItemKey.IsValid(item)) return;

        var items = list.EnsureItems();
        var existing = items.FirstOrDefault(i => CraftListItemKey.Matches(i, item));
        if (existing != null)
        {
            existing.ProfessionId ??= item.ProfessionId;
            if (existing.SpellId <= 0) existing.SpellId = item.SpellId;
        }
        else
        {
            items.Add(new CraftListItem
            {
                ItemId = item.ItemId,
                Quantity = item.Quantity < 0 ? 0 : item.Quantity,
                ProfessionId = item.ProfessionId,
                SpellId = item.SpellId
            });
        }

        Save();
    }

    public void RemoveItem(string listId, int itemId, int spellId = 0)
    {
        var list = GetById(listId);
        if (list == null) return;

        list.Items.RemoveAll(i => CraftListItemKey.Matches(i, itemId, spellId));
        Save();
    }

    public void SetQuantity(string listId, int itemId, int quantity, int spellId = 0)
    {
        var list = GetById(listId);
        if (list == null) return;

        var items = list.EnsureItems();
        var item = items.FirstOrDefault(i => CraftListItemKey.Matches(i, itemId, spellId));
        if (item == null) return;

        if (quantity < 0) quantity = 0;
        item.Quantity = quantity;

        Save();
    }

    public void ExportList(string listId, string filePath)
    {
        var list = GetById(listId);
        if (list == null) return;

        list.EnsureItems();
        File.WriteAllText(filePath, JsonSerializer.Serialize(list, JsonOpts));
    }

    public CraftListDefinition ImportList(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var imported = JsonSerializer.Deserialize<CraftListDefinition>(json, JsonOpts)
            ?? throw new InvalidDataException("Fichier de liste invalide.");

        imported.Id = Guid.NewGuid().ToString("N");
        imported.EnsureItems();
        if (string.IsNullOrWhiteSpace(imported.Name))
            imported.Name = $"Import {_lists.Count + 1}";

        _lists.Add(imported);
        Save();
        return imported;
    }

    public void Save()
    {
        var data = new CraftListsData { Lists = _lists.ToList() };
        File.WriteAllText(_filePath, JsonSerializer.Serialize(data, JsonOpts));
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;

        try
        {
            var data = JsonSerializer.Deserialize<CraftListsData>(File.ReadAllText(_filePath), JsonOpts);
            if (data?.Lists == null) return;

            _lists.Clear();
            foreach (var list in data.Lists)
            {
                if (string.IsNullOrWhiteSpace(list.Id) || string.IsNullOrWhiteSpace(list.Name))
                    continue;
                list.EnsureItems();
                _lists.Add(list);
            }
        }
        catch
        {
            // fichier corrompu : repartir vide
        }
    }

}
