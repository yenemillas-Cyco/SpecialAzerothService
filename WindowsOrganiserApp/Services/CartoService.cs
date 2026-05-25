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
#if DEBUG
    private readonly string? _debugWorkspacePath;
#endif

    public CartoService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpecialAzerothService");
        Directory.CreateDirectory(appData);
        _filePath = Path.Combine(appData, "carto.json");
#if DEBUG
        _debugWorkspacePath = ResolveDebugWorkspacePath();
        if (_debugWorkspacePath != null)
            Directory.CreateDirectory(Path.GetDirectoryName(_debugWorkspacePath)!);
#endif
    }

#if DEBUG
    private static string? ResolveDebugWorkspacePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            if (dir.GetFiles("*.csproj").Any(f =>
                    f.Name.Contains("WindowsOrganiserApp", StringComparison.OrdinalIgnoreCase)
                    || f.Name.Contains("SpecialAzeroth", StringComparison.OrdinalIgnoreCase)))
                return Path.Combine(dir.FullName, "carto.user.json");
        }

        return null;
    }
#endif

    public CartoData Load()
    {
#if DEBUG
        if (_debugWorkspacePath != null && File.Exists(_debugWorkspacePath))
        {
            var devJson = File.ReadAllText(_debugWorkspacePath);
            var devData = JsonSerializer.Deserialize<CartoData>(devJson, JsonOptions) ?? new CartoData();
            NormalizeDictionaries(devData);
            return devData;
        }
#endif

        if (!File.Exists(_filePath))
            return new CartoData();

        var json = File.ReadAllText(_filePath);
        var data = JsonSerializer.Deserialize<CartoData>(json, JsonOptions) ?? new CartoData();
        NormalizeDictionaries(data);
        return data;
    }

    private static void NormalizeDictionaries(CartoData data)
    {
        data.AccountSettings = new Dictionary<string, CartoAccountConfig>(
            data.AccountSettings ?? new Dictionary<string, CartoAccountConfig>(),
            StringComparer.OrdinalIgnoreCase);

        data.AccountDisplayNames = new Dictionary<string, string>(
            data.AccountDisplayNames ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
    }


    public void Save(CartoData data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(_filePath, json);
#if DEBUG
        if (_debugWorkspacePath != null)
            File.WriteAllText(_debugWorkspacePath, json);
#endif
    }
}
