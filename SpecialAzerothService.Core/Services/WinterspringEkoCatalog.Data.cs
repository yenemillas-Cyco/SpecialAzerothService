namespace SpecialAzerothService.Core.Services;

public static partial class Tier3QuestCatalog
{
    public const string QuestIdEko = "EKO";
    public const string ProfessionIdPrefixEko = "EKO:";

    private static QuestClassSet BuildEkoGroup() =>
        new(null, "7 quêtes Juju (3 E'ko chacune)", BuildEkoPieces(), "Winterspring");

    private static IReadOnlyList<QuestPieceRecipe> BuildEkoPieces() =>
    [
        EkoPiece(
            "Puissance de Juju", 12460,
            "Augmente votre puissance d'attaque de 40 pendant 10 min. (1 Min de recharge)",
            12436, "E'ko de Cognegivre",
            "Géants Cognegivre et Protecteurs Cognegivre — sud de Berceau-de-l'Hiver, au nord de la Gorge de Sombrefer."),

        EkoPiece(
            "Pouvoir de Juju", 12451,
            "Augmente votre Force de 30 pendant 30 min. (1 Min de recharge)",
            12431, "E'ko des Tombe-hiver",
            "Ursa, Chaman, Protecteur, Guide et Totémique Tombe-hiver — ouest de Berceau-de-l'Hiver, à l'est de Long-Guet."),

        EkoPiece(
            "Rafale de Juju", 12450,
            "Augmente votre score de hâte de 30 pendant 20 s. (1 Min de recharge)",
            12430, "E'ko de sabre-de-givre",
            "Sabres-de-givre, traqueurs, jeunes, chasseresses et guetteurs — nord de Berceau-de-l'Hiver, près du Rocher des Sabres-de-givre."),

        EkoPiece(
            "Fourberie de Juju", 12458,
            "Augmente votre Intelligence de 30 pendant 30 min. (1 Min de recharge)",
            12433, "E'ko d'Indomptable",
            "Chouettards berserk, lunaires, affolés, enragés et éreintés — partout dans Berceau-de-l'Hiver."),

        EkoPiece(
            "Fuite de Juju", 12459,
            "Augmente votre score d'esquive de 60 pendant 10 s. (1 Min de recharge)",
            12435, "E'ko de Chardon de glace",
            "Patriarches, matriarches et yétis Chardon de glace — à l'est de Long-Guet, près des grottes."),

        EkoPiece(
            "Frisson de Juju", 12457,
            "Augmente votre résistance au Givre de 15 pendant 10 min. (1 Min de recharge)",
            12434, "E'ko de Noroît",
            "Ravageurs, chimères et petites Noroîts — partout dans Berceau-de-l'Hiver."),

        EkoPiece(
            "Braise de Juju", 12455,
            "Augmente votre résistance au Feu de 15 pendant 10 min. (1 Min de recharge)",
            12432, "E'ko de Croc acéré",
            "Anciens, marteleurs, enragés et ours Crocs acérés — partout dans Berceau-de-l'Hiver.")
    ];

    private static QuestPieceRecipe EkoPiece(
        string jujuNameFr,
        int resultItemId,
        string effectDescriptionFr,
        int ekoItemId,
        string ekoNameFr,
        string farmHintFr) =>
        new(
            null,
            $"3 × {ekoNameFr}",
            jujuNameFr,
            resultItemId,
            farmHintFr,
            [new Tier3Material(ekoItemId, 3, ekoNameFr)],
            effectDescriptionFr);
}
