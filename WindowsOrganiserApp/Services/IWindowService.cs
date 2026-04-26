using WindowsOrganiserApp.Models;

namespace WindowsOrganiserApp.Services;

public interface IWindowService
{
    IntPtr OwnHandle { get; set; }
    List<WindowInfo> GetOpenWindows();
    void MoveAndResize(IntPtr handle, WindowRect rect);
    WindowRect GetWorkArea();
}
