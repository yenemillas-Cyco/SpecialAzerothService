using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

/// <summary>Règles de tri automatique des catégories roster (niveau + contenu sacs/banque).</summary>
public static class CartoCharacterCategoryAutoSort
{
    private static readonly HashSet<int> StarterItemIds = new()
    {
        6948, 6947,
        159, 117, 2070, 4540, 4541, 4542, 4536, 4537, 4538, 4539,
        1179, 1205, 1262, 1268, 1269, 1273, 2680, 2681, 2682, 2683, 2684, 2685, 2686, 2687
    };

    private static readonly string[] StarterNameFragments =
    [
        "hearthstone", "pierre de foyer", "pierre du foyer",
        "refreshing spring water", "eau de source",
        "tough jerky", "tough hunk of bread", "food", "nourriture", "pain", "eau"
    ];

    public static CharacterStatus? SuggestCategory(WowCharacter ch, WowCharacterData? sync)
    {
        var level = sync is { Level: > 0 } ? sync.Level : ch.Level;

        if (level >= 60)
            return CharacterStatus.Main;

        if (level is >= 20 and <= 22 && IsDemoniste(ch, sync))
            return CharacterStatus.TpBoy;

        if (level is >= 1 and <= 2)
            return HasNonStarterItems(sync) ? CharacterStatus.Banque : CharacterStatus.ClicBoys;

        return null;
    }

    private static bool IsDemoniste(WowCharacter ch, WowCharacterData? sync)
    {
        if (ch.Class == WowClass.Demoniste)
            return true;

        if (!string.IsNullOrWhiteSpace(sync?.Class))
            return CartoSyncMapper.ParseClass(sync.Class) == WowClass.Demoniste;

        return false;
    }

    private static bool HasNonStarterItems(WowCharacterData? sync)
    {
        if (sync == null)
            return false;

        foreach (var item in sync.Inventory.Concat(sync.Bank))
        {
            if (item.Count <= 0)
                continue;

            if (!IsStarterItem(item))
                return true;
        }

        return false;
    }

    private static bool IsStarterItem(WowItem item)
    {
        if (item.ItemId > 0 && StarterItemIds.Contains(item.ItemId))
            return true;

        if (string.IsNullOrWhiteSpace(item.Name))
            return false;

        var name = item.Name.Trim();
        foreach (var fragment in StarterNameFragments)
        {
            if (name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
