using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.Craft;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

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
                var charKey = (account.AccountName, character.Name);
                if (!characters.TryGetValue(charKey, out var charStock))
                {
                    charStock = new CraftCharacterStock
                    {
                        CharacterName = character.Name,
                        AccountName = account.AccountName,
                        GoldCopper = character.Gold
                    };
                    characters[charKey] = charStock;
                }
                else if (character.Gold > charStock.GoldCopper)
                    charStock.GoldCopper = character.Gold;

                AddItems(character.Inventory, character.Name, account.AccountName,
                    CraftPickupSource.Inventory, totals, breakdown, characters, includeInMulePool: true);
                AddItems(character.Bank, character.Name, account.AccountName,
                    CraftPickupSource.Bank, totals, breakdown, characters, includeInMulePool: true);

                foreach (var mail in character.Mail)
                {
                    AddItems(mail.Items, character.Name, account.AccountName,
                        CraftPickupSource.Mail, totals, breakdown, characters, includeInMulePool: false);
                }
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
        Dictionary<(string Account, string Character), CraftCharacterStock> characters,
        bool includeInMulePool)
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

        var transferableStore = source switch
        {
            CraftPickupSource.Inventory => charStock.Inventory,
            CraftPickupSource.Bank => charStock.Bank,
            _ => charStock.Mail
        };
        var boundStore = source switch
        {
            CraftPickupSource.Inventory => charStock.BoundInventory,
            CraftPickupSource.Bank => charStock.BoundBank,
            _ => charStock.BoundMail
        };

        foreach (var item in items)
        {
            if (item.ItemId <= 0 || item.Count <= 0) continue;

            var treatAsBound = item.IsBound
                || QuestBoundMaterialHelper.IsNonTransferableQuestMaterial(item.ItemId);

            if (treatAsBound)
            {
                boundStore[item.ItemId] = boundStore.GetValueOrDefault(item.ItemId) + item.Count;
                AddBreakdownHold(breakdown, item.ItemId, characterName, accountName, item.Count, isBound: true);
                continue;
            }

            if (includeInMulePool)
                totals[item.ItemId] = totals.GetValueOrDefault(item.ItemId) + item.Count;

            transferableStore[item.ItemId] = transferableStore.GetValueOrDefault(item.ItemId) + item.Count;
            AddBreakdownHold(breakdown, item.ItemId, characterName, accountName, item.Count, isBound: false);
        }
    }

    private static void AddBreakdownHold(
        Dictionary<int, List<CraftStockCharacterHold>> breakdown,
        int itemId,
        string characterName,
        string accountName,
        int count,
        bool isBound)
    {
        if (!breakdown.TryGetValue(itemId, out var list))
        {
            list = [];
            breakdown[itemId] = list;
        }

        var idx = list.FindIndex(h =>
            h.IsBound == isBound
            && h.CharacterName.Equals(characterName, StringComparison.OrdinalIgnoreCase)
            && h.AccountName.Equals(accountName, StringComparison.OrdinalIgnoreCase));

        if (idx >= 0)
        {
            var prev = list[idx];
            list[idx] = new CraftStockCharacterHold
            {
                CharacterName = characterName,
                AccountName = accountName,
                Count = prev.Count + count,
                IsBound = isBound
            };
        }
        else
        {
            list.Add(new CraftStockCharacterHold
            {
                CharacterName = characterName,
                AccountName = accountName,
                Count = count,
                IsBound = isBound
            });
        }
    }
}
