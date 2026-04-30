using System.IO;
using System.Text.Json;
using Serilog;
using WindowsOrganiserApp.Models;

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
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);
            _logger.Information("Settings loaded from {Path}", _filePath);
            return settings ?? new AppSettings();
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
