using WindowsOrganiserApp.Models.Craft;

namespace WindowsOrganiserApp.Services;

public interface ICraftService
{
    CraftDatabase Database { get; }
    IReadOnlyList<CraftProfession> GetProfessions(string? contentTypeFilter = null);
}
