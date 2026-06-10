using SpecialAzerothService.Core.Models.Reputation;

namespace SpecialAzerothService.Core.Services;

public static partial class ReputationTurnInCatalog
{
    private static ReputationFarmDefinition Placeholder(
        string id,
        string name,
        string location,
        string npc,
        string notes) =>
        new()
        {
            Id = id,
            FactionNameFr = name,
            LocationFr = location,
            NpcNameFr = npc,
            NotesFr = notes,
            Routes = [],
        };
}
