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
                if (!WowWindowRules.IsWowGameProcess(processName, processId))
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

        // Deduplicate by handle then sort by launch order
        windows = windows
            .GroupBy(w => w.Handle).Select(g => g.First())
            .OrderBy(w => w.StartTime).ToList();
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

        var style = NativeMethods.GetWindowLongA(handle, NativeMethods.GWL_STYLE);
        if ((style & (int)NativeMethods.WS_MAXIMIZE) != 0)
        {
            NativeMethods.SetWindowLongA(handle, NativeMethods.GWL_STYLE,
                style & ~(int)NativeMethods.WS_MAXIMIZE);
        }

        NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
        Thread.Sleep(100);

        // NOACTIVATE + NOZORDER: avoid Z-order race when moving multiple windows
        const uint flags = NativeMethods.SWP_FRAMECHANGED
                         | NativeMethods.SWP_NOACTIVATE
                         | NativeMethods.SWP_NOZORDER
                         | NativeMethods.SWP_ASYNCWINDOWPOS;

        var ok = NativeMethods.SetWindowPos(handle, IntPtr.Zero,
            rect.X, rect.Y, rect.Width, rect.Height, flags);

        if (!ok)
        {
            var error = Marshal.GetLastWin32Error();
            _logger.Warning("SetWindowPos failed for {Handle}, error={Error} — retrying with MoveWindow", handle, error);
            NativeMethods.MoveWindow(handle, rect.X, rect.Y, rect.Width, rect.Height, true);
        }

        // Verify position was actually applied
        Thread.Sleep(30);
        NativeMethods.GetWindowRect(handle, out var actual);
        var actualX = actual.Left;
        var actualY = actual.Top;
        var actualW = actual.Right - actual.Left;
        var actualH = actual.Bottom - actual.Top;
        if (Math.Abs(actualX - rect.X) > 8 || Math.Abs(actualY - rect.Y) > 8 ||
            Math.Abs(actualW - rect.Width) > 16 || Math.Abs(actualH - rect.Height) > 16)
        {
            _logger.Warning("Position mismatch for {Handle}: expected ({X},{Y},{W},{H}) got ({AX},{AY},{AW},{AH}) — retrying",
                handle, rect.X, rect.Y, rect.Width, rect.Height, actualX, actualY, actualW, actualH);
            Thread.Sleep(100);
            NativeMethods.MoveWindow(handle, rect.X, rect.Y, rect.Width, rect.Height, true);
        }
    }

    public void BringToFront(IntPtr handle)
    {
        if (!NativeMethods.IsWindow(handle)) return;
        if (NativeMethods.IsIconic(handle))
            NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
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
