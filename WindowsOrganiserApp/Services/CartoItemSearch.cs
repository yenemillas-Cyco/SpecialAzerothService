using WindowsOrganiserApp.Models.WowSync;

namespace WindowsOrganiserApp.Services;

public static class CartoItemSearch
{
    public static List<WowItemSearchResult> Search(
        IEnumerable<WowAccountData> accounts,
        string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length < 2)
            return [];

        var results = new List<WowItemSearchResult>();
        foreach (var account in accounts)
        {
            foreach (var character in account.Characters)
            {
                SearchInItems(results, account.AccountName, character, character.Inventory, "Inventaire", trimmed);
                SearchInItems(results, account.AccountName, character, character.Bank, "Banque", trimmed);

                foreach (var mail in character.Mail)
                    SearchInItems(results, account.AccountName, character, mail.Items, "Courrier", trimmed);
            }
        }

        return results
            .OrderBy(x => x.ItemName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.CharacterName)
            .ToList();
    }

    private static void SearchInItems(
        List<WowItemSearchResult> results,
        string accountName,
        WowCharacterData character,
        IEnumerable<WowItem> items,
        string location,
        string query)
    {
        foreach (var item in items)
        {
            if (!item.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            results.Add(new WowItemSearchResult
            {
                ItemName = item.Name,
                ItemId = item.ItemId,
                Count = item.Count,
                AccountName = accountName,
                CharacterName = character.Name,
                Location = location,
                Character = character
            });
        }
    }
}
