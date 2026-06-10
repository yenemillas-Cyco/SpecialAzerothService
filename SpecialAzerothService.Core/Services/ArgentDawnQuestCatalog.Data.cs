namespace SpecialAzerothService.Core.Services;

public static partial class Tier3QuestCatalog
{
    public const string QuestIdArgentDawn = "ArgentDawn";
    public const string ProfessionIdPrefixArgentDawn = "AD:";

    public const int InsigniaOfTheDawnItemId = ReputationTurnInCatalog.InsigniaOfTheDawnItemId;
    public const int InsigniaOfTheCrusadeItemId = ReputationTurnInCatalog.InsigniaOfTheCrusadeItemId;

    public const int SupplyBagItemId = 22679;
    public const int LeggingsOfThePlagueHunterItemId = 22690;
    public const int BandOfPietyItemId = 22681;
    public const int BandOfResolutionItemId = 22680;

    private static QuestClassSet BuildArgentDawnMirandaGroup() =>
        new(null, "Intendante Miranda Breechlock", BuildArgentDawnSuperiorPieces(), "Chapelle de l'Espoir de Lumière");

    private static IReadOnlyList<QuestPieceRecipe> BuildArgentDawnSuperiorPieces() =>
    [
        AdSuperiorPiece(
            "Armes de bataille excellentes — Ami de l'Aube",
            SupplyBagItemId,
            "Sac de fournitures",
            "Amical",
            30,
            30,
            "Récompense au choix : sac 18 places, bagues, jambières, casque, etc."),

        AdSuperiorPiece(
            "Armes de bataille excellentes — Honoré auprès de l'Aube",
            LeggingsOfThePlagueHunterItemId,
            "Jambières du chassepeste",
            "Honoré",
            20,
            20,
            "Mêmes récompenses qu'en Amical."),

        AdSuperiorPiece(
            "Armes de bataille excellentes — Révéré auprès de l'Aube",
            BandOfPietyItemId,
            "Bague de piété",
            "Révéré",
            7,
            7,
            "Récompense au choix parmi les armes de bataille excellentes."),

        AdSuperiorPiece(
            "Armes de bataille excellentes — Exalté auprès de l'Aube",
            BandOfResolutionItemId,
            "Bague de résolution",
            "Exalté",
            6,
            6,
            "Récompense au choix parmi les armes de bataille excellentes."),
    ];

    private static QuestPieceRecipe AdSuperiorPiece(
        string questNameFr,
        int resultItemId,
        string rewardExampleFr,
        string reputationFr,
        int dawnQty,
        int crusadeQty,
        string hintFr) =>
        new(
            null,
            reputationFr,
            $"{questNameFr} — {rewardExampleFr}",
            resultItemId,
            "",
            [
                new Tier3Material(InsigniaOfTheDawnItemId, dawnQty, "Insigne de l'Aube"),
                new Tier3Material(InsigniaOfTheCrusadeItemId, crusadeQty, "Insigne de la Croisade"),
            ],
            hintFr);
}
