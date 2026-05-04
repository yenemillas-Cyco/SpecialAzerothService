namespace WindowsOrganiserApp.Models;

public sealed class LayoutPreset
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<AdvancedWindowPosition> Positions { get; set; } = [];
    public override string ToString() => Name;
}

public sealed class AdvancedWindowPosition
{
    public int SlotIndex { get; set; }
    public string MonitorDeviceName { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsMain { get; set; }
}
