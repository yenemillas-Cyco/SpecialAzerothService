using SpecialAzerothService.Core.Models.Reputation;

namespace SpecialAzerothService.Core.Services;

public static partial class ReputationTurnInCatalog
{
    public const int SilithidCarapaceFragmentItemId = 20384;
    public const int QirajiLordsInsigniaItemId = 21229;
    public const int AncientQirajiArtifactItemId = 21230;

    private static ReputationTurnInRoute BroodTurnInRoute(
        string routeId,
        string variantLabelFr,
        int itemId,
        string nameFr,
        int quantity,
        int reputation,
        string detailFr) =>
        new()
        {
            RouteId = routeId,
            Method = ReputationTurnInMethod.TurnIn,
            LabelFr = variantLabelFr,
            VariantLabelFr = variantLabelFr,
            DescriptionFr = detailFr,
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

    public static readonly ReputationFarmDefinition BroodOfNozdormu = new()
    {
        Id = "BroodOfNozdormu",
        FactionNameFr = "Progéniture de Nozdormu",
        LocationFr = "Silithus (ruches) / Temple d'Ahn'Qiraj",
        NpcNameFr = "Baristolth, Kandrostrasz, Andorgos",
        NotesFr =
            "Quatre voies de réputation : (1) fragments silithide après la chaîne du Sceptre "
            + "(tête du Seigneur des couvées à UBRS) ; (2) trash AQ40 jusqu'à 2999/3000 Neutre ; "
            + "(3) insignes des boss AQ40 ; (4) artefacts sur le trash AQ40. "
            + "Les insignes rapportent aussi 250 rép. Cercle cénarien.",
        Tiers =
        [
            new ReputationFarmTier
            {
                TierId = "Fragments",
                LabelFr = "Fragments de carapace",
                ReputationNeeded = 36000,
                DescriptionFr =
                    "Baristolth (Fort cénarien, Silithus) — quête « La main des justes ». "
                    + "Prérequis : chaîne du Sceptre des sables changeants (Seigneur des couvées, UBRS). "
                    + "Fragments sur les silithides dans les ruches de Silithus.",
                DefaultVariantRouteId = "Fragments",
                VariantRouteIds = ["Fragments"],
            },
            new ReputationFarmTier
            {
                TierId = "Insignias",
                LabelFr = "Insignes de seigneur qiraji",
                ReputationNeeded = 42000,
                DescriptionFr =
                    "Kandrostrasz (AQ40) — quête « Champions mortels ». "
                    + "Un insigne par joueur sur chaque boss AQ40 (sauf C'Thun).",
                DefaultVariantRouteId = "Insignias",
                VariantRouteIds = ["Insignias"],
            },
            new ReputationFarmTier
            {
                TierId = "Artifacts",
                LabelFr = "Artefacts qiraji antiques",
                ReputationNeeded = 42000,
                DescriptionFr =
                    "Andorgos (AQ40) — quête « Secrets des Qiraji ». "
                    + "Butin rare sur tout le trash AQ40.",
                DefaultVariantRouteId = "Artifacts",
                VariantRouteIds = ["Artifacts"],
            },
        ],
        Routes =
        [
            BroodTurnInRoute(
                "Fragments",
                "Fragments silithide",
                SilithidCarapaceFragmentItemId,
                "Fragment de carapace silithide",
                200,
                500,
                "200 fragments = 500 rép. · Répétable jusqu'à Neutre."),
            BroodTurnInRoute(
                "Insignias",
                "Insigne de seigneur qiraji",
                QirajiLordsInsigniaItemId,
                "Insigne de seigneur qiraji",
                1,
                500,
                "1 insigne = 500 rép. (+ 250 Cercle cénarien) · Répétable."),
            BroodTurnInRoute(
                "Artifacts",
                "Artefact qiraji antique",
                AncientQirajiArtifactItemId,
                "Artefact qiraji antique",
                1,
                1000,
                "1 artefact = 1000 rép. · Répétable."),
        ],
    };
}
