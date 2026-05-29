using WindowsOrganiserApp.Models.Carto;
using WindowsOrganiserApp.Models.Craft;
using WindowsOrganiserApp.Models.WowSync;

namespace WindowsOrganiserApp.Services;

public sealed class CraftStockService : ICraftStockService
{
    private readonly ICartoService _cartoService;
    private readonly IWowSyncService _wowSyncService;

    public CraftStockService(ICartoService cartoService, IWowSyncService wowSyncService)
    {
        _cartoService = cartoService;
        _wowSyncService = wowSyncService;
    }

    public IReadOnlyList<CraftStockOwnerInfo> GetAvailableOwners()
    {
        var carto = _cartoService.Load();
        return carto.Users
            .OrderBy(u => u.SortOrder)
            .ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            .Select(u => new CraftStockOwnerInfo
            {
                UserId = u.Id,
                OwnerName = u.Name
            })
            .ToList();
    }

    public CraftStockSnapshot ReadStockForOwners(IReadOnlyCollection<string> userIds)
    {
        var selectedUserIds = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selectedUserIds.Count == 0)
            return new CraftStockSnapshot();

        var carto = _cartoService.Load();
        var allowedFolders = carto.AccountSettings
            .Where(kv => selectedUserIds.Contains(kv.Value.UserId))
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (allowedFolders.Count == 0)
            return new CraftStockSnapshot();

        var totals = new Dictionary<int, int>();
        var breakdown = new Dictionary<int, List<CraftStockCharacterHold>>();
        var characters = new Dictionary<(string Account, string Character), CraftCharacterStock>();

        List<WowAccountData> accounts;
        try
        {
            if (string.IsNullOrWhiteSpace(_wowSyncService.WowPath))
                return new CraftStockSnapshot();

            accounts = _wowSyncService.ReadAllAccounts(carto.AccountSettings);
        }
        catch
        {
            return new CraftStockSnapshot();
        }

        foreach (var account in accounts)
        {
            if (!allowedFolders.Contains(account.SourceAccountName))
                continue;

            foreach (var character in account.Characters)
            {
                AddItems(character.Inventory, character.Name, account.AccountName,
                    CraftPickupSource.Inventory, totals, breakdown, characters);
                AddItems(character.Bank, character.Name, account.AccountName,
                    CraftPickupSource.Bank, totals, breakdown, characters);
            }
        }

        return new CraftStockSnapshot
        {
            TotalByItemId = totals,
            ByItemId = breakdown,
            Characters = characters.Values
                .OrderBy(c => c.CharacterName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.AccountName, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static void AddItems(
        IEnumerable<WowItem> items,
        string characterName,
        string accountName,
        CraftPickupSource source,
        Dictionary<int, int> totals,
        Dictionary<int, List<CraftStockCharacterHold>> breakdown,
        Dictionary<(string Account, string Character), CraftCharacterStock> characters)
    {
        var charKey = (accountName, characterName);
        if (!characters.TryGetValue(charKey, out var charStock))
        {
            charStock = new CraftCharacterStock
            {
                CharacterName = characterName,
                AccountName = accountName
            };
            characters[charKey] = charStock;
        }

        var store = source == CraftPickupSource.Inventory ? charStock.Inventory : charStock.Bank;

        foreach (var item in items)
        {
            if (item.ItemId <= 0 || item.Count <= 0) continue;

            totals[item.ItemId] = totals.GetValueOrDefault(item.ItemId) + item.Count;
            store[item.ItemId] = store.GetValueOrDefault(item.ItemId) + item.Count;

            if (!breakdown.TryGetValue(item.ItemId, out var list))
            {
                list = [];
                breakdown[item.ItemId] = list;
            }

            var idx = list.FindIndex(h =>
                h.CharacterName.Equals(characterName, StringComparison.OrdinalIgnoreCase)
                && h.AccountName.Equals(accountName, StringComparison.OrdinalIgnoreCase));

            if (idx >= 0)
            {
                var prev = list[idx];
                list[idx] = new CraftStockCharacterHold
                {
                    CharacterName = characterName,
                    AccountName = accountName,
                    Count = prev.Count + item.Count
                };
            }
            else
            {
                list.Add(new CraftStockCharacterHold
                {
                    CharacterName = characterName,
                    AccountName = accountName,
                    Count = item.Count
                });
            }
        }
    }
}
