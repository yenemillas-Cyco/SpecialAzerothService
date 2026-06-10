using SpecialAzerothService.Core.Models.Reputation;

namespace SpecialAzerothService.Core.Services;

public static partial class ReputationTurnInCatalog
{
    public const int IncendosaurScaleItemId = 18944;
    public const int CoalItemId = 3857;
    public const int IronBarItemId = 3575;
    public const int KingsbloodItemId = 3356;
    public const int HeavyLeatherItemId = 4234;
    public const int DarkIronResidueItemId = 18945;
    public const int DarkIronOreItemId = 11370;
    public const int CoreLeatherItemId = 17012;
    public const int FieryCoreItemId = 17010;
    public const int LavaCoreItemId = 17011;
    public const int BloodOfTheMountainItemId = 11382;

    private static readonly ReputationTurnInRequirement TwoIncendosaurScales = new()
    {
        ItemId = IncendosaurScaleItemId,
        NameFr = "Écaille d'Incendosaure",
        QuantityPerTurnIn = 2,
    };

    private static readonly ReputationTurnInRequirement OneCoal = new()
    {
        ItemId = CoalItemId,
        NameFr = "Charbon",
        QuantityPerTurnIn = 1,
    };

    private static readonly string[] HonoredVariantRouteIds =
    [
        "Honored_Ore",
        "Honored_CoreLeather",
        "Honored_FieryCore",
        "Honored_LavaCore",
        "Honored_BloodMountain",
    ];

    private static ReputationTurnInRoute ThoriumPhase1Route(
        string routeId,
        string variantLabelFr,
        int altItemId,
        string altNameFr,
        int altQuantity) =>
        new()
        {
            RouteId = routeId,
            Method = ReputationTurnInMethod.TurnIn,
            LabelFr = $"Neutre → Amical — {variantLabelFr}",
            VariantLabelFr = variantLabelFr,
            DescriptionFr =
                $"2 écailles + 1 charbon + {altQuantity}× {altNameFr} par remise = 25 rép.",
            BaseReputation = 25,
            ItemUnitLabelFr = "remises",
            Requirements =
            [
                TwoIncendosaurScales,
                OneCoal,
                new ReputationTurnInRequirement
                {
                    ItemId = altItemId,
                    NameFr = altNameFr,
                    QuantityPerTurnIn = altQuantity,
                },
            ],
        };

    private static ReputationTurnInRoute ThoriumHonoredRoute(
        string routeId,
        string variantLabelFr,
        string turnInDescriptionFr,
        int itemId,
        string nameFr,
        int quantity,
        int reputation) =>
        new()
        {
            RouteId = routeId,
            Method = ReputationTurnInMethod.TurnIn,
            LabelFr = $"Honoré+ — {variantLabelFr}",
            VariantLabelFr = variantLabelFr,
            DescriptionFr = turnInDescriptionFr,
            BaseReputation = reputation,
            ItemUnitLabelFr = "remises",
            Requirements =
            [
                new ReputationTurnInRequirement
                {
                    ItemId = itemId,
                    NameFr = nameFr,
                    QuantityPerTurnIn = quantity,
                },
            ],
        };

    public static readonly ReputationFarmDefinition ThoriumBrotherhood = new()
    {
        Id = "ThoriumBrotherhood",
        FactionNameFr = "Confrérie du Thorium",
        LocationFr = "Poste du Thorium / Profondeurs de Rochenoire",
        NpcNameFr = "Maître forgeron Burninate / Lokhtos Sombrelige",
        NotesFr =
            "Choisissez le palier visé : les composants affichés correspondent au total pour ce palier. "
            + "Quêtes (~700 rép.) et forgeron Gadgetzan (+1150) non inclus.",
        Tiers =
        [
            new ReputationFarmTier
            {
                TierId = "Neutral_Friendly",
                LabelFr = "Neutre → Amical",
                ReputationNeeded = 3000,
                DescriptionFr = "Maître forgeron Burninate (Poste du Thorium), niveau 48+.",
                DefaultVariantRouteId = "Neutral_Iron",
                VariantRouteIds = ["Neutral_Iron", "Neutral_Kingsblood", "Neutral_Leather"],
            },
            new ReputationFarmTier
            {
                TierId = "Friendly_Honored",
                LabelFr = "Amical → Honoré",
                ReputationNeeded = 6000,
                DescriptionFr = "Burninate — 4 résidus de sombrefer par remise (25 rép.).",
                DefaultVariantRouteId = "Friendly_Residue",
                VariantRouteIds = ["Friendly_Residue"],
            },
            new ReputationFarmTier
            {
                TierId = "Honored_Revered",
                LabelFr = "Honoré → Révéré",
                ReputationNeeded = 12000,
                DescriptionFr = "Lokhtos Sombrelige (bar de BRD), niveau 60+.",
                DefaultVariantRouteId = "Honored_Ore",
                VariantRouteIds = HonoredVariantRouteIds,
            },
            new ReputationFarmTier
            {
                TierId = "Revered_Exalted",
                LabelFr = "Révéré → Exalté",
                ReputationNeeded = 21000,
                DescriptionFr = "Lokhtos Sombrelige — mêmes types de remises qu'Honoré → Révéré.",
                DefaultVariantRouteId = "Honored_Ore",
                VariantRouteIds = HonoredVariantRouteIds,
            },
        ],
        Routes =
        [
            ThoriumPhase1Route("Neutral_Iron", "Barres de fer", IronBarItemId, "Barre de fer", 4),
            ThoriumPhase1Route("Neutral_Kingsblood", "Sang-royal", KingsbloodItemId, "Sang-royal", 4),
            ThoriumPhase1Route("Neutral_Leather", "Cuir lourd", HeavyLeatherItemId, "Cuir lourd", 10),
            new ReputationTurnInRoute
            {
                RouteId = "Friendly_Residue",
                Method = ReputationTurnInMethod.TurnIn,
                LabelFr = "Amical → Honoré — Résidu de sombrefer",
                VariantLabelFr = "Résidu de sombrefer",
                DescriptionFr = "4 résidus de sombrefer par remise = 25 rép.",
                BaseReputation = 25,
                ItemUnitLabelFr = "remises",
                Requirements =
                [
                    new ReputationTurnInRequirement
                    {
                        ItemId = DarkIronResidueItemId,
                        NameFr = "Résidu de sombrefer",
                        QuantityPerTurnIn = 4,
                    },
                ],
            },
            ThoriumHonoredRoute(
                "Honored_Ore",
                "Minerai de sombrefer",
                "10 minerais de sombrefer = 50 rép.",
                DarkIronOreItemId,
                "Minerai de sombrefer",
                10,
                50),
            ThoriumHonoredRoute(
                "Honored_CoreLeather",
                "Cuir du Magma",
                "2 cuirs du Magma = 150 rép.",
                CoreLeatherItemId,
                "Cuir du Magma",
                2,
                150),
            ThoriumHonoredRoute(
                "Honored_FieryCore",
                "Noyau de feu",
                "1 noyau de feu = 200 rép.",
                FieryCoreItemId,
                "Noyau de feu",
                1,
                200),
            ThoriumHonoredRoute(
                "Honored_LavaCore",
                "Noyau de lave",
                "1 noyau de lave = 200 rép.",
                LavaCoreItemId,
                "Noyau de lave",
                1,
                200),
            ThoriumHonoredRoute(
                "Honored_BloodMountain",
                "Sang de montagne",
                "1 sang de montagne = 200 rép.",
                BloodOfTheMountainItemId,
                "Sang de montagne",
                1,
                200),
        ],
    };
}
