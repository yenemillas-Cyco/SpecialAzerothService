using System.IO;
using System.Text.Json;
using SpecialAzerothService.Core.Models.Bounty;

namespace SpecialAzerothService.Core.Services;

public sealed class BountyService : IBountyService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public BountyService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpecialAzerothService");
        Directory.CreateDirectory(appData);
        _filePath = Path.Combine(appData, "bounties.json");
    }

    public BountyData Load()
    {
        if (!File.Exists(_filePath))
            return new BountyData();

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<BountyData>(json, JsonOpts) ?? new BountyData();
    }

    public void Save(BountyData data)
    {
        var json = JsonSerializer.Serialize(data, JsonOpts);
        File.WriteAllText(_filePath, json);
    }
}
