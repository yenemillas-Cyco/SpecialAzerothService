namespace WindowsOrganiserApp.Models;

public record MonitorInfo(
    IntPtr Handle,
    string DeviceName,
    WindowRect Bounds,
    WindowRect WorkArea,
    bool IsPrimary)
{
    public int Index { get; init; }

    public string DisplayLabel => IsPrimary ? "Principal" : $"Écran {Index}";

    public override string ToString() => DisplayLabel;
}
