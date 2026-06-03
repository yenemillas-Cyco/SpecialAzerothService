using System.IO;
using System.Text.Json;
using Serilog;
using SpecialAzerothService.Core.Models;

namespace SpecialAzerothService.Core.Services;

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
            {
                var fresh = new AppSettings();
                Save(fresh);
                _logger.Information("Created new settings");
                return fresh;
            }

            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();

            _logger.Information("Settings loaded from {Path}, schema={Schema}",
                _filePath, settings.DataSchemaVersion);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load settings, using defaults");
            var fallback = new AppSettings();
            Save(fallback);
            return fallback;
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
