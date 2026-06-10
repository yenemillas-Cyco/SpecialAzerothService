namespace SpecialAzerothService.Core.Services;

public static partial class Tier3QuestCatalog
{
    public const string QuestIdBl = "BL";
    public const string ProfessionIdPrefixBl = "BL:";

    public const int SnickerfangJowl = 8391;
    public const int BlastedBoarLung = 8392;
    public const int ScorpokPincer = 8393;
    public const int BasiliskBrain = 8394;
    public const int VultureGizzard = 8396;

    /// <summary>Consommable TF fini (lié) — utilisé notamment pour l'Arcanum de constitution.</summary>
    public const int LungJuiceCocktail = 8412;

    private static QuestClassSet BuildBlDrazialGroup() =>
        new(null, "Mage de sang Drazial", BuildBlDrazialPieces(), "Terres Foudroyées");

    private static QuestClassSet BuildBlLynnoreGroup() =>
        new(null, "Mage de sang Lynnore", BuildBlLynnorePieces(), "Terres Foudroyées");

    private static IReadOnlyList<QuestPieceRecipe> BuildBlDrazialPieces() =>
    [
        BlPiece(
            "R.O.I.D.S.", 8410,
            "Augmente votre Force de 25 pendant 1 h.",
            "La colère des âges",
            "Composants : joues de Raillecroc (Raillecroc affamée, Hyène Raillecroc), poumons de sanglier (Sanglier infernal, Sanglier cendré), pinces de scorpok (Aiguillonneur scorpok).",
            SnickerfangJowl, 3, "Joues de Raillecroc",
            BlastedBoarLung, 2, "Poumon de sanglier éclaté",
            ScorpokPincer, 1, "Pince de scorpok"),

        BlPiece(
            "Poudre de scorpok terrestre", 8411,
            "Augmente votre Agilité de 25 pendant 1 h.",
            "Le sel de scorpok",
            "Composants : pinces de scorpok (Aiguillonneur scorpok), gésiers de vautour (Pourfendeur noir, Racleur-d'os), poumons de sanglier (Sanglier infernal, Sanglier cendré).",
            ScorpokPincer, 3, "Pince de scorpok",
            VultureGizzard, 2, "Gésier de vautour",
            BlastedBoarLung, 1, "Poumon de sanglier éclaté"),

        BlPiece(
            "Cocktail de jus de poumon", 8412,
            "Augmente votre Endurance de 25 pendant 1 h.",
            "L'esprit du sanglier",
            "Composants : poumons de sanglier (Sanglier infernal, Sanglier cendré), pinces de scorpok (Aiguillonneur scorpok), cerveaux de basilic (Basilic Rougepierre, Peau de cristal Rougepierre).",
            BlastedBoarLung, 3, "Poumon de sanglier éclaté",
            ScorpokPincer, 2, "Pince de scorpok",
            BasiliskBrain, 1, "Cerveau de basilic")
    ];

    private static IReadOnlyList<QuestPieceRecipe> BuildBlLynnorePieces() =>
    [
        BlPiece(
            "Potion de cortex cérébral", 8423,
            "Augmente votre Intelligence de 25 pendant 1 h.",
            "Un esprit infaillible",
            "Composants : cerveaux de basilic (Basilic Rougepierre, Peau de cristal Rougepierre), gésiers de vautour (Pourfendeur noir, Racleur-d'os).",
            BasiliskBrain, 10, "Cerveau de basilic",
            VultureGizzard, 2, "Gésier de vautour"),

        BlPiece(
            "Gomme de gésier", 8424,
            "Augmente votre Esprit de 25 pendant 1 h.",
            "Domination spirituelle",
            "Composants : gésiers de vautour (Pourfendeur noir, Racleur-d'os), joues de Raillecroc (Raillecroc affamée, Hyène Raillecroc).",
            VultureGizzard, 10, "Gésier de vautour",
            SnickerfangJowl, 2, "Joues de Raillecroc")
    ];

    private static QuestPieceRecipe BlPiece(
        string consumableNameFr,
        int resultItemId,
        string effectDescriptionFr,
        string questNameFr,
        string farmHintFr,
        int mat1Id, int mat1Qty, string mat1Name,
        int mat2Id, int mat2Qty, string mat2Name,
        int mat3Id = 0, int mat3Qty = 0, string? mat3Name = null)
    {
        var materials = new List<Tier3Material>
        {
            new(mat1Id, mat1Qty, mat1Name),
            new(mat2Id, mat2Qty, mat2Name)
        };

        if (mat3Id > 0 && mat3Qty > 0 && !string.IsNullOrWhiteSpace(mat3Name))
            materials.Add(new Tier3Material(mat3Id, mat3Qty, mat3Name));

        return new QuestPieceRecipe(
            null,
            questNameFr,
            consumableNameFr,
            resultItemId,
            farmHintFr,
            materials,
            effectDescriptionFr);
    }
}
