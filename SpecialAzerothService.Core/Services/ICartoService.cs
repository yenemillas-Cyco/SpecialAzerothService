using SpecialAzerothService.Core.Models.Carto;

namespace SpecialAzerothService.Core.Services;

public interface ICartoService
{
    CartoData Load();
    void Save(CartoData data);
}
