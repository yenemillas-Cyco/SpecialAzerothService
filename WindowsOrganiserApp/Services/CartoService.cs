using System.IO;
using System.Text.Json;
using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Services;

public sealed class CartoService : ICartoService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public CartoService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpecialAzerothService");
        Directory.CreateDirectory(appData);
        _filePath = Path.Combine(appData, "carto.json");
    }

    public CartoData Load()
    {
        if (!File.Exists(_filePath))
            return new CartoData();

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<CartoData>(json, JsonOptions) ?? new CartoData();
    }

    public void Save(CartoData data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
