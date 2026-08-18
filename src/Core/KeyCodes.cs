using System.Runtime.InteropServices;

namespace ObbyistMacro.Core;

/// <summary>Friendly display names and reverse lookup for virtual-key codes and mouse buttons.</summary>
public static class KeyCodes
{
    public const int VK_LBUTTON = 0x01;
    public const int VK_RBUTTON = 0x02;
    public const int VK_MBUTTON = 0x04;
    public const int VK_XBUTTON1 = 0x05;
    public const int VK_XBUTTON2 = 0x06;
    public const int VK_BACK = 0x08;
    public const int VK_TAB = 0x09;
    public const int VK_RETURN = 0x0D;
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;
    public const int VK_PAUSE = 0x13;
    public const int VK_CAPITAL = 0x14;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_SPACE = 0x20;
    public const int VK_PRIOR = 0x21;
    public const int VK_NEXT = 0x22;
    public const int VK_END = 0x23;
    public const int VK_HOME = 0x24;
    public const int VK_LEFT = 0x25;
    public const int VK_UP = 0x26;
    public const int VK_RIGHT = 0x27;
    public const int VK_DOWN = 0x28;
    public const int VK_INSERT = 0x2D;
    public const int VK_DELETE = 0x2E;
    public const int VK_F11 = 0x7A;

    public static bool IsMouseButton(int vk) => vk >= VK_LBUTTON && vk <= VK_XBUTTON2;

    public static string Name(int vk)
    {
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
        if (vk >= 0x60 && vk <= 0x69) return "Numpad" + (vk - 0x60);
        if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x70 + 1);
        return vk switch
        {
            VK_LBUTTON => "LButton",
            VK_RBUTTON => "RButton",
            VK_MBUTTON => "MButton",
            VK_XBUTTON1 => "XButton1",
            VK_XBUTTON2 => "XButton2",
            VK_BACK => "Backspace",
            VK_TAB => "Tab",
            VK_RETURN => "Enter",
            VK_SHIFT => "Shift",
            VK_CONTROL => "Ctrl",
            VK_MENU => "Alt",
            VK_PAUSE => "Pause",
            VK_CAPITAL => "CapsLock",
            VK_ESCAPE => "Esc",
            VK_SPACE => "Space",
            VK_PRIOR => "PgUp",
            VK_NEXT => "PgDn",
            VK_END => "End",
            VK_HOME => "Home",
            VK_LEFT => "Left",
            VK_UP => "Up",
            VK_RIGHT => "Right",
            VK_DOWN => "Down",
            VK_INSERT => "Insert",
            VK_DELETE => "Delete",
            _ => "VK" + vk,
        };
    }

    public static bool TryParse(string name, out int vk)
    {
        vk = 0;
        if (string.IsNullOrWhiteSpace(name)) return false;
        name = name.Trim();
        if (name.Length == 1 && char.IsLetter(name[0])) { vk = char.ToUpperInvariant(name[0]); return true; }
        if (name.Length == 1 && char.IsDigit(name[0])) { vk = name[0]; return true; }
        string upper = name.ToUpperInvariant();
        if (upper.StartsWith("F") && int.TryParse(upper[1..], out int f) && f >= 1 && f <= 24) { vk = 0x70 + f - 1; return true; }
        if (upper.StartsWith("NUMPAD") && int.TryParse(upper[6..], out int n) && n <= 9) { vk = 0x60 + n; return true; }
        int result = 0;
        bool ok = upper switch
        {
            "LBUTTON" => TrySet(KeyCodes.VK_LBUTTON),
            "RBUTTON" => TrySet(KeyCodes.VK_RBUTTON),
            "MBUTTON" => TrySet(KeyCodes.VK_MBUTTON),
            "XBUTTON1" => TrySet(KeyCodes.VK_XBUTTON1),
            "XBUTTON2" => TrySet(KeyCodes.VK_XBUTTON2),
            "BACKSPACE" => TrySet(KeyCodes.VK_BACK),
            "TAB" => TrySet(KeyCodes.VK_TAB),
            "ENTER" => TrySet(KeyCodes.VK_RETURN),
            "SHIFT" => TrySet(KeyCodes.VK_SHIFT),
            "CTRL" or "CONTROL" => TrySet(KeyCodes.VK_CONTROL),
            "ALT" => TrySet(KeyCodes.VK_MENU),
            "PAUSE" => TrySet(KeyCodes.VK_PAUSE),
            "CAPSLOCK" => TrySet(KeyCodes.VK_CAPITAL),
            "ESC" or "ESCAPE" => TrySet(KeyCodes.VK_ESCAPE),
            "SPACE" => TrySet(KeyCodes.VK_SPACE),
            "PGUP" => TrySet(KeyCodes.VK_PRIOR),
            "PGDN" => TrySet(KeyCodes.VK_NEXT),
            "END" => TrySet(KeyCodes.VK_END),
            "HOME" => TrySet(KeyCodes.VK_HOME),
            "LEFT" => TrySet(KeyCodes.VK_LEFT),
            "UP" => TrySet(KeyCodes.VK_UP),
            "RIGHT" => TrySet(KeyCodes.VK_RIGHT),
            "DOWN" => TrySet(KeyCodes.VK_DOWN),
            "INSERT" => TrySet(KeyCodes.VK_INSERT),
            "DELETE" or "DEL" => TrySet(KeyCodes.VK_DELETE),
            _ => false,
        };
        vk = result;
        return ok;

        bool TrySet(int code) { result = code; return true; }
    }
}