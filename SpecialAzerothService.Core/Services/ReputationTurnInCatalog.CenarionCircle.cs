using SpecialAzerothService.Core.Models.Reputation;

namespace SpecialAzerothService.Core.Services;

public static partial class ReputationTurnInCatalog
{
    public const int EncryptedTwilightTextItemId = 20404;

    public static readonly ReputationFarmDefinition CenarionCircle = new()
    {
        Id = "CenarionCircle",
        FactionNameFr = "Cercle cénarien",
        LocationFr = "Fort cénarien (Silithus)",
        NpcNameFr = "Bor Wildmane",
        NotesFr = "10 textes du crépuscule cryptés remis = 100 réputation.",
        Routes =
        [
            new ReputationTurnInRoute
            {
                RouteId = "Texts",
                Method = ReputationTurnInMethod.TurnIn,
                LabelFr = "Textes du crépuscule cryptés",
                DescriptionFr =
                    "Remettre 10 textes du crépuscule cryptés à Bor Wildmane "
                    + "(quête répétable « Les textes du crépuscule cryptés »).",
                BaseReputation = 100,
                ItemsPerTurnIn = 10,
                ItemUnitLabelFr = "textes",
                AcceptedItems =
                [
                    new() { ItemId = EncryptedTwilightTextItemId, NameFr = "Texte du crépuscule crypté" },
                ],
            },
        ],
    };
}
