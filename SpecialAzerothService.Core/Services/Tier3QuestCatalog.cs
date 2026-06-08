using SpecialAzerothService.Core.Models.Carto;

namespace SpecialAzerothService.Core.Services;

/// <summary>Quêtes d'équipement (T3 Naxxramas, extensible).</summary>
public static partial class Tier3QuestCatalog
{
    public const string QuestIdT3 = "T3";
    public const string ProfessionIdPrefix = "T3:";

    public const int ArcaniteBar = 12360;
    public const int ArcaneCrystal = 12363;
    public const int CuredRuggedHide = 15407;
    public const int Mooncloth = 14342;
    public const int NexusCrystal = 20725;
    // IDs Classic (Naxxramas) — ne pas confondre avec 22682+ (autres objets, ex. patrons).
    public const int WartornClothScrap = 22376;
    public const int WartornLeatherScrap = 22373;
    public const int WartornChainScrap = 22374;
    public const int WartornPlateScrap = 22375;

    private static readonly Lazy<IReadOnlyList<QuestCategoryDefinition>> CategoriesCache = new(BuildCategories);

    public static IReadOnlyList<QuestCategoryDefinition> Categories => CategoriesCache.Value;

    private static IReadOnlyList<QuestCategoryDefinition> BuildCategories() =>
    [
        new QuestCategoryDefinition(
            QuestIdT3,
            "T3",
            "Naxxramas — set T3",
            "Composants de quête par pièce (jetons boss exclus).",
            BuildT3Classes()),
        new QuestCategoryDefinition(
            QuestIdEko,
            "E'ko",
            "Winterspring — consommables Juju",
            "Cache de Mau'ari requis pour farmer les E'ko. Chaque quête : 3 E'ko → 1 Juju chez le Sorcier-docteur Mau'ari (Long-Guet).",
            [BuildEkoGroup()]),
        new QuestCategoryDefinition(
            QuestIdBl,
            "Terres Foudroyées",
            "Terres Foudroyées — consommables",
            "Nord de la zone, à l'ouest de la route : Mages de sang Drazial et Lynnore. Quêtes répétables — consommables liés, uniques (+25 stat, 1 h).",
            [BuildBlDrazialGroup(), BuildBlLynnoreGroup()])
    ];

    public static QuestCategoryDefinition? FindCategory(string questId) =>
        Categories.FirstOrDefault(c => c.Id.Equals(questId, StringComparison.OrdinalIgnoreCase));

    public static QuestClassSet? FindClass(string questId, WowClass wowClass) =>
        FindCategory(questId)?.Classes.FirstOrDefault(c => c.Class == wowClass);

    public static string ProfessionId(WowClass wowClass, Tier3ArmorSlot? slot = null) =>
        slot == null
            ? $"{ProfessionIdPrefix}{wowClass}"
            : $"{ProfessionIdPrefix}{wowClass}:{slot}";

    public static string ProfessionIdEko(int resultItemId) => $"{ProfessionIdPrefixEko}{resultItemId}";

    public static string ProfessionIdBl(int resultItemId) => $"{ProfessionIdPrefixBl}{resultItemId}";

    public static string ProfessionLabelEko(int resultItemId)
    {
        var name = FindResultDisplayName(resultItemId);
        return string.IsNullOrEmpty(name) ? "Quête E'ko" : $"Quête E'ko — {name}";
    }

    public static bool TryParseEkoProfessionId(string? professionId, out int resultItemId) =>
        TryParsePrefixedQuestProfessionId(professionId, ProfessionIdPrefixEko, out resultItemId);

    public static bool TryParseBlProfessionId(string? professionId, out int resultItemId) =>
        TryParsePrefixedQuestProfessionId(professionId, ProfessionIdPrefixBl, out resultItemId);

    public static string ProfessionLabelBl(int resultItemId)
    {
        var name = FindResultDisplayName(resultItemId);
        return string.IsNullOrEmpty(name) ? "Quête Terres Foudroyées" : $"Quête TF — {name}";
    }

    public static string ProfessionIdForQuestPiece(
        string questCategoryId,
        WowClass? wowClass,
        Tier3ArmorSlot? slot,
        int resultItemId) =>
        questCategoryId switch
        {
            _ when questCategoryId.Equals(QuestIdEko, StringComparison.OrdinalIgnoreCase) => ProfessionIdEko(resultItemId),
            _ when questCategoryId.Equals(QuestIdBl, StringComparison.OrdinalIgnoreCase) => ProfessionIdBl(resultItemId),
            _ => ProfessionId(wowClass!.Value, slot!.Value)
        };

    public static string QuestAddStatusMessage(string questCategoryId, string pieceNameFr) =>
        questCategoryId switch
        {
            _ when questCategoryId.Equals(QuestIdEko, StringComparison.OrdinalIgnoreCase) =>
                $"{pieceNameFr} ajouté — les E'ko apparaissent dans Matériaux.",
            _ when questCategoryId.Equals(QuestIdBl, StringComparison.OrdinalIgnoreCase) =>
                $"{pieceNameFr} ajouté — les composants de farm apparaissent dans Matériaux.",
            _ => $"{pieceNameFr} ajouté — les composants apparaissent dans Matériaux."
        };

    public static string QuestCategoryHint(string questCategoryId) =>
        questCategoryId switch
        {
            _ when questCategoryId.Equals(QuestIdEko, StringComparison.OrdinalIgnoreCase) =>
                "Sélectionnez un Juju — 3 E'ko seront calculés dans Matériaux.",
            _ when questCategoryId.Equals(QuestIdBl, StringComparison.OrdinalIgnoreCase) =>
                "Choisissez un consommable — les organes de farm seront calculés dans Matériaux.",
            _ => "Choisissez une classe, puis ajoutez les pièces souhaitées."
        };

    private static bool TryParsePrefixedQuestProfessionId(string? professionId, string prefix, out int resultItemId)
    {
        resultItemId = 0;
        if (string.IsNullOrEmpty(professionId)
            || !professionId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(professionId[prefix.Length..], out resultItemId);
    }

    public static bool TryParseProfessionId(string? professionId, out WowClass wowClass, out Tier3ArmorSlot? slot)
    {
        wowClass = default;
        slot = null;
        if (string.IsNullOrEmpty(professionId)
            || !professionId.StartsWith(ProfessionIdPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = professionId[ProfessionIdPrefix.Length..];
        var parts = rest.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !Enum.TryParse(parts[0], ignoreCase: true, out wowClass))
            return false;

        if (parts.Length == 1)
            return true;

        if (!Enum.TryParse(parts[1], ignoreCase: true, out Tier3ArmorSlot parsedSlot))
            return false;

        slot = parsedSlot;
        return true;
    }

    public static string ProfessionLabel(WowClass wowClass, Tier3ArmorSlot? slot = null)
    {
        var className = GetClassNameFr(wowClass);
        if (slot == null)
            return $"Quête T3 — {className}";

        return $"Quête T3 — {className} ({GetSlotLabelFr(slot.Value)})";
    }

    public static string GetClassNameFr(WowClass wowClass) => wowClass switch
    {
        WowClass.Guerrier => "Guerrier",
        WowClass.Paladin => "Paladin",
        WowClass.Chasseur => "Chasseur",
        WowClass.Voleur => "Voleur",
        WowClass.Pretre => "Prêtre",
        WowClass.Chaman => "Chaman",
        WowClass.Mage => "Mage",
        WowClass.Demoniste => "Démoniste",
        WowClass.Druide => "Druide",
        _ => wowClass.ToString()
    };

    public static string GetSlotLabelFr(Tier3ArmorSlot slot) => slot switch
    {
        Tier3ArmorSlot.Wrist => "Poignets",
        Tier3ArmorSlot.Belt => "Ceinture",
        Tier3ArmorSlot.Hands => "Mains",
        Tier3ArmorSlot.Feet => "Pieds",
        Tier3ArmorSlot.Shoulders => "Épaules",
        Tier3ArmorSlot.Head => "Tête",
        Tier3ArmorSlot.Legs => "Jambes",
        Tier3ArmorSlot.Chest => "Torse",
        _ => slot.ToString()
    };

    public static string? FindMaterialDisplayName(int itemId)
    {
        foreach (var category in Categories)
        {
            foreach (var cls in category.Classes)
            {
                foreach (var piece in cls.Pieces)
                {
                    var mat = piece.Materials.FirstOrDefault(m => m.ItemId == itemId);
                    if (mat != null)
                        return mat.DisplayNameFr;
                }
            }
        }

        return null;
    }

    public static string? FindResultDisplayName(int itemId)
    {
        if (TryFindPieceByResultItemId(itemId, out var piece, out _))
            return piece!.PieceNameFr;
        return null;
    }

    public static bool TryFindPieceByResultItemId(int itemId, out QuestPieceRecipe? piece, out WowClass wowClass)
    {
        foreach (var category in Categories)
        {
            foreach (var cls in category.Classes)
            {
                foreach (var candidate in cls.Pieces)
                {
                    if (candidate.ResultItemId != itemId)
                        continue;

                    piece = candidate;
                    wowClass = cls.Class ?? default;
                    return true;
                }
            }
        }

        piece = null;
        wowClass = default;
        return false;
    }
}

public enum Tier3ArmorSlot
{
    Wrist,
    Belt,
    Hands,
    Feet,
    Shoulders,
    Head,
    Legs,
    Chest
}

public sealed record Tier3Material(int ItemId, int Quantity, string DisplayNameFr);

public sealed record QuestPieceRecipe(
    Tier3ArmorSlot? Slot,
    string SlotLabelFr,
    string PieceNameFr,
    int ResultItemId,
    string DesecratedTokenFr,
    IReadOnlyList<Tier3Material> Materials,
    string? EffectDescriptionFr = null)
{
    public string MaterialsSummary =>
        string.Join(" · ", Materials.Select(m => $"{m.DisplayNameFr} ×{m.Quantity}"));

    public string DisplayDescription =>
        string.IsNullOrWhiteSpace(EffectDescriptionFr)
            ? ""
            : $"{PieceNameFr} : {EffectDescriptionFr}";
}

public sealed record QuestClassSet(
    WowClass? Class,
    string SetNameFr,
    IReadOnlyList<QuestPieceRecipe> Pieces,
    string? GroupTitleFr = null);

public sealed record QuestCategoryDefinition(
    string Id,
    string ShortTitleFr,
    string TitleFr,
    string DescriptionFr,
    IReadOnlyList<QuestClassSet> Classes);
