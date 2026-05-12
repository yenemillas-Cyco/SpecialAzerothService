namespace WindowsOrganiserApp.Models;

public sealed class AppSettings
{
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 700;
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public string Theme { get; set; } = "Classic";
    public string Language { get; set; } = "Français";

    public string UserGuid { get; set; } = Guid.NewGuid().ToString();
    public string UserDisplayName { get; set; } = Environment.UserName;
    public string SyncServerUrl { get; set; } = "https://carto-sync-server.fly.dev/carto";
    public List<string> FriendGuids { get; set; } = [];

    public List<MonitorConfigSettings> MonitorConfigs { get; set; } = [];
    public List<WindowSettings> Windows { get; set; } = [];
}

public sealed class MonitorConfigSettings
{
    public string DeviceName { get; set; } = string.Empty;
    public string Mode { get; set; } = nameof(LayoutMode.Main);
    public string Size { get; set; } = nameof(MainSize.Moyen);
    public string Position { get; set; } = nameof(MainPosition.TopRight);
    public bool HasLateral { get; set; } = true;
    public bool HasBandeau { get; set; }
    public string SplitOrientation { get; set; } = nameof(Models.SplitOrientation.Horizontal);
}

public sealed class WindowSettings
{
    public int LaunchOrder { get; set; }
    public string CustomName { get; set; } = string.Empty;
    public string? AssignedMonitorDeviceName { get; set; }
    public bool IsMainWindow { get; set; }
    public bool IsSelected { get; set; } = true;
}
