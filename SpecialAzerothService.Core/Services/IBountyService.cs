using SpecialAzerothService.Core.Models.Bounty;

namespace SpecialAzerothService.Core.Services;

public interface IBountyService
{
    BountyData Load();
    void Save(BountyData data);
}
