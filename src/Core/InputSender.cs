using System.Runtime.InteropServices;

namespace ObbyistMacro.Core;

/// <summary>
/// Sends synthetic input to the system (mouse movement, key taps).
/// Uses keybd_event / mouse_event (the same APIs SendKeys and AutoHotkey use):
/// SendInput is rejected with ERROR_INVALID_PARAMETER in some elevated
/// environments, so the classic APIs are more reliable for game automation.
/// </summary>
public static class InputSender
{
    // Scan codes used by the FPS navigation sequence
    public const byte SC_ESC = 0x01;
    public const byte SC_TAB = 0x0F;
    public const byte SC_ENTER = 0x1C;
    public const byte SC_UP = 0x48;
    public const byte SC_DOWN = 0x50;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint MOUSEEVENTF_MOVE = 0x0001;

    public static void MoveMouse(int dx, int dy)
        => mouse_event(MOUSEEVENTF_MOVE, dx, dy, 0, UIntPtr.Zero);

    public static void KeyDown(int vk)
        => keybd_event((byte)vk, 0, 0, UIntPtr.Zero);

    public static void KeyUp(int vk)
        => keybd_event((byte)vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

    public static void TapKey(int vk, int holdMs = 15)
    {
        KeyDown(vk);
        Thread.Sleep(holdMs);
        KeyUp(vk);
    }

    /// <summary>
    /// Sends a key tap by scan code. Arrow keys (Up/Down) are extended keys:
    /// without the extended flag the system interprets them as numpad 8/2.
    /// </summary>
    public static void SendScancode(byte scancode, bool up = false, bool extended = false)
    {
        uint flags = KEYEVENTF_SCANCODE
                   | (extended ? KEYEVENTF_EXTENDEDKEY : 0)
                   | (up ? KEYEVENTF_KEYUP : 0);
        keybd_event(0, scancode, flags, UIntPtr.Zero);
    }

    public static void TapScancode(byte scancode, int holdMs = 15, bool extended = false)
    {
        SendScancode(scancode, extended: extended);
        Thread.Sleep(holdMs);
        SendScancode(scancode, up: true, extended: extended);
    }

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}