using System;
using System.Runtime.InteropServices;

public static class WinApiHelper
{
    public const int WS_EX_LAYERED = 0x80000;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WS_POPUP = unchecked((int)0x80000000);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    public static void SetWindowTransparent(IntPtr handle)
    {
        int exStyle = GetWindowLong(handle, -20);
        SetWindowLong(handle, -20, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        SetLayeredWindowAttributes(handle, 0, 255, 1);
    }
}
