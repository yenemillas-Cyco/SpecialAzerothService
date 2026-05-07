using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;
using WindowsOrganiserApp.Helpers;
using WindowsOrganiserApp.Models;

namespace WindowsOrganiserApp.Services;

public class WindowService : IWindowService
{
    private readonly ILogger _logger;

    public IntPtr OwnHandle { get; set; }

    public WindowService(ILogger logger)
    {
        _logger = logger;
    }

    public List<WindowInfo> GetOpenWindows(bool wowOnly = true)
    {
        var windows = new List<WindowInfo>();
        var shellWindow = NativeMethods.GetShellWindow();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (hWnd == shellWindow) return true;
            if (hWnd == OwnHandle) return true;
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;
            if (NativeMethods.IsWindowCloaked(hWnd)) return true;

            var length = NativeMethods.GetWindowTextLength(hWnd);
            if (length == 0) return true;

            var exStyle = NativeMethods.GetWindowLongA(hWnd, NativeMethods.GWL_EXSTYLE);
            if ((exStyle & (int)NativeMethods.WS_EX_TOOLWINDOW) != 0)
                return true;

            var sb = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString();

            if (string.IsNullOrWhiteSpace(title)) return true;

            var (processName, processId, startTime) = GetProcessInfo(hWnd);

            if (wowOnly)
            {
#if DEBUG
                var allowed = new[] { "Wow", "WowClassic", "WowT", "WowB", "msedge" };
#else
                var allowed = new[] { "Wow", "WowClassic", "WowT", "WowB" };
#endif
                if (!allowed.Any(a => processName.Equals(a, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            else
            {
                // Mode toutes fenêtres : exclure les process système courants
                var excluded = new[] { "explorer", "SearchHost", "TextInputHost", "ShellExperienceHost",
                    "StartMenuExperienceHost", "SystemSettings", "ApplicationFrameHost" };
                if (excluded.Any(e => processName.Equals(e, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            var style = NativeMethods.GetWindowLongA(hWnd, NativeMethods.GWL_STYLE);
            var canResize = (style & (int)NativeMethods.WS_THICKFRAME) != 0;
            var (minW, minH) = canResize ? NativeMethods.GetWindowMinSize(hWnd) : (0, 0);

            windows.Add(new WindowInfo
            {
                Handle = hWnd,
                Title = title,
                ProcessName = processName,
                ProcessId = processId,
                StartTime = startTime,
                CanResize = canResize,
                MinWidth = minW,
                MinHeight = minH
            });

            return true;
        }, IntPtr.Zero);

        // Trier par ordre de lancement et assigner le numéro
        windows = windows.OrderBy(w => w.StartTime).ToList();
        for (var i = 0; i < windows.Count; i++)
            windows[i].LaunchOrder = i + 1;

        _logger.Information("Enumerated {Count} windows (wowOnly={WowOnly})", windows.Count, wowOnly);
        return windows;
    }

    public void MoveAndResize(IntPtr handle, WindowRect rect)
    {
        if (!NativeMethods.IsWindow(handle))
        {
            _logger.Warning("Handle {Handle} is no longer valid, skipping", handle);
            return;
        }

        _logger.Information("Moving window {Handle} to ({X},{Y}) size ({W}x{H})",
            handle, rect.X, rect.Y, rect.Width, rect.Height);

        // Retirer le style WS_MAXIMIZE s'il est présent (fenêtre snappée ou maximisée)
        var style = NativeMethods.GetWindowLongA(handle, NativeMethods.GWL_STYLE);
        if ((style & (int)NativeMethods.WS_MAXIMIZE) != 0)
        {
            NativeMethods.SetWindowLongA(handle, NativeMethods.GWL_STYLE,
                style & ~(int)NativeMethods.WS_MAXIMIZE);
            _logger.Information("Removed WS_MAXIMIZE from window {Handle}", handle);
        }

        // Forcer l'état normal (ni minimisé, ni maximisé)
        NativeMethods.ShowWindow(handle, NativeMethods.SW_SHOWNORMAL);
        Thread.Sleep(50);

        // Déplacer et redimensionner
        var ok = NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HWND_TOP,
            rect.X, rect.Y, rect.Width, rect.Height,
            NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW);

        if (!ok)
        {
            var error = Marshal.GetLastWin32Error();
            _logger.Error("SetWindowPos failed for {Handle}, Win32 error={Error}", handle, error);

            // Fallback avec MoveWindow
            _logger.Information("Trying MoveWindow fallback for {Handle}", handle);
            NativeMethods.MoveWindow(handle, rect.X, rect.Y, rect.Width, rect.Height, true);
        }

        NativeMethods.SetForegroundWindow(handle);
    }

    public WindowRect GetWorkArea()
    {
        var rect = new NativeMethods.RECT();
        NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETWORKAREA, 0, ref rect, 0);

        return new WindowRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public WindowRect GetWindowRect(IntPtr handle)
    {
        NativeMethods.GetWindowRect(handle, out var rect);
        return new WindowRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref NativeMethods.RECT _, IntPtr _) =>
        {
            var info = new NativeMethods.MONITORINFOEX();
            info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(info);

            if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                var bounds = new WindowRect(
                    info.rcMonitor.Left, info.rcMonitor.Top,
                    info.rcMonitor.Right - info.rcMonitor.Left,
                    info.rcMonitor.Bottom - info.rcMonitor.Top);

                var workArea = new WindowRect(
                    info.rcWork.Left, info.rcWork.Top,
                    info.rcWork.Right - info.rcWork.Left,
                    info.rcWork.Bottom - info.rcWork.Top);

                var isPrimary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                var deviceName = info.szDevice.TrimEnd('\0');

                monitors.Add(new MonitorInfo(hMonitor, deviceName, bounds, workArea, isPrimary));
            }

            return true;
        }, IntPtr.Zero);

        _logger.Information("Enumerated {Count} monitors", monitors.Count);
        return monitors;
    }

    private static (string Name, uint Pid, DateTime StartTime) GetProcessInfo(IntPtr hWnd)
    {
        try
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
            var process = Process.GetProcessById((int)processId);
            return (process.ProcessName, processId, process.StartTime);
        }
        catch
        {
            return ("Unknown", 0, DateTime.MaxValue);
        }
    }
}
