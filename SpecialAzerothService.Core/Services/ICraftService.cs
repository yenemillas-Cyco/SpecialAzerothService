using SpecialAzerothService.Core.Models.Craft;

namespace SpecialAzerothService.Core.Services;

public interface ICraftService
{
    CraftDatabase Database { get; }
    IReadOnlyList<CraftProfession> GetProfessions(string? contentTypeFilter = null);
}
