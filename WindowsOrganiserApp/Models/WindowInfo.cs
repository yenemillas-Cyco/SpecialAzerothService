using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WindowsOrganiserApp.Models;

public partial class WindowInfo : ObservableObject
{
    public IntPtr Handle { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public uint ProcessId { get; init; }

    [ObservableProperty]
    private int _launchOrder;

    [ObservableProperty]
    private string _customName = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(CustomName)
        ? $"WoW {LaunchOrder}"
        : CustomName;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isMainWindow;

    [ObservableProperty]
    private int _order;

    [ObservableProperty]
    private MonitorInfo? _assignedMonitor;

    [ObservableProperty]
    private bool _isFullscreen;

    public WindowRect? SavedRect { get; set; }

    partial void OnLaunchOrderChanged(int value) => OnPropertyChanged(nameof(DisplayName));
    partial void OnCustomNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));
}
