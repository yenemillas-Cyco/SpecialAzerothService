using SpecialAzerothService.Core.Models.Craft;

namespace SpecialAzerothService.Core.Services;

internal static class CraftDecompositionHelper
{
    /// <summary>
    /// Pas de chaîne via transmutation : Feu élémentaire, Arcanite, etc. restent des besoins directs.
    /// </summary>
    public static bool ShouldExpandIntoReagents(CraftLookupResult lookup) =>
        !lookup.IsTransmute
        && !lookup.Entry.IsItemEntry
        && lookup.Entry.Reagents.Count > 0;
}
