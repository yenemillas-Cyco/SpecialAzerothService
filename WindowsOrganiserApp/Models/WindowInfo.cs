using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WindowsOrganiserApp.Models;

public partial class WindowInfo : ObservableObject
{
    public IntPtr Handle { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public uint ProcessId { get; init; }
    public bool CanResize { get; init; } = true;
    public int MinWidth { get; init; }
    public int MinHeight { get; init; }

    [ObservableProperty]
    private int _launchOrder;

    [ObservableProperty]
    private string _customName = string.Empty;

    private static readonly string[] WowProcesses = ["Wow", "WowClassic", "WowT", "WowB"];

    public string DisplayName => string.IsNullOrWhiteSpace(CustomName)
        ? WowProcesses.Any(p => ProcessName.Equals(p, StringComparison.OrdinalIgnoreCase))
            ? $"WoW {LaunchOrder}"
            : TruncateTitle(Title, 20)
        : CustomName;

    private static string TruncateTitle(string title, int max) =>
        title.Length <= max ? title : title[..(max - 1)] + "…";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isMainWindow;

    [ObservableProperty]
    private MonitorInfo? _assignedMonitor;

    [ObservableProperty]
    private bool _isFullscreen;

    public WindowRect? SavedRect { get; set; }

    partial void OnLaunchOrderChanged(int value) => OnPropertyChanged(nameof(DisplayName));
    partial void OnCustomNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));
}
