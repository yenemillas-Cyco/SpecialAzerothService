using SpecialAzerothService.Core.Models.Reputation;

namespace SpecialAzerothService.Core.Services;

public static partial class ReputationTurnInCatalog
{
    public const int DmfReputationPerTurnIn = 250;

    public const int SmallFurryPawItemId = 5134;
    public const int TornBearPeltItemId = 5135;
    public const int SoftBushyTailItemId = 5136;
    public const int VibrantPlumeItemId = 5137;
    public const int EvilBatEyeItemId = 5138;
    public const int GlowingScorpidBloodItemId = 2939;

    public const int CoarseWeightstoneItemId = 3240;
    public const int HeavyGrindingStoneItemId = 3486;
    public const int GreenIronBracersItemId = 3842;
    public const int BigBlackMaceItemId = 3815;
    public const int DenseGrindingStoneItemId = 12644;

    public const int CopperModulatorItemId = 4363;
    public const int WhirringBronzeGizmoItemId = 4375;
    public const int GreenFireworkItemId = 9318;
    public const int MechanicalRepairKitItemId = 11590;
    public const int ThoriumWidgetItemId = 15992;

    public const int EmbossedLeatherBootsItemId = 2307;
    public const int ToughenedLeatherArmorItemId = 4265;
    public const int BarbaricHarnessItemId = 5739;
    public const int TurtleScaleLeggingsItemId = 8185;
    public const int RuggedArmorKitItemId = 15564;

    private static readonly string[] YebbVariantRouteIds =
    [
        "Yebb_T1",
        "Yebb_T2",
        "Yebb_T3",
        "Yebb_T4",
        "Yebb_T5_Bat",
        "Yebb_T5_Scorp",
    ];

    private static readonly string[] KerriVariantRouteIds =
    [
        "Kerri_T1",
        "Kerri_T2",
        "Kerri_T3",
        "Kerri_T4",
        "Kerri_T5",
    ];

    private static readonly string[] RinlingVariantRouteIds =
    [
        "Rinling_T1",
        "Rinling_T2",
        "Rinling_T3",
        "Rinling_T4",
        "Rinling_T5",
    ];

    private static readonly string[] ChronosVariantRouteIds =
    [
        "Chronos_T1",
        "Chronos_T2",
        "Chronos_T3",
        "Chronos_T4",
        "Chronos_T5",
    ];

    private static ReputationTurnInRoute DmfQuestRoute(
        string routeId,
        string variantLabelFr,
        int itemId,
        string nameFr,
        int quantity,
        string cutoffNoteFr) =>
        new()
        {
            RouteId = routeId,
            Method = ReputationTurnInMethod.TurnIn,
            LabelFr = variantLabelFr,
            VariantLabelFr = variantLabelFr,
            DescriptionFr =
                $"{quantity}× {nameFr} par remise = {DmfReputationPerTurnIn} rép. {cutoffNoteFr}",
            BaseReputation = DmfReputationPerTurnIn,
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

    public static readonly ReputationFarmDefinition DarkmoonFaire = new()
    {
        Id = "DarkmoonFaire",
        FactionNameFr = "Foire de Sombrelune",
        LocationFr = "Forêt des Elwynn / Mulgore (tournante)",
        NpcNameFr = "Yebb Neblegear, Kerri Hicks, Rinling, Chronos",
        NotesFr =
            "250 rép. par remise (Classic Era). Paliers 1–4 : quêtes limitées selon la réputation "
            + "(remettre du palier le plus bas au plus haut). Palier 5 : répétable jusqu'à Honoré. "
            + "Tickets : 1 / 4 / 8 / 12 / 20 par palier.",
        Tiers =
        [
            new ReputationFarmTier
            {
                TierId = "Yebb",
                LabelFr = "Yebb — Pièces d'animaux",
                ReputationNeeded = 42000,
                DescriptionFr =
                    "Yebb Neblegear — Elwynn /way 40.2, 69.5 · Mulgore /way 37.5, 39.6. "
                    + "Accessible à tous les personnages.",
                DefaultVariantRouteId = "Yebb_T5_Bat",
                VariantRouteIds = YebbVariantRouteIds,
            },
            new ReputationFarmTier
            {
                TierId = "Kerri",
                LabelFr = "Kerri — Forge",
                ReputationNeeded = 42000,
                DescriptionFr =
                    "Kerri Hicks — Elwynn /way 40.5, 69.9 · Mulgore /way 37.8, 38.9. "
                    + "Recettes apprises chez le maître forgeron.",
                DefaultVariantRouteId = "Kerri_T5",
                VariantRouteIds = KerriVariantRouteIds,
            },
            new ReputationFarmTier
            {
                TierId = "Rinling",
                LabelFr = "Rinling — Ingénierie",
                ReputationNeeded = 42000,
                DescriptionFr =
                    "Rinling — Elwynn /way 41.7, 70.7 · Mulgore /way 37.1, 37.2. "
                    + "Schémas feu d'artifice vert et widget en thorium chez les vendeurs gobelins.",
                DefaultVariantRouteId = "Rinling_T5",
                VariantRouteIds = RinlingVariantRouteIds,
            },
            new ReputationFarmTier
            {
                TierId = "Chronos",
                LabelFr = "Chronos — Travail du cuir",
                ReputationNeeded = 42000,
                DescriptionFr =
                    "Chronos — Elwynn /way 41.5, 68.9 · Mulgore /way 37.2, 37.7. "
                    + "Recettes apprises chez le maître du cuir.",
                DefaultVariantRouteId = "Chronos_T5",
                VariantRouteIds = ChronosVariantRouteIds,
            },
        ],
        Routes =
        [
            DmfQuestRoute(
                "Yebb_T1",
                "Palier 1 — Pattes velues",
                SmallFurryPawItemId,
                "Patte velue",
                5,
                "(1 ticket · disparaît à 500/3000 Neutre)"),
            DmfQuestRoute(
                "Yebb_T2",
                "Palier 2 — Peaux d'ours",
                TornBearPeltItemId,
                "Peau d'ours déchirée",
                5,
                "(4 tickets · disparaît à 1100/3000 Neutre)"),
            DmfQuestRoute(
                "Yebb_T3",
                "Palier 3 — Queues touffues",
                SoftBushyTailItemId,
                "Queue touffue",
                5,
                "(8 tickets · disparaît à 1700/3000 Neutre)"),
            DmfQuestRoute(
                "Yebb_T4",
                "Palier 4 — Plumes vibrantes",
                VibrantPlumeItemId,
                "Plume vibrante",
                5,
                "(12 tickets · disparaît à 2500/3000 Neutre)"),
            DmfQuestRoute(
                "Yebb_T5_Bat",
                "Palier 5 — Yeux de chauve-souris",
                EvilBatEyeItemId,
                "Œil de chauve-souris vicieux",
                10,
                "(20 tickets · répétable)"),
            DmfQuestRoute(
                "Yebb_T5_Scorp",
                "Palier 5 — Sang de scorpide",
                GlowingScorpidBloodItemId,
                "Sang de scorpide luisant",
                10,
                "(20 tickets · répétable)"),

            DmfQuestRoute(
                "Kerri_T1",
                "Palier 1 — Pierres grossières",
                CoarseWeightstoneItemId,
                "Pierre à aiguiser grossière",
                10,
                "(1 ticket · disparaît à 500/3000 Neutre)"),
            DmfQuestRoute(
                "Kerri_T2",
                "Palier 2 — Pierres lourdes",
                HeavyGrindingStoneItemId,
                "Pierre à aiguiser lourde",
                7,
                "(4 tickets · disparaît à 1100/3000 Neutre)"),
            DmfQuestRoute(
                "Kerri_T3",
                "Palier 3 — Brassards en fer vert",
                GreenIronBracersItemId,
                "Brassards en fer vert",
                3,
                "(8 tickets · disparaît à 1700/3000 Neutre)"),
            DmfQuestRoute(
                "Kerri_T4",
                "Palier 4 — Grande masse noire",
                BigBlackMaceItemId,
                "Grande masse noire",
                1,
                "(12 tickets · disparaît à 2500/3000 Neutre)"),
            DmfQuestRoute(
                "Kerri_T5",
                "Palier 5 — Pierres denses",
                DenseGrindingStoneItemId,
                "Pierre à aiguiser dense",
                8,
                "(20 tickets · répétable)"),

            DmfQuestRoute(
                "Rinling_T1",
                "Palier 1 — Modulateurs de cuivre",
                CopperModulatorItemId,
                "Modulateur de cuivre",
                5,
                "(1 ticket · disparaît à 500/3000 Neutre)"),
            DmfQuestRoute(
                "Rinling_T2",
                "Palier 2 — Bidules en bronze",
                WhirringBronzeGizmoItemId,
                "Bidule bourdonnant en bronze",
                7,
                "(4 tickets · disparaît à 1100/3000 Neutre)"),
            DmfQuestRoute(
                "Rinling_T3",
                "Palier 3 — Feux d'artifice verts",
                GreenFireworkItemId,
                "Feu d'artifice vert",
                36,
                "(8 tickets · disparaît à 1700/3000 Neutre)"),
            DmfQuestRoute(
                "Rinling_T4",
                "Palier 4 — Kits de réparation",
                MechanicalRepairKitItemId,
                "Kit de réparation mécanique",
                6,
                "(12 tickets · disparaît à 2500/3000 Neutre)"),
            DmfQuestRoute(
                "Rinling_T5",
                "Palier 5 — Widgets en thorium",
                ThoriumWidgetItemId,
                "Widget en thorium",
                6,
                "(20 tickets · répétable)"),

            DmfQuestRoute(
                "Chronos_T1",
                "Palier 1 — Bottes estampées",
                EmbossedLeatherBootsItemId,
                "Bottes en cuir estampé",
                3,
                "(1 ticket · disparaît à 500/3000 Neutre)"),
            DmfQuestRoute(
                "Chronos_T2",
                "Palier 2 — Armure renforcée",
                ToughenedLeatherArmorItemId,
                "Armure en cuir renforcé",
                3,
                "(4 tickets · disparaît à 1100/3000 Neutre)"),
            DmfQuestRoute(
                "Chronos_T3",
                "Palier 3 — Harnais barbare",
                BarbaricHarnessItemId,
                "Harnais barbare",
                3,
                "(8 tickets · disparaît à 1700/3000 Neutre)"),
            DmfQuestRoute(
                "Chronos_T4",
                "Palier 4 — Jambières en écailles",
                TurtleScaleLeggingsItemId,
                "Jambières en écailles de tortue",
                1,
                "(12 tickets · disparaît à 2500/3000 Neutre)"),
            DmfQuestRoute(
                "Chronos_T5",
                "Palier 5 — Kits d'armure robustes",
                RuggedArmorKitItemId,
                "Kit d'armure robuste",
                8,
                "(20 tickets · répétable)"),
        ],
    };
}
