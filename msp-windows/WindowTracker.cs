using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

public static class WindowTracker
{
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, out RECT lpRect, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    // Returns the foreground window handle, or IntPtr.Zero when it belongs to this
    // app (overlay/control window) or is not a usable top-level window. This lets the
    // overlay follow whichever external program the user focuses, while ignoring focus
    // changes onto our own windows.
    public static IntPtr GetForegroundAppWindow()
    {
        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd) || IsIconic(hWnd)) {
            return IntPtr.Zero;
        }

        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0 || pid == (uint)Process.GetCurrentProcess().Id) {
            return IntPtr.Zero;
        }

        return hWnd;
    }

    public static Rectangle GetWindowBoundsByHandle(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) {
            return Rectangle.Empty;
        }

        if (GetClientRect(hWnd, out RECT clientRect)) {
            POINT topLeft = new POINT { X = 0, Y = 0 };
            ClientToScreen(hWnd, ref topLeft);
            int width = clientRect.Right - clientRect.Left;
            int height = clientRect.Bottom - clientRect.Top;

            if (width > 0 && height > 0) {
                return new Rectangle(topLeft.X, topLeft.Y, width, height);
            }
        }

        // DWMWA_EXTENDED_FRAME_BOUNDS = 9
        int hr = DwmGetWindowAttribute(hWnd, 9, out RECT rect, Marshal.SizeOf(typeof(RECT)));
        if (hr != 0) {
            if (!GetWindowRect(hWnd, out rect)) {
                return Rectangle.Empty;
            }
        }

        return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public static Rectangle GetGameWindowBounds(string processName)
    {
        foreach (Process process in Process.GetProcessesByName(processName)) {
            if (process.MainWindowHandle == IntPtr.Zero) {
                ErrorLogger.LogError("E101", $"Process '{processName}' has no main window handle.");
                continue;
            }

            RECT clientRect;
            if (GetClientRect(process.MainWindowHandle, out clientRect)) {
                POINT topLeft = new POINT { X = 0, Y = 0 };
                ClientToScreen(process.MainWindowHandle, ref topLeft);
                int width = clientRect.Right - clientRect.Left;
                int height = clientRect.Bottom - clientRect.Top;

                if (width > 0 && height > 0) {
                    return new Rectangle(topLeft.X, topLeft.Y, width, height);
                }
            }

            RECT rect;
            // DWMWA_EXTENDED_FRAME_BOUNDS = 9
            int hr = DwmGetWindowAttribute(process.MainWindowHandle, 9, out rect, Marshal.SizeOf(typeof(RECT)));
            if (hr != 0) {
                if (!GetWindowRect(process.MainWindowHandle, out rect)) {
                    ErrorLogger.LogError("E102", $"Failed to get window rectangle for process '{processName}' using GetWindowRect.");
                    continue;
                }
            }
            return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
        return Rectangle.Empty;
    }
}
