using System.IO;
using System.Text.Json;
using WindowsOrganiserApp.Models;

namespace WindowsOrganiserApp.Services;

public interface IPresetService
{
    List<LayoutPreset> LoadAll();
    void Save(LayoutPreset preset);
    void Delete(string name);
}

public class PresetService : IPresetService
{
    private static readonly string PresetsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpecialAzerothService", "presets");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public List<LayoutPreset> LoadAll()
    {
        if (!Directory.Exists(PresetsDir))
            return [];

        var presets = new List<LayoutPreset>();
        foreach (var file in Directory.GetFiles(PresetsDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var preset = JsonSerializer.Deserialize<LayoutPreset>(json, JsonOpts);
                if (preset is not null)
                    presets.Add(preset);
            }
            catch { /* skip corrupted files */ }
        }

        return presets.OrderBy(p => p.Name).ToList();
    }

    public void Save(LayoutPreset preset)
    {
        Directory.CreateDirectory(PresetsDir);
        var safeName = string.Join("_", preset.Name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(PresetsDir, $"{safeName}.json");
        var json = JsonSerializer.Serialize(preset, JsonOpts);
        File.WriteAllText(path, json);
    }

    public void Delete(string name)
    {
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(PresetsDir, $"{safeName}.json");
        if (File.Exists(path))
            File.Delete(path);
    }
}
