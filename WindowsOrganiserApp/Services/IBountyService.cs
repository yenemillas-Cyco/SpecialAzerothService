using WindowsOrganiserApp.Models.Bounty;

namespace WindowsOrganiserApp.Services;

public interface IBountyService
{
    BountyData Load();
    void Save(BountyData data);
}
