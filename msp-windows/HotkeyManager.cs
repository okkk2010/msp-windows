using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class HotkeyManager : IDisposable
{
    private const int HOTKEY_ID = 1;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_SHIFT = 0x0004;
    private const int VK_S = (int)Keys.S;
    private IntPtr _handle;
    private Action _onHotkeyPressed;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public HotkeyManager(IntPtr handle, Action onHotkeyPressed)
    {
        _handle = handle;
        _onHotkeyPressed = onHotkeyPressed;
        if (!RegisterHotKey(_handle, HOTKEY_ID, MOD_ALT | MOD_SHIFT, VK_S) ) {
            ErrorLogger.LogError("E103", $"Failed to register hotkey (ALT+SHIFT+S) for handle {_handle}");
        }
    }

    public bool HandleHotkeyMessage(Message m)
    {
        if (m.Msg == 0x0312 && m.WParam.ToInt32() == HOTKEY_ID) {
            _onHotkeyPressed?.Invoke();
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        UnregisterHotKey(_handle, HOTKEY_ID);
    }
}
