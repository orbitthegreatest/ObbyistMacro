using System.Diagnostics;
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
    public const byte SC_F11 = 0x57;

    // Scan code for Space, used by the wallhop macro's jump (like Spencer Macro Utilities)
    public const byte SC_SPACE = 0x39;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

    public static void MoveMouse(int dx, int dy)
        => mouse_event(MOUSEEVENTF_MOVE, dx, dy, 0, UIntPtr.Zero);

    /// <summary>
    /// Moves the cursor to absolute screen coordinates via mouse_event.
    /// Unlike SetCursorPos, mouse_event also generates raw input (WM_INPUT),
    /// which games like Roblox actually process — so this updates the game's
    /// internal cursor position, while SetCursorPos only moves the OS cursor.
    ///
    /// Normalizes against the full virtual desktop (SM_XVIRTUALSCREEN etc.),
    /// not just the primary monitor — GetSystemMetrics(SM_CXSCREEN) only covers
    /// the primary display, so on any multi-monitor setup, or when Roblox is on
    /// a non-primary or differently-sized monitor, coordinates from
    /// WindowClientCenter (which can be negative or exceed the primary
    /// display's bounds) would silently normalize to the wrong point. The
    /// MOUSEEVENTF_VIRTUALDESK flag tells mouse_event to map the 0..65535
    /// range across that same virtual desktop instead of the primary monitor.
    /// </summary>
    public static void MoveMouseAbsolute(int x, int y)
    {
        int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        if (vw <= 0) vw = GetSystemMetrics(SM_CXSCREEN);
        if (vh <= 0) vh = GetSystemMetrics(SM_CYSCREEN);
        uint nx = (uint)((long)(x - vx) * 65535 / Math.Max(1, vw - 1));
        uint ny = (uint)((long)(y - vy) * 65535 / Math.Max(1, vh - 1));
        mouse_event(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, (int)nx, (int)ny, 0, UIntPtr.Zero);
    }

    /// <summary>Moves the cursor to absolute screen coordinates.</summary>
    public static void SetCursor(int x, int y)
        => SetCursorPos(x, y);

    /// <summary>Reads the current cursor position.</summary>
    public static bool GetCursorPosition(out int x, out int y)
    {
        bool ok = GetCursorPos(out POINT p);
        x = p.X;
        y = p.Y;
        return ok;
    }

    /// <summary>Re-applies the 1x1 cursor clip. The game may override the clip, so call this repeatedly.</summary>
    public static void ReassertClip(int x, int y)
    {
        RECT r = new() { Left = x, Top = y, Right = x + 1, Bottom = y + 1 };
        ClipCursor(ref r);
    }

    /// <summary>Releases the cursor clip.</summary>
    public static void UnlockCursor()
    {
        RECT empty = new();
        ClipCursor(ref empty);
    }

    /// <summary>
    /// Clears any active cursor clip and moves the cursor to a point, re-clearing
    /// the clip until the position sticks. Games apply their own 1x1 "pointer
    /// lock" clips (e.g. Roblox windowed mode parks the cursor at the window's
    /// top-left), and those clips clamp SetCursorPos to the clip rect. Clearing
    /// the clip repeatedly for a short window beats them, since the game only
    /// re-asserts its lock for a moment after the menu closes.
    /// </summary>
    public static void ForceCursor(int x, int y, int maxMs = 4000)
    {
        Stopwatch sw = Stopwatch.StartNew();
        int stable = 0;
        while (sw.ElapsedMilliseconds < maxMs && stable < 8)
        {
            UnlockCursor();
            SetCursorPos(x, y);
            Thread.Sleep(40);
            if (GetCursorPos(out POINT p) && Math.Abs(p.X - x) <= 4 && Math.Abs(p.Y - y) <= 4) stable++;
            else stable = 0;
        }
    }

    /// <summary>
    /// Fights the game for the final cursor position after a menu closes. Roblox
    /// (windowed/borderless mode) re-applies its own 1x1 pointer-lock clip right
    /// as the menu closes, parking the cursor at the window's top-left corner —
    /// after the clip is asserted, a single SetCursorPos/mouse_event call loses
    /// the race silently. This clears that clip and re-sends both the OS cursor
    /// position (SetCursorPos) and a raw-input move (mouse_event, so the game's
    /// own internal cursor tracking updates too) every tick until the position
    /// sticks for several consecutive reads or maxMs runs out.
    /// </summary>
    public static void ForceCursorRaw(int x, int y, int maxMs = 700)
    {
        Stopwatch sw = Stopwatch.StartNew();
        int stable = 0;
        while (sw.ElapsedMilliseconds < maxMs && stable < 8)
        {
            UnlockCursor();
            SetCursorPos(x, y);
            MoveMouseAbsolute(x, y);
            Thread.Sleep(25);
            if (GetCursorPos(out POINT p) && Math.Abs(p.X - x) <= 4 && Math.Abs(p.Y - y) <= 4) stable++;
            else stable = 0;
        }
    }

    /// <summary>Full window rect (screen coords, includes chrome) — used to detect
    /// whether a display-mode switch (e.g. F11) actually took effect.</summary>
    public static string WindowRectLog(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out RECT r)) return "none";
        return $"({r.Left},{r.Top})-({r.Right},{r.Bottom})";
    }

    /// <summary>
    /// True if the window's rect fills its monitor's bounds (a couple pixels of
    /// slack for borderless-fullscreen edge rounding). The F11 stuck-cursor fix
    /// is only needed in windowed mode — Roblox fullscreen doesn't hit the same
    /// stuck-clip bug — so this lets the macro skip the flicker entirely when
    /// it's not needed.
    /// </summary>
    public static bool IsFullscreen(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out RECT wr)) return false;
        IntPtr mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (mon == IntPtr.Zero) return false;
        MONITORINFO mi = new() { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(mon, ref mi)) return false;
        const int slack = 2;
        return wr.Left <= mi.rcMonitor.Left + slack && wr.Top <= mi.rcMonitor.Top + slack
            && wr.Right >= mi.rcMonitor.Right - slack && wr.Bottom >= mi.rcMonitor.Bottom - slack;
    }

    /// <summary>Center of the primary screen.</summary>
    public static (int X, int Y) ScreenCenter()
        => (GetSystemMetrics(SM_CXSCREEN) / 2, GetSystemMetrics(SM_CYSCREEN) / 2);

    /// <summary>Center of a window's client area (screen coordinates).</summary>
    public static (int X, int Y) WindowClientCenter(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetClientRect(hwnd, out RECT r) || r.Right - r.Left <= 0)
            return ScreenCenter();
        ClientToScreen(hwnd, out POINT origin);
        return (origin.X + (r.Right - r.Left) / 2, origin.Y + (r.Bottom - r.Top) / 2);
    }

    /// <summary>
    /// Center-right of a window's client area (screen coordinates): 75% across,
    /// vertically centered. Auto-detects the game's real resolution, so it works
    /// windowed, borderless or fullscreen, on any monitor.
    /// </summary>
    public static (int X, int Y) WindowClientCenterRight(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetClientRect(hwnd, out RECT r) || r.Right - r.Left <= 0)
            return ScreenCenter();
        ClientToScreen(hwnd, out POINT origin);
        return (origin.X + (int)((r.Right - r.Left) * 0.75), origin.Y + (r.Bottom - r.Top) / 2);
    }

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

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ClipCursor(ref RECT lpRect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
}