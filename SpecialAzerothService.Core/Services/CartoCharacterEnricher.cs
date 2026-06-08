using System.Globalization;
using System.Text;
using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

public static class CartoCharacterEnricher
{
    private const int SoulShardItemId = 6265;
    private const int RendHeadItemId = 12630;
    private const int OnyxiaHeadItemId = 18422;
    private const int NefarianHeadItemId = 19002;
    private const int HakkarHeartItemId = 19802;

    public static WowItem QuestItemWowStub(QuestItemType type) =>
        type switch
        {
            QuestItemType.Tete_de_Rend =>
                new WowItem { ItemId = RendHeadItemId, Name = "Tête de Rend", Count = 1, Quality = 4 },
            QuestItemType.Tete_dOnyxia =>
                new WowItem { ItemId = OnyxiaHeadItemId, Name = "Tête d'Onyxia", Count = 1, Quality = 4 },
            QuestItemType.Tete_de_Nefarian =>
                new WowItem { ItemId = NefarianHeadItemId, Name = "Tête de Nefarian", Count = 1, Quality = 4 },
            QuestItemType.Coeur_de_Hakkar =>
                new WowItem { ItemId = HakkarHeartItemId, Name = "Cœur de Hakkar", Count = 1, Quality = 4 },
            _ => new WowItem { ItemId = 0, Name = "", Count = 1 }
        };

    /// <summary>
    /// Utilise l'objet WowSync (icône fichier jeu) si présent dans sac/banque, sinon stub Wowhead.
    /// </summary>
    public static WowItem ResolveQuestIconItem(WowCharacterData? sync, QuestItemType type)
    {
        var stub = QuestItemWowStub(type);
        if (sync == null || stub.ItemId <= 0)
            return stub;

        foreach (var item in sync.Inventory.Concat(sync.Bank))
        {
            if (item.ItemId == stub.ItemId)
                return CopyWowItemForDisplay(item, stub);
        }

        foreach (var item in sync.Inventory.Concat(sync.Bank))
        {
            if (MatchQuestItem(item) == type)
                return CopyWowItemForDisplay(item, stub);
        }

        return stub;
    }

    private static WowItem CopyWowItemForDisplay(WowItem src, WowItem fallbackQuality)
    {
        return new WowItem
        {
            Name = src.Name,
            Count = Math.Max(1, src.Count),
            ItemId = src.ItemId,
            Icon = src.Icon,
            Quality = src.Quality > 0 ? src.Quality : fallbackQuality.Quality
        };
    }

    private static readonly Dictionary<string, ProfessionType> ProfessionByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alchimie"] = ProfessionType.Alchimie,
        ["Alchemy"] = ProfessionType.Alchimie,
        ["Forge"] = ProfessionType.Forge,
        ["Forgeron"] = ProfessionType.Forge,
        ["Blacksmithing"] = ProfessionType.Forge,
        ["Enchantement"] = ProfessionType.Enchantement,
        ["Enchanting"] = ProfessionType.Enchantement,
        ["Ingénierie"] = ProfessionType.Ingenierie,
        ["Ingenierie"] = ProfessionType.Ingenierie,
        ["Engineering"] = ProfessionType.Ingenierie,
        ["Herboristerie"] = ProfessionType.Herboristerie,
        ["Herbalism"] = ProfessionType.Herboristerie,
        ["Couture"] = ProfessionType.Couture,
        ["Couturier"] = ProfessionType.Couture,
        ["Tailoring"] = ProfessionType.Couture,
        ["Tailor"] = ProfessionType.Couture,
        ["Alchimiste"] = ProfessionType.Alchimie,
        ["Travailleur du cuir"] = ProfessionType.Travail_du_cuir,
        ["Leatherworker"] = ProfessionType.Travail_du_cuir,
        ["Travail du cuir"] = ProfessionType.Travail_du_cuir,
        ["Leatherworking"] = ProfessionType.Travail_du_cuir,
        ["Exploitation"] = ProfessionType.Exploitation_miniere,
        ["Minage"] = ProfessionType.Exploitation_miniere,
        ["Mining"] = ProfessionType.Exploitation_miniere,
        ["Dépeçage"] = ProfessionType.Depecage,
        ["Depecage"] = ProfessionType.Depecage,
        ["Skinning"] = ProfessionType.Depecage,
        ["Pêche"] = ProfessionType.Peche,
        ["Peche"] = ProfessionType.Peche,
        ["Fishing"] = ProfessionType.Peche,
        ["Cuisine"] = ProfessionType.Cuisine,
        ["Cooking"] = ProfessionType.Cuisine,
        ["Secourisme"] = ProfessionType.Secourisme,
        ["First Aid"] = ProfessionType.Secourisme,
    };

    private static readonly (QuestItemType Type, int[] ItemIds, string[] NameFragments)[] QuestItemRules =
    [
        (QuestItemType.Tete_de_Rend, [RendHeadItemId], ["rend", "tete", "head", "blackhand"]),
        (QuestItemType.Tete_dOnyxia, [OnyxiaHeadItemId], ["onyxia", "tete", "head"]),
        (QuestItemType.Tete_de_Nefarian, [NefarianHeadItemId], ["nefarian", "tete", "head"]),
        (QuestItemType.Coeur_de_Hakkar, [HakkarHeartItemId], ["hakkar", "coeur", "cœur", "heart"]),
    ];

    public static void ApplyFromSync(WowCharacterData sync, WowCharacter carto)
    {
        if (!string.IsNullOrWhiteSpace(sync.Race))
            carto.Race = sync.Race.Trim();

        SyncProfessions(sync, carto);
        SyncQuestItems(sync, carto);
        SyncRaidAttunements(sync, carto);
        CartoSyncMapper.ApplyCooldownsFromSync(sync, carto);
        if (carto.Class == WowClass.Demoniste)
            carto.ShardCount = CountSoulShards(sync);
    }

    private static void SyncProfessions(WowCharacterData sync, WowCharacter carto)
    {
        if (sync.Professions.Count == 0) return;

        var mapped = new List<ProfessionInfo>();
        foreach (var prof in sync.Professions)
        {
            if (!TryMapProfession(prof.Name, out var type)) continue;
            mapped.Add(new ProfessionInfo
            {
                Type = type,
                Skill = prof.Rank > 0 ? prof.Rank : (prof.MaxRank > 0 ? prof.MaxRank : 1)
            });
        }

        if (mapped.Count > 0)
            carto.Professions = mapped;
    }

    public static bool TryMapProfessionName(string? name, out ProfessionType type) =>
        TryMapProfession(name, out type);

    private static bool TryMapProfession(string? name, out ProfessionType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(name)) return false;

        var trimmed = name.Trim();
        if (ProfessionByName.TryGetValue(trimmed, out type))
            return true;

        foreach (var (key, value) in ProfessionByName)
        {
            if (trimmed.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                type = value;
                return true;
            }
        }

        return false;
    }

    private static void SyncRaidAttunements(WowCharacterData sync, WowCharacter carto)
    {
        carto.RaidAttunements.Clear();
        foreach (var def in RaidAttunementCatalog.All)
        {
            carto.RaidAttunements.Add(new RaidAttunementEntry
            {
                Type = def.Type,
                IsAttuned = sync.HasRaidAttunementSync && sync.IsRaidAttuned(def.Type)
            });
        }
    }

    private static void SyncQuestItems(WowCharacterData sync, WowCharacter carto)
    {
        var allItems = sync.Inventory.Concat(sync.Bank).ToList();
        var detected = DetectQuestItems(allItems);
        var previous = carto.QuestItems.ToDictionary(q => q.Type);

        carto.QuestItems.Clear();
        foreach (var type in Enum.GetValues<QuestItemType>())
        {
            var hasItem = detected.Contains(type);
            previous.TryGetValue(type, out var prev);

            if (!hasItem && prev == null)
                continue;

            carto.QuestItems.Add(new QuestItemEntry
            {
                Type = type,
                HasItem = hasItem,
                PlannedTurnIn = prev?.PlannedTurnIn,
                Note = prev?.Note
            });
        }
    }

    public static HashSet<QuestItemType> DetectQuestItems(IEnumerable<WowItem> items)
    {
        var found = new HashSet<QuestItemType>();
        foreach (var item in items)
        {
            var type = MatchQuestItem(item);
            if (type != null)
                found.Add(type.Value);
        }

        return found;
    }

    public static QuestItemType? MatchQuestItem(WowItem item)
    {
        foreach (var rule in QuestItemRules)
        {
            if (rule.ItemIds.Contains(item.ItemId))
                return rule.Type;

            var norm = Normalize(item.Name);
            if (rule.Type == QuestItemType.Tete_de_Rend
                && norm.Contains("rend")
                && (norm.Contains("tete") || norm.Contains("head") || norm.Contains("blackhand")))
                return rule.Type;

            if (rule.Type == QuestItemType.Tete_dOnyxia
                && norm.Contains("onyxia")
                && (norm.Contains("tete") || norm.Contains("head")))
                return rule.Type;

            if (rule.Type == QuestItemType.Tete_de_Nefarian
                && norm.Contains("nefarian")
                && (norm.Contains("tete") || norm.Contains("head")))
                return rule.Type;

            if (rule.Type == QuestItemType.Coeur_de_Hakkar
                && norm.Contains("hakkar")
                && (norm.Contains("coeur") || norm.Contains("heart")))
                return rule.Type;
        }

        return null;
    }

    public static int CountSoulShards(WowCharacterData sync)
    {
        return sync.Inventory.Concat(sync.Bank)
            .Where(IsSoulShard)
            .Sum(i => Math.Max(1, i.Count));
    }

    public static bool IsSoulShard(WowItem item) =>
        item.ItemId == SoulShardItemId
        || Normalize(item.Name).Contains("fragment")
           && (Normalize(item.Name).Contains("ame") || Normalize(item.Name).Contains("soul"));

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var lower = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(lower.Length);
        foreach (var c in lower)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Replace("'", "").Replace("’", "");
    }
}
