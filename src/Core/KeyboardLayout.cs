using System.Runtime.InteropServices;
using System.Text;

namespace ObbyistMacro.Core;

/// <summary>
/// Detects the active keyboard layout and resolves the physical keys that type
/// given characters on it. Roblox's camera-alignment keys (comma = rotate the
/// camera 45° one way, period = the other) are matched by character, so on
/// non-US layouts (French AZERTY, German QWERTZ, Russian, Dvorak, English
/// Canada, ...) the correct physical keys differ. VkKeyScanExW against the
/// active layout resolves them exactly, for any layout, with no static table.
/// </summary>
public static class KeyboardLayout
{
    private const uint MAPVK_VK_TO_VSC = 0;
    private const uint LOCALE_SLANGUAGE = 0x2;

    /// <summary>
    /// Layout of the foreground window's thread (Roblox while the macros are
    /// firing). Falls back to this process's layout.
    /// </summary>
    public static IntPtr ForegroundLayout()
    {
        IntPtr hwnd = GetForegroundWindow();
        uint tid = hwnd == IntPtr.Zero ? 0 : GetWindowThreadProcessId(hwnd, out _);
        IntPtr hkl = GetKeyboardLayout(tid);
        return hkl == IntPtr.Zero ? GetKeyboardLayout(0) : hkl;
    }

    /// <summary>Friendly name of the active layout, e.g. "English (Canada)".</summary>
    public static string DisplayName()
    {
        try
        {
            uint langId = (uint)(ForegroundLayout().ToInt64() & 0xFFFF);
            var locale = new StringBuilder(85);
            if (LCIDToLocaleName(langId, locale, locale.Capacity, 0) > 0)
            {
                var name = new StringBuilder(256);
                if (GetLocaleInfoEx(locale.ToString(), LOCALE_SLANGUAGE, name, name.Capacity) > 0)
                    return name.ToString();
            }
        }
        catch { }
        return "Unknown layout";
    }

    /// <summary>
    /// Resolves the key that types <paramref name="c"/> on the active layout.
    /// <paramref name="modifiers"/> carries the shift state VkKeyScanExW reports:
    /// bit 0 = Shift, bit 1 = Ctrl, bit 2 = Alt (Ctrl+Alt together = AltGr).
    /// </summary>
    public static bool Resolve(char c, out int vk, out int scan, out bool extended, out byte modifiers)
    {
        vk = 0;
        scan = 0;
        extended = false;
        modifiers = 0;
        IntPtr hkl = ForegroundLayout();
        short encoded = VkKeyScanExW(c, hkl);
        if (encoded == -1) return false;
        vk = encoded & 0xFF;
        modifiers = (byte)((encoded >> 8) & 0xFF);
        scan = ScanCodeFor(vk, hkl);
        extended = IsExtendedVk(vk);
        return scan != 0;
    }

    /// <summary>Scan code for a virtual key on the active layout.</summary>
    public static int ScanCodeFor(int vk) => ScanCodeFor(vk, ForegroundLayout());

    private static int ScanCodeFor(int vk, IntPtr hkl)
        => (int)MapVirtualKeyExW((uint)vk, MAPVK_VK_TO_VSC, hkl);

    /// <summary>
    /// Virtual keys whose scan codes live on the E0xx extended prefix. Without
    /// the extended flag the system interprets them as their non-extended twin
    /// (e.g. Right arrow becomes numpad 8).
    /// </summary>
    public static bool IsExtendedVk(int vk) => vk switch
    {
        0xA3 or 0xA5 or 0x2D or 0x2E or 0x24 or 0x23 or 0x21 or 0x22
            or 0x25 or 0x26 or 0x27 or 0x28 or 0x90 => true,
        _ => false,
    };

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int LCIDToLocaleName(uint Locale, StringBuilder lpName, int cchName, uint dwFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetLocaleInfoEx(string lpLocaleName, uint LCType, StringBuilder lpLCData, int cchData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScanExW(char ch, IntPtr dwhkl);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyExW(uint uCode, uint uMapType, IntPtr dwhkl);
}