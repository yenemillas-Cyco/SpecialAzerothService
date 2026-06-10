using SpecialAzerothService.Core.Models.Reputation;

namespace SpecialAzerothService.Core.Services;

public static partial class ReputationTurnInCatalog
{
    public const int CoreOfElementsItemId = 22527;
    public const int DarkIronScrapsItemId = 22528;
    public const int BoneFragmentsItemId = 22526;
    public const int CryptFiendPartsItemId = 22525;
    public const int SavageFrondItemId = 22529;

    public const int InsigniaOfTheDawnItemId = 22523;
    public const int InsigniaOfTheCrusadeItemId = 22524;

    public const int ArgentDawnRepeatableRepPerTurnIn = 10;
    public const int ArgentDawnFirstTurnInRep = 200;

    private static readonly ReputationTurnInItem DawnInsigniaReward = new()
    {
        ItemId = InsigniaOfTheDawnItemId,
        NameFr = "Insigne de l'Aube",
    };

    private static readonly ReputationTurnInItem CrusadeInsigniaReward = new()
    {
        ItemId = InsigniaOfTheCrusadeItemId,
        NameFr = "Insigne de la Croisade",
    };

    private static readonly string[] ArgentDawnDropVariantRouteIds =
    [
        "AD_Core",
        "AD_DarkIron",
        "AD_Bone",
        "AD_CryptFiend",
        "AD_SavageFrond",
    ];

    private static ReputationTurnInRoute ArgentDawnDropRoute(
        string routeId,
        string variantLabelFr,
        int itemId,
        string nameFr,
        string npcFr,
        IReadOnlyList<ReputationTurnInItem> rewards) =>
        new()
        {
            RouteId = routeId,
            Method = ReputationTurnInMethod.TurnIn,
            LabelFr = variantLabelFr,
            VariantLabelFr = variantLabelFr,
            DescriptionFr =
                $"30× {nameFr} · {ArgentDawnRepeatableRepPerTurnIn} rép./remise "
                + $"(1ʳᵉ : {ArgentDawnFirstTurnInRep} rép.) — {npcFr}",
            BaseReputation = ArgentDawnRepeatableRepPerTurnIn,
            ItemUnitLabelFr = "remises",
            Requirements =
            [
                new ReputationTurnInRequirement
                {
                    ItemId = itemId,
                    NameFr = nameFr,
                    QuantityPerTurnIn = 30,
                },
            ],
            TurnInRewards = rewards,
        };

    public static readonly ReputationFarmDefinition ArgentDawn = new()
    {
        Id = "ArgentDawn",
        FactionNameFr = "Aube d'argent",
        LocationFr = "Chapelle de l'Espoir de Lumière (Maleterres de l'Est)",
        NpcNameFr = "Angela Dosantos, Korfax, Rohan, Leopold, Rayne",
        NotesFr =
            "Cinq quêtes répétables de farm d'insignes à la Chapelle. "
            + "Échangez les insignes chez l'intendante Miranda (onglet Craft → quêtes).",
        Tiers =
        [
            new ReputationFarmTier
            {
                TierId = "DropTurnIns",
                LabelFr = "Remises butins",
                ReputationNeeded = 42000,
                DescriptionFr =
                    "30 objets par remise — +1 insigne. "
                    + "Première remise de chaque type : 200 rép. ; suivantes : 10 rép.",
                DefaultVariantRouteId = "AD_Core",
                VariantRouteIds = ArgentDawnDropVariantRouteIds,
            },
        ],
        Routes =
        [
            ArgentDawnDropRoute(
                "AD_Core",
                "Noyaux des éléments",
                CoreOfElementsItemId,
                "Noyau des éléments",
                "Archimage Angela Dosantos",
                [DawnInsigniaReward]),
            ArgentDawnDropRoute(
                "AD_DarkIron",
                "Morceaux de sombrefer",
                DarkIronScrapsItemId,
                "Morceaux de sombrefer",
                "Korfax, Champion de la Lumière",
                [DawnInsigniaReward]),
            ArgentDawnDropRoute(
                "AD_Bone",
                "Fragments d'os",
                BoneFragmentsItemId,
                "Fragments d'os",
                "Rohan l'Assassin",
                [CrusadeInsigniaReward]),
            ArgentDawnDropRoute(
                "AD_CryptFiend",
                "Morceaux de démon des cryptes",
                CryptFiendPartsItemId,
                "Morceaux de démon des cryptes",
                "Veneur Leopold",
                [CrusadeInsigniaReward]),
            ArgentDawnDropRoute(
                "AD_SavageFrond",
                "Palmes sauvages",
                SavageFrondItemId,
                "Palme sauvage",
                "Rayne",
                [DawnInsigniaReward, CrusadeInsigniaReward]),
        ],
    };
}
