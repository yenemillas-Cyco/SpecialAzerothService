using System.IO;
using System.Text.Json;
using Serilog;
using WindowsOrganiserApp.Models;
using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private readonly ILogger _logger;

    public SettingsService(ILogger logger)
    {
        _logger = logger;
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpecialAzerothService");
        Directory.CreateDirectory(appData);
        _filePath = Path.Combine(appData, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSettings();

            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();

            if (settings.FriendGuids.Count > 0 && settings.Friends.Count == 0)
            {
                foreach (var guid in settings.FriendGuids)
                    settings.Friends.Add(new FriendEntry { Guid = guid, Name = guid[..Math.Min(8, guid.Length)] });
                settings.FriendGuids.Clear();
                _logger.Information("Migrated {Count} friendGuids to Friends", settings.Friends.Count);
                Save(settings);
            }

            _logger.Information("Settings loaded from {Path}", _filePath);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load settings, using defaults");
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOpts);
            File.WriteAllText(_filePath, json);
            _logger.Information("Settings saved to {Path}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to save settings");
        }
    }
}
