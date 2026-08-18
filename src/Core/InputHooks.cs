using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ObbyistMacro.Core;

/// <summary>Global low-level keyboard and mouse hooks (WH_KEYBOARD_LL / WH_MOUSE_LL).</summary>
public static class InputHooks
{
    public enum MouseButton { Left, Right, Middle, X1, X2 }

    public static event Action<int> KeyDown;
    public static event Action<int> KeyUp;
    public static event Action<MouseButton> MouseDown;
    public static event Action<MouseButton> MouseUp;

    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;

    private const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208;
    private const int WM_XBUTTONDOWN = 0x020B, WM_XBUTTONUP = 0x020C;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    private static HookProc _kbdProc;
    private static HookProc _mouseProc;
    private static IntPtr _kbdHook;
    private static IntPtr _mouseHook;

    public static void Start()
    {
        if (_kbdHook != IntPtr.Zero) return;
        using Process curProcess = Process.GetCurrentProcess();
        using ProcessModule curModule = curProcess.MainModule;
        IntPtr module = GetModuleHandle(curModule.ModuleName);
        _kbdProc = KeyboardCallback;
        _mouseProc = MouseCallback;
        _kbdHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbdProc, module, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
    }

    public static void Stop()
    {
        if (_kbdHook != IntPtr.Zero) { UnhookWindowsHookEx(_kbdHook); _kbdHook = IntPtr.Zero; }
        if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
    }

    private static IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            KBDLLHOOKSTRUCT info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vk = (int)info.vkCode;
            if (vk == 0) return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            long msg = wParam.ToInt64();
            if (msg == 0x0100 || msg == 0x0104) KeyDown?.Invoke(vk);
            else if (msg == 0x0101 || msg == 0x0105) KeyUp?.Invoke(vk);
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            MSLLHOOKSTRUCT info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            long msg = wParam.ToInt64();
            switch (msg)
            {
                case WM_LBUTTONDOWN: MouseDown?.Invoke(MouseButton.Left); break;
                case WM_LBUTTONUP: MouseUp?.Invoke(MouseButton.Left); break;
                case WM_RBUTTONDOWN: MouseDown?.Invoke(MouseButton.Right); break;
                case WM_RBUTTONUP: MouseUp?.Invoke(MouseButton.Right); break;
                case WM_MBUTTONDOWN: MouseDown?.Invoke(MouseButton.Middle); break;
                case WM_MBUTTONUP: MouseUp?.Invoke(MouseButton.Middle); break;
                case WM_XBUTTONDOWN:
                case WM_XBUTTONUP:
                    int which = (int)((info.mouseData >> 16) & 0xFFFF);
                    MouseButton btn = which == 1 ? MouseButton.X1 : MouseButton.X2;
                    if (msg == WM_XBUTTONDOWN) MouseDown?.Invoke(btn); else MouseUp?.Invoke(btn);
                    break;
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    public static int MouseButtonToVk(MouseButton b) => b switch
    {
        MouseButton.Left => KeyCodes.VK_LBUTTON,
        MouseButton.Right => KeyCodes.VK_RBUTTON,
        MouseButton.Middle => KeyCodes.VK_MBUTTON,
        MouseButton.X1 => KeyCodes.VK_XBUTTON1,
        _ => KeyCodes.VK_XBUTTON2,
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}