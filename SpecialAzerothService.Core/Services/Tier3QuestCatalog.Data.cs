using SpecialAzerothService.Core.Models.Carto;

namespace SpecialAzerothService.Core.Services;

public static partial class Tier3QuestCatalog
{
    private static readonly IReadOnlyList<QuestPieceRecipe> ClothCasterPieces =
    [
        ClothTemplatePiece(Tier3ArmorSlot.Wrist, "Poignets", "Brassards", "Brassards profanés", WartornClothScrap, 6,
            ArcaneCrystal, 1, NexusCrystal, 1),
        ClothTemplatePiece(Tier3ArmorSlot.Belt, "Ceinture", "Ceinture", "Ceinture profanée", WartornClothScrap, 8,
            ArcaneCrystal, 2, Mooncloth, 2),
        ClothTemplatePiece(Tier3ArmorSlot.Hands, "Mains", "Gants", "Gants profanés", WartornClothScrap, 8,
            Mooncloth, 4),
        ClothTemplatePiece(Tier3ArmorSlot.Feet, "Pieds", "Sandales", "Sandales profanées", WartornClothScrap, 12,
            Mooncloth, 2, CuredRuggedHide, 3),
        ClothTemplatePiece(Tier3ArmorSlot.Shoulders, "Épaules", "Spallières", "Spallières profanées", WartornClothScrap, 12,
            Mooncloth, 2, CuredRuggedHide, 3),
        ClothTemplatePiece(Tier3ArmorSlot.Head, "Tête", "Diadème", "Diadème profané", WartornClothScrap, 15,
            Mooncloth, 3, NexusCrystal, 3),
        ClothTemplatePiece(Tier3ArmorSlot.Legs, "Jambes", "Jambières", "Jambières profanées", WartornClothScrap, 20,
            Mooncloth, 4, NexusCrystal, 2),
        ClothTemplatePiece(Tier3ArmorSlot.Chest, "Torse", "Robe", "Robe profanée", WartornClothScrap, 25,
            Mooncloth, 4, NexusCrystal, 2)
    ];

    private static IReadOnlyList<QuestPieceRecipe> ClothPiecesFor(WowClass wowClass, string setSuffix) =>
        ClothCasterPieces.Select(p => p with
        {
            PieceNameFr = $"{p.PieceNameFr} {setSuffix}",
            ResultItemId = Tier3ResultItemIds.Get(wowClass, p.Slot!.Value)
        }).ToList();

    private static QuestClassSet ClassSet(WowClass wowClass, string setNameFr, IReadOnlyList<QuestPieceRecipe> pieces) =>
        new(wowClass, setNameFr, pieces);

    private static IReadOnlyList<QuestClassSet> BuildT3Classes() =>
    [
        ClassSet(WowClass.Guerrier, "Tenue de combat de Cuirassier",
        [
            Piece(WowClass.Guerrier, Tier3ArmorSlot.Wrist, "Poignets", "Brassards de cuirassier", "Brassards profanés", WartornPlateScrap, 6, ArcaniteBar, 1, NexusCrystal, 1),
            Piece(WowClass.Guerrier, Tier3ArmorSlot.Belt, "Ceinture", "Garde-taille de cuirassier", "Ceinture profanée", WartornPlateScrap, 8, ArcaniteBar, 1, CuredRuggedHide, 5),
            Piece(WowClass.Guerrier, Tier3ArmorSlot.Hands, "Mains", "Gantelets de cuirassier", "Gants profanés", WartornPlateScrap, 8, ArcaniteBar, 1, CuredRuggedHide, 5),
            Piece(WowClass.Guerrier, Tier3ArmorSlot.Feet, "Pieds", "Sabatons de cuirassier", "Bottes profanées", WartornPlateScrap, 12, ArcaniteBar, 2, CuredRuggedHide, 3),
            Piece(WowClass.Guerrier, Tier3ArmorSlot.Shoulders, "Épaules", "Espauliers de cuirassier", "Spallières profanées", WartornPlateScrap, 12, ArcaniteBar, 2, CuredRuggedHide, 3),
            Piece(WowClass.Guerrier, Tier3ArmorSlot.Head, "Tête", "Heaume de cuirassier", "Heaume profané", WartornPlateScrap, 15, ArcaniteBar, 5, NexusCrystal, 1),
            Piece(WowClass.Guerrier, Tier3ArmorSlot.Legs, "Jambes", "Cuissards de cuirassier", "Jambières profanées", WartornPlateScrap, 20, ArcaniteBar, 4, CuredRuggedHide, 3),
            Piece(WowClass.Guerrier, Tier3ArmorSlot.Chest, "Torse", "Cuirasse de cuirassier", "Cuirasse profanée", WartornPlateScrap, 25, ArcaniteBar, 4, NexusCrystal, 2)
        ]),

        ClassSet(WowClass.Paladin, "Armure de rédemption",
        [
            Piece(WowClass.Paladin, Tier3ArmorSlot.Wrist, "Poignets", "Brassards de rédemption", "Brassards profanés", WartornPlateScrap, 6, ArcaniteBar, 1, CuredRuggedHide, 2),
            Piece(WowClass.Paladin, Tier3ArmorSlot.Belt, "Ceinture", "Ceinture de rédemption", "Ceinture profanée", WartornPlateScrap, 8, ArcaniteBar, 1, NexusCrystal, 3),
            Piece(WowClass.Paladin, Tier3ArmorSlot.Hands, "Mains", "Gantelets de rédemption", "Gants profanés", WartornPlateScrap, 8, ArcaniteBar, 1, CuredRuggedHide, 5),
            Piece(WowClass.Paladin, Tier3ArmorSlot.Feet, "Pieds", "Bottes de rédemption", "Bottes profanées", WartornPlateScrap, 12, ArcaniteBar, 2, CuredRuggedHide, 3),
            Piece(WowClass.Paladin, Tier3ArmorSlot.Shoulders, "Épaules", "Espauliers de rédemption", "Spallières profanées", WartornPlateScrap, 12, ArcaniteBar, 2, NexusCrystal, 2),
            Piece(WowClass.Paladin, Tier3ArmorSlot.Head, "Tête", "Heaume de rédemption", "Heaume profané", WartornPlateScrap, 15, ArcaniteBar, 5, CuredRuggedHide, 2),
            Piece(WowClass.Paladin, Tier3ArmorSlot.Legs, "Jambes", "Cuissards de rédemption", "Jambières profanées", WartornPlateScrap, 20, ArcaniteBar, 4, NexusCrystal, 2),
            Piece(WowClass.Paladin, Tier3ArmorSlot.Chest, "Torse", "Tunique de rédemption", "Tunique profanée", WartornPlateScrap, 25, ArcaniteBar, 4, CuredRuggedHide, 3)
        ]),

        ClassSet(WowClass.Voleur, "Armure de tranche-os",
        [
            Piece(WowClass.Voleur, Tier3ArmorSlot.Wrist, "Poignets", "Brassards de tranche-os", "Brassards profanés", WartornLeatherScrap, 6, CuredRuggedHide, 2, ArcaniteBar, 1),
            Piece(WowClass.Voleur, Tier3ArmorSlot.Belt, "Ceinture", "Ceinture de tranche-os", "Ceinture profanée", WartornLeatherScrap, 8, CuredRuggedHide, 5, NexusCrystal, 1),
            Piece(WowClass.Voleur, Tier3ArmorSlot.Hands, "Mains", "Gantelets de tranche-os", "Gants profanés", WartornLeatherScrap, 8, CuredRuggedHide, 5, ArcaniteBar, 1),
            Piece(WowClass.Voleur, Tier3ArmorSlot.Feet, "Pieds", "Bottes de tranche-os", "Bottes profanées", WartornLeatherScrap, 12, CuredRuggedHide, 3, NexusCrystal, 2),
            Piece(WowClass.Voleur, Tier3ArmorSlot.Shoulders, "Épaules", "Espauliers de tranche-os", "Spallières profanées", WartornLeatherScrap, 12, CuredRuggedHide, 5, NexusCrystal, 1),
            Piece(WowClass.Voleur, Tier3ArmorSlot.Head, "Tête", "Heaume de tranche-os", "Heaume profané", WartornLeatherScrap, 15, CuredRuggedHide, 8, NexusCrystal, 1),
            Piece(WowClass.Voleur, Tier3ArmorSlot.Legs, "Jambes", "Cuissards de tranche-os", "Jambières profanées", WartornLeatherScrap, 20, CuredRuggedHide, 8, ArcaniteBar, 1),
            Piece(WowClass.Voleur, Tier3ArmorSlot.Chest, "Torse", "Cuirasse de tranche-os", "Cuirasse profanée", WartornLeatherScrap, 25, CuredRuggedHide, 6, ArcaniteBar, 2)
        ]),

        ClassSet(WowClass.Druide, "Graccord de marcherêve",
        [
            Piece(WowClass.Druide, Tier3ArmorSlot.Wrist, "Poignets", "Protège-poignets de marcherêve", "Brassards profanés", WartornLeatherScrap, 6, ArcaneCrystal, 1, CuredRuggedHide, 2),
            Piece(WowClass.Druide, Tier3ArmorSlot.Belt, "Ceinture", "Ceinturon de marcherêve", "Ceinture profanée", WartornLeatherScrap, 8, Mooncloth, 3, CuredRuggedHide, 2),
            Piece(WowClass.Druide, Tier3ArmorSlot.Hands, "Mains", "Protège-mains de marcherêve", "Gants profanés", WartornLeatherScrap, 8, CuredRuggedHide, 5, NexusCrystal, 1),
            Piece(WowClass.Druide, Tier3ArmorSlot.Feet, "Pieds", "Bottes de marcherêve", "Bottes profanées", WartornLeatherScrap, 12, Mooncloth, 3, CuredRuggedHide, 2),
            Piece(WowClass.Druide, Tier3ArmorSlot.Shoulders, "Épaules", "Spallières de marcherêve", "Spallières profanées", WartornLeatherScrap, 12, CuredRuggedHide, 5, NexusCrystal, 1),
            Piece(WowClass.Druide, Tier3ArmorSlot.Head, "Tête", "Coiffe de marcherêve", "Heaume profané", WartornLeatherScrap, 15, CuredRuggedHide, 6, NexusCrystal, 2),
            Piece(WowClass.Druide, Tier3ArmorSlot.Legs, "Jambes", "Garde-jambes de marcherêve", "Jambières profanées", WartornLeatherScrap, 20, CuredRuggedHide, 8, NexusCrystal, 1),
            Piece(WowClass.Druide, Tier3ArmorSlot.Chest, "Torse", "Tunique de marcherêve", "Tunique profanée", WartornLeatherScrap, 25, CuredRuggedHide, 6, NexusCrystal, 2)
        ]),

        ClassSet(WowClass.Chasseur, "Armure de traqueur des cryptes",
        [
            Piece(WowClass.Chasseur, Tier3ArmorSlot.Wrist, "Poignets", "Garde-poignets de traqueur des cryptes", "Brassards profanés", WartornChainScrap, 6, ArcaniteBar, 1, CuredRuggedHide, 2),
            Piece(WowClass.Chasseur, Tier3ArmorSlot.Belt, "Ceinture", "Ceinturon de traqueur des cryptes", "Ceinture profanée", WartornChainScrap, 8, ArcaniteBar, 1, NexusCrystal, 3),
            Piece(WowClass.Chasseur, Tier3ArmorSlot.Hands, "Mains", "Garde-mains de traqueur des cryptes", "Gants profanés", WartornChainScrap, 8, ArcaniteBar, 1, CuredRuggedHide, 5),
            Piece(WowClass.Chasseur, Tier3ArmorSlot.Feet, "Pieds", "Bottes de traqueur des cryptes", "Bottes profanées", WartornChainScrap, 12, ArcaniteBar, 1, NexusCrystal, 3),
            Piece(WowClass.Chasseur, Tier3ArmorSlot.Shoulders, "Épaules", "Spallières de traqueur des cryptes", "Spallières profanées", WartornChainScrap, 12, ArcaniteBar, 2, CuredRuggedHide, 3),
            Piece(WowClass.Chasseur, Tier3ArmorSlot.Head, "Tête", "Coiffe de traqueur des cryptes", "Heaume profané", WartornChainScrap, 15, ArcaniteBar, 4, NexusCrystal, 2),
            Piece(WowClass.Chasseur, Tier3ArmorSlot.Legs, "Jambes", "Garde-jambes de traqueur des cryptes", "Jambières profanées", WartornChainScrap, 20, ArcaniteBar, 3, CuredRuggedHide, 5),
            Piece(WowClass.Chasseur, Tier3ArmorSlot.Chest, "Torse", "Tunique de traqueur des cryptes", "Tunique profanée", WartornChainScrap, 25, ArcaniteBar, 4, CuredRuggedHide, 3)
        ]),

        ClassSet(WowClass.Chaman, "Briseterre",
        [
            Piece(WowClass.Chaman, Tier3ArmorSlot.Wrist, "Poignets", "Garde-poignets de briseterre", "Brassards profanés", WartornChainScrap, 6, ArcaniteBar, 1, CuredRuggedHide, 2),
            Piece(WowClass.Chaman, Tier3ArmorSlot.Belt, "Ceinture", "Ceinturon de briseterre", "Ceinture profanée", WartornChainScrap, 8, ArcaniteBar, 1, NexusCrystal, 3),
            Piece(WowClass.Chaman, Tier3ArmorSlot.Hands, "Mains", "Garde-mains de briseterre", "Gants profanés", WartornChainScrap, 8, ArcaniteBar, 1, CuredRuggedHide, 5),
            Piece(WowClass.Chaman, Tier3ArmorSlot.Feet, "Pieds", "Bottes de briseterre", "Bottes profanées", WartornChainScrap, 12, ArcaniteBar, 1, NexusCrystal, 3),
            Piece(WowClass.Chaman, Tier3ArmorSlot.Shoulders, "Épaules", "Spallières de briseterre", "Spallières profanées", WartornChainScrap, 12, ArcaniteBar, 2, Mooncloth, 2),
            Piece(WowClass.Chaman, Tier3ArmorSlot.Head, "Tête", "Coiffe de briseterre", "Heaume profané", WartornChainScrap, 15, ArcaniteBar, 4, NexusCrystal, 2),
            Piece(WowClass.Chaman, Tier3ArmorSlot.Legs, "Jambes", "Garde-jambes de briseterre", "Jambières profanées", WartornChainScrap, 20, ArcaniteBar, 3, CuredRuggedHide, 5),
            Piece(WowClass.Chaman, Tier3ArmorSlot.Chest, "Torse", "Tunique de briseterre", "Tunique profanée", WartornChainScrap, 25, ArcaniteBar, 4, CuredRuggedHide, 3)
        ]),

        ClassSet(WowClass.Mage, "Grège de givrefeu", ClothPiecesFor(WowClass.Mage, "de givrefeu")),
        ClassSet(WowClass.Pretre, "Habit de foi", ClothPiecesFor(WowClass.Pretre, "de foi")),
        ClassSet(WowClass.Demoniste, "Grège du pestilence", ClothPiecesFor(WowClass.Demoniste, "du pestilence"))
    ];

    private static QuestPieceRecipe Piece(
        WowClass wowClass,
        Tier3ArmorSlot slot,
        string slotLabelFr,
        string pieceNameFr,
        string desecratedTokenFr,
        int scrapItemId,
        int scrapQty,
        params int[] extraMats) =>
        BuildPiece(slot, slotLabelFr, pieceNameFr, Tier3ResultItemIds.Get(wowClass, slot), desecratedTokenFr, scrapItemId, scrapQty, extraMats);

    private static QuestPieceRecipe ClothTemplatePiece(
        Tier3ArmorSlot slot,
        string slotLabelFr,
        string pieceNameFr,
        string desecratedTokenFr,
        int scrapItemId,
        int scrapQty,
        params int[] extraMats) =>
        BuildPiece(slot, slotLabelFr, pieceNameFr, 0, desecratedTokenFr, scrapItemId, scrapQty, extraMats);

    private static QuestPieceRecipe BuildPiece(
        Tier3ArmorSlot slot,
        string slotLabelFr,
        string pieceNameFr,
        int resultItemId,
        string desecratedTokenFr,
        int scrapItemId,
        int scrapQty,
        params int[] extraMats)
    {
        if (extraMats.Length % 2 != 0)
            throw new ArgumentException("Les matériaux supplémentaires doivent être des paires id/quantité.", nameof(extraMats));

        var list = new List<Tier3Material> { new(scrapItemId, scrapQty, ScrapName(scrapItemId)) };
        for (var i = 0; i < extraMats.Length; i += 2)
            list.Add(new Tier3Material(extraMats[i], extraMats[i + 1], MaterialName(extraMats[i])));
        return new QuestPieceRecipe(slot, slotLabelFr, pieceNameFr, resultItemId, desecratedTokenFr, list);
    }

    private static string ScrapName(int itemId) => itemId switch
    {
        WartornClothScrap => "Débris d'armure en tissu",
        WartornLeatherScrap => "Débris d'armure en cuir",
        WartornChainScrap => "Débris d'armure en mailles",
        WartornPlateScrap => "Débris d'armure en plaques",
        _ => $"#{itemId}"
    };

    private static string MaterialName(int itemId) => itemId switch
    {
        ArcaniteBar => "Barre d'arcanite",
        ArcaneCrystal => "Cristal arcanique",
        CuredRuggedHide => "Peau robuste traitée",
        Mooncloth => "Etoffe lunaire",
        NexusCrystal => "Cristal de nexus",
        _ => $"#{itemId}"
    };
}
