using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Services;

public interface ICartoService
{
    CartoData Load();
    void Save(CartoData data);
}
