namespace SpecialAzerothService.Core.Services;

/// <summary>
/// Quêtes Libram → Arcanum (Burning Steppes + Hache-Tripes).
/// Source : https://www.wowhead.com/classic/guide/arcanums-gear-enchants-classic-wow
/// </summary>
public static partial class Tier3QuestCatalog
{
    public const string QuestIdArcanum = "ARCANUM";
    public const string ProfessionIdPrefixArcanum = "ARCANUM:";

    public const int GoldCostLesserArcanumCopper = 300_000; // 30 or

    // Résultats — arcanums inférieurs
    public const int LesserArcanumConstitution = 11642;
    public const int LesserArcanumResilience = 11643;
    public const int LesserArcanumRumination = 11644;
    public const int LesserArcanumTenacity = 11645;
    public const int LesserArcanumVoracity = 11647;

    // Résultats — arcanums (Lorekeeper Lydros) — IDs Classic officiels
    public const int ArcanumRapidity = 18329;
    public const int ArcanumFocus = 18330;
    public const int ArcanumProtection = 18331;

    // Composants partagés
    public const int BlackDiamond = 11754;
    public const int PristineBlackDiamond = 18335;
    public const int LargeBrilliantShard = 14344;

    // Librams inférieurs (11732–11734, 11736–11737 — pas 11735 qui est un masque)
    public const int LibramRumination = 11732;
    public const int LibramConstitution = 11733;
    public const int LibramTenacity = 11734;
    public const int LibramResilience = 11736;
    public const int LibramVoracity = 11737;
    public const int LibramRapidity = 18332;
    public const int LibramFocus = 18333;
    public const int LibramProtection = 18334;

    // Composants arcanums inférieurs (échangeables — pool mule)
    public const int NightDragonBreath = 11952;
    public const int CrystalSpire = 11567;
    public const int CrystalWard = 11564;
    public const int CrystalForce = 11563;
    public const int WhipperRootTuber = 11951;

    /// <summary>Gomme de gésier (buff TF fini, quête rumination).</summary>
    public const int GizzardGum = 8424;

    /// <summary>Butins de quête BoP — non échangeables entre persos (composants liés).</summary>
    public const int BurningEssence = 11751;
    public const int BlackBloodOfTheTormented = 11752;
    public const int SkinOfShadow = 12753;
    public const int BloodOfHeroes = 12938;
    public const int FrayedAbominationStitching = 12735;
    public const int EyeOfKajal = 18336;

    private static QuestClassSet BuildLesserArcanumGroup() =>
        new(null, "Arcanums inférieurs (Mathredis Firestar)", BuildLesserArcanumPieces(), "Steppes Ardentes");

    private static QuestClassSet BuildGreaterArcanumGroup() =>
        new(null, "Arcanums (Lorekeeper Lydros)", BuildGreaterArcanumPieces(), "Hache-Tripes — bibliothèque");

    private static IReadOnlyList<QuestPieceRecipe> BuildLesserArcanumPieces() =>
    [
        ArcanumPiece(
            LesserArcanumConstitution,
            "Arcanum inférieur de constitution",
            "+100 points de vie (casque / jambières)",
            "Mathredis Firestar — Steppes Ardentes",
            GoldCostLesserArcanumCopper,
            BlackDiamond, 1, "Diamant noir",
            LungJuiceCocktail, 1, "Cocktail de jus de poumon",
            LibramConstitution, 1, "Libram de constitution",
            NightDragonBreath, 4, "Souffle de dragon nocturne"),

        ArcanumPiece(
            LesserArcanumResilience,
            "Arcanum inférieur de résilience",
            "+20 résistance au Feu",
            "Mathredis Firestar — Steppes Ardentes",
            GoldCostLesserArcanumCopper,
            BlackDiamond, 1, "Diamant noir",
            CrystalSpire, 4, "Cime de cristal",
            BurningEssence, 1, "Essence ardente",
            LibramResilience, 1, "Libram de résilience"),

        ArcanumPiece(
            LesserArcanumRumination,
            "Arcanum inférieur de rumination",
            "+150 mana",
            "Mathredis Firestar — Steppes Ardentes",
            GoldCostLesserArcanumCopper,
            BlackDiamond, 1, "Diamant noir",
            BlackBloodOfTheTormented, 1, "Sang noir du tourmenté",
            GizzardGum, 1, "Gomme de gésier",
            LibramRumination, 1, "Libram de rumination"),

        ArcanumPiece(
            LesserArcanumTenacity,
            "Arcanum inférieur de ténacité",
            "+125 armure",
            "Mathredis Firestar — Steppes Ardentes",
            GoldCostLesserArcanumCopper,
            BlackDiamond, 1, "Diamant noir",
            LibramTenacity, 1, "Libram de ténacité",
            CrystalWard, 4, "Gardien de cristal",
            EyeOfKajal, 1, "Oeil de Kajal"),

        ArcanumPiece(
            LesserArcanumVoracity,
            "Arcanum inférieur de voracité",
            "+8 à une caractéristique de base (FOR/AGI/END/INT/ESP)",
            "Mathredis Firestar — Steppes Ardentes",
            GoldCostLesserArcanumCopper,
            BlackDiamond, 1, "Diamant noir",
            LibramVoracity, 1, "Libram de voracité",
            WhipperRootTuber, 4, "Tubercule de fouetteur",
            CrystalForce, 4, "Force de cristal")
    ];

    private static IReadOnlyList<QuestPieceRecipe> BuildGreaterArcanumPieces() =>
    [
        ArcanumPiece(
            ArcanumRapidity,
            "Arcanum de rapidité",
            "+1 % hâte",
            "Lorekeeper Lydros — bibliothèque Hache-Tripes (phase 2+)",
            goldCopper: 0,
            LibramRapidity, 1, "Libram de rapidité",
            PristineBlackDiamond, 1, "Diamant noir impeccable",
            LargeBrilliantShard, 2, "Grand éclat brillant",
            BloodOfHeroes, 2, "Sang de héros"),

        ArcanumPiece(
            ArcanumFocus,
            "Arcanum de concentration",
            "+8 soins et dégâts des sorts",
            "Lorekeeper Lydros — bibliothèque Hache-Tripes (phase 2+)",
            goldCopper: 0,
            LibramFocus, 1, "Libram de concentration",
            PristineBlackDiamond, 1, "Diamant noir impeccable",
            LargeBrilliantShard, 4, "Grand éclat brillant",
            SkinOfShadow, 2, "Peau d'ombre"),

        ArcanumPiece(
            ArcanumProtection,
            "Arcanum de protection",
            "+1 % esquive",
            "Lorekeeper Lydros — bibliothèque Hache-Tripes (phase 2+)",
            goldCopper: 0,
            LibramProtection, 1, "Libram de protection",
            PristineBlackDiamond, 1, "Diamant noir impeccable",
            LargeBrilliantShard, 2, "Grand éclat brillant",
            FrayedAbominationStitching, 1, "Pièce d'abomination effilochée")
    ];

    private static QuestPieceRecipe ArcanumPiece(
        int resultItemId,
        string arcanumNameFr,
        string effectDescriptionFr,
        string npcHintFr,
        int goldCopper,
        int mat1Id, int mat1Qty, string mat1Name,
        int mat2Id, int mat2Qty, string mat2Name,
        int mat3Id, int mat3Qty, string mat3Name,
        int mat4Id = 0, int mat4Qty = 0, string? mat4Name = null)
    {
        var materials = new List<Tier3Material>
        {
            new(mat1Id, mat1Qty, mat1Name),
            new(mat2Id, mat2Qty, mat2Name),
            new(mat3Id, mat3Qty, mat3Name)
        };

        if (mat4Id > 0 && mat4Qty > 0 && !string.IsNullOrWhiteSpace(mat4Name))
            materials.Add(new Tier3Material(mat4Id, mat4Qty, mat4Name));

        return new QuestPieceRecipe(
            null,
            npcHintFr,
            arcanumNameFr,
            resultItemId,
            npcHintFr,
            materials,
            effectDescriptionFr,
            goldCopper);
    }
}
