using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ObbyistMacro.Core;

/// <summary>Roblox process helpers: detection, foreground check, suspend/resume (NtSuspendProcess).</summary>
public static class Roblox
{
    public const string ProcessName = "RobloxPlayerBeta";

    public static bool IsRunning()
    {
        try { return Process.GetProcessesByName(ProcessName).Length > 0; }
        catch { return false; }
    }

    public static bool IsForeground()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return false;
            using Process p = Process.GetProcessById((int)pid);
            return string.Equals(p.ProcessName, ProcessName, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>Returns the handle of the current foreground window.</summary>
    public static IntPtr GetForegroundWindowHandle()
    {
        try { return GetForegroundWindow(); }
        catch { return IntPtr.Zero; }
    }

    /// <summary>Brings a Roblox window to the foreground (restores it if minimized).</summary>
    public static bool Focus()
    {
        try
        {
            foreach (Process p in Process.GetProcessesByName(ProcessName))
            {
                IntPtr h = p.MainWindowHandle;
                if (h == IntPtr.Zero) continue;
                if (IsIconic(h)) ShowWindow(h, SW_RESTORE);
                if (!SetForegroundWindow(h))
                {
                    // Alt key press bypasses the foreground lock
                    keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    SetForegroundWindow(h);
                }
                return true;
            }
        }
        catch { }
        return false;
    }

    public static bool Suspend()
    {
        bool any = false;
        foreach (Process p in Process.GetProcessesByName(ProcessName))
        {
            IntPtr h = OpenProcess(0x1F0FFF, false, (uint)p.Id);
            if (h == IntPtr.Zero) continue;
            NtSuspendProcess(h);
            CloseHandle(h);
            any = true;
        }
        return any;
    }

    public static bool Resume()
    {
        bool any = false;
        foreach (Process p in Process.GetProcessesByName(ProcessName))
        {
            IntPtr h = OpenProcess(0x1F0FFF, false, (uint)p.Id);
            if (h == IntPtr.Zero) continue;
            NtResumeProcess(h);
            CloseHandle(h);
            any = true;
        }
        return any;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const int SW_RESTORE = 9;
    private const byte VK_MENU = 0x12;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(IntPtr processHandle);
}