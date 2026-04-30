using WindowsOrganiserApp.Models;

namespace WindowsOrganiserApp.ViewModels;

public static class LayoutModeHelper
{
    public static LayoutMode[] Values { get; } = Enum.GetValues<LayoutMode>();
}

public static class MainSizeHelper
{
    public static MainSize[] Values { get; } = Enum.GetValues<MainSize>();
}

public static class MainPositionHelper
{
    public static MainPosition[] Values { get; } = Enum.GetValues<MainPosition>();
}

public static class SplitOrientationHelper
{
    public static SplitOrientation[] Values { get; } = Enum.GetValues<SplitOrientation>();
}
