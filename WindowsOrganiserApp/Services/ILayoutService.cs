using WindowsOrganiserApp.Models;

namespace WindowsOrganiserApp.Services;

public interface ILayoutService
{
    Dictionary<IntPtr, WindowRect> CalculateMainLayout(
        List<WindowInfo> selectedWindows, WindowRect workArea,
        MainSize mainSize, MainPosition mainPosition,
        bool hasLateral, bool hasBandeau);

    Dictionary<IntPtr, WindowRect> CalculateSplitLayout(
        List<WindowInfo> selectedWindows, WindowRect workArea,
        SplitOrientation orientation = SplitOrientation.Horizontal);
}
