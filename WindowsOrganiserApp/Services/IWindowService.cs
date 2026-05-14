using WindowsOrganiserApp.Models;

namespace WindowsOrganiserApp.Services;

public interface IWindowService
{
    IntPtr OwnHandle { get; set; }
    List<WindowInfo> GetOpenWindows(bool wowOnly = true);
    void MoveAndResize(IntPtr handle, WindowRect rect);
    void BringToFront(IntPtr handle);
    WindowRect GetWorkArea();
    WindowRect GetWindowRect(IntPtr handle);
    List<MonitorInfo> GetMonitors();
}
