using System.Diagnostics;
using System.IO;
using ObbyistMacro.Core;

namespace ObbyistMacro.Macros;

/// <summary>
/// Roblox FPS cap changer. Toggles the in-game FPS cap between 60 and 240 using
/// keyboard navigation (Esc -> Tab -> Down xN -> Enter -> Up/Down x10 -> Enter -> Esc),
/// ported from roblox-fps-toggle.ahk.
/// </summary>
public class FpsMacro
{
    private readonly AppSettings _settings;
    private Action<string> _status;
    private Action _finished;
    public event Action<string> Notify;

    public FpsMacro(AppSettings settings) => _settings = settings;

    public bool IsCalibrated => _settings.Fps.FpsDown > 0;

    /// <summary>Toggles the FPS cap (60 &lt;-&gt; 240). Must run while Roblox is focused.</summary>
    public void Toggle()
    {
        string target = _settings.Fps.CurrentCap == "240" ? "60" : "240";
        SetCap(target);
    }

    /// <summary>Runs the full keyboard navigation to set the requested cap.</summary>
    public void SetCap(string target)
    {
        if (!Roblox.IsForeground())
        {
            Notify?.Invoke("Roblox must be focused to change the FPS cap.");
            return;
        }
        if (!IsCalibrated)
        {
            Notify?.Invoke("Not calibrated yet! Open the FPS tab and run Calibration first.");
            return;
        }

        var s = _settings.Fps;

        // Park the cursor at the center-right of the Roblox window before the
        // menu opens, so the menu hover starts somewhere harmless. Positions
        // come from the window's client area, so any resolution, windowed/
        // fullscreen or monitor layout works.
        IntPtr hwnd = Roblox.GetForegroundWindowHandle();
        var (lockX, lockY) = InputSender.WindowClientCenterRight(hwnd);
        DebugLog($"hwnd={hwnd} lock=({lockX},{lockY})");
        int cx = 0, cy = 0;

        // Swallow all real mouse input for the rest of the sequence: the game
        // can no longer be disturbed by hover changes or clicks, no matter what
        // it does to the cursor position itself.
        InputHooks.BlockMouse = true;
        InputSender.ForceCursor(lockX, lockY, maxMs: 500);   // park at center-right (beats the game's lock clip)
        InputSender.ReassertClip(lockX, lockY);
        try
        {
            InputSender.TapScancode(InputSender.SC_ESC);                   // open menu
            Thread.Sleep(150);
            InputSender.TapScancode(InputSender.SC_TAB);                   // focus menu
            Thread.Sleep(400);
            for (int i = 0; i < s.FpsDown; i++)                            // highlight FPS option
            {
                InputSender.TapScancode(InputSender.SC_DOWN, extended: true);
                Thread.Sleep(30);
            }
            Thread.Sleep(250);
            InputSender.TapScancode(InputSender.SC_ENTER);                 // open FPS selector
            Thread.Sleep(300);
            byte key = target == "60" ? InputSender.SC_UP : InputSender.SC_DOWN;
            int count = target == "60" ? (s.UpCount > 0 ? s.UpCount : 10) : (s.DownCount > 0 ? s.DownCount : 10);
            for (int i = 0; i < count; i++)                                // highlight 60/240
            {
                InputSender.TapScancode(key, extended: true);
            }
            Thread.Sleep(300);
            InputSender.TapScancode(InputSender.SC_ENTER);                 // select it
            Thread.Sleep(300);

            InputSender.TapScancode(InputSender.SC_ESC);                   // close menu
            Thread.Sleep(80);
            InputHooks.BlockMouse = false;

            // Roblox's windowed-mode mouse-lock can get stuck holding a native
            // ClipCursor that pins the real cursor in place and won't release —
            // confirmed by hand: toggling fullscreen (F11) is what clears it,
            // not any amount of SetCursorPos/ClipCursor fighting from outside
            // the process. So reproduce that fix directly: flip to fullscreen
            // and back, which forces Roblox to tear down and recreate its
            // window/display surface, resetting its own stuck lock as a side
            // effect. This replaces the old cursor-fight approach entirely,
            // since fighting a stuck native lock from outside the process
            // isn't a race we can win.
            //
            // Log the window rect before/after each tap: if F11 is actually
            // reaching Roblox, the rect should visibly jump to the monitor's
            // full bounds after the first tap and back to the windowed size
            // after the second. If the rect never changes, the key isn't
            // landing at all and no amount of retiming will fix that.
            //
            // Only needed in windowed mode — Roblox fullscreen doesn't hit the
            // stuck-clip bug in the first place, so skip the flicker entirely
            // when the window's already filling its monitor.
            if (!InputSender.IsFullscreen(hwnd))
            {
                DebugLog($"f11-before rect={InputSender.WindowRectLog(hwnd)}");
                InputSender.TapScancode(InputSender.SC_F11, holdMs: 30);       // -> fullscreen, drops the stuck clip
                Thread.Sleep(180);
                DebugLog($"f11-after-1 rect={InputSender.WindowRectLog(hwnd)}");
                InputSender.TapScancode(InputSender.SC_F11, holdMs: 30);       // -> back to windowed
                Thread.Sleep(180);
                DebugLog($"f11-after-2 rect={InputSender.WindowRectLog(hwnd)}");
            }
            else
            {
                DebugLog("f11-skip already-fullscreen");
            }

            // Re-fetch the handle: some display-mode switches recreate the
            // window rather than just restyling it, which would leave hwnd
            // stale and send WindowClientCenter back to a wrong/old rect.
            IntPtr hwndAfter = Roblox.GetForegroundWindowHandle();
            if (hwndAfter != IntPtr.Zero) hwnd = hwndAfter;

            (cx, cy) = InputSender.WindowClientCenter(hwnd);
            InputSender.MoveMouseAbsolute(cx, cy);                         // land it on center now that nothing's clipping it
        }
        finally
        {
            InputHooks.BlockMouse = false;
            DebugLog($"final=({cx},{cy}) cursor={GetCursorLog()}");
        }

        s.CurrentCap = target;
        Notify?.Invoke("Roblox FPS cap set to " + target);
    }

    /// <summary>
    /// Step-by-step calibration guided by the UI: the user presses Esc to open the
    /// Roblox menu, Tab to focus it, then Down until the FPS option is highlighted
    /// and Enter to save. Counts the Down presses. Esc cancels from step 2 on.
    /// </summary>
    public void StartCalibration(Action<string> status, Action finished)
    {
        _status = status;
        _finished = finished;
        CalibrationController.Active = true;
        CalibrationController.Count = 0;
        CalibrationController.Phase = 0;
        CalibrationController.OnKey = vk =>
        {
            switch (CalibrationController.Phase)
            {
                case 0:
                    // Step 1: Esc opens the Roblox menu
                    if (vk == KeyCodes.VK_ESCAPE)
                    {
                        CalibrationController.Phase = 1;
                        status?.Invoke("Step 2/3 — In Roblox, press Tab to focus the menu. (Esc cancels)");
                    }
                    break;
                case 1:
                    if (vk == KeyCodes.VK_ESCAPE) Cancel();
                    else if (vk == KeyCodes.VK_TAB)
                    {
                        CalibrationController.Phase = 2;
                        status?.Invoke("Step 3/3 — Press Down until the FPS option is highlighted, then Enter to save. (Esc cancels)");
                    }
                    break;
                case 2:
                    if (vk == KeyCodes.VK_ESCAPE) Cancel();
                    else if (vk == KeyCodes.VK_RETURN) Save();
                    else if (vk == KeyCodes.VK_DOWN)
                    {
                        CalibrationController.Count++;
                        status?.Invoke("Step 3/3 — Down: " + CalibrationController.Count +
                                       " — press Enter to save, Esc cancels.");
                    }
                    break;
            }
        };
        status?.Invoke("Step 1/3 — In Roblox, press Esc to open the menu.");
    }

    private void Cancel()
    {
        CalibrationController.Active = false;
        _settings.Fps.FpsDown = 0;
        _status?.Invoke("Calibration cancelled.");
        _finished?.Invoke();
    }

    private void Save()
    {
        CalibrationController.Active = false;
        _settings.Fps.FpsDown = CalibrationController.Count;
        _status?.Invoke("Calibrated! " + _settings.Fps.FpsDown + " down presses saved.");
        _finished?.Invoke();
    }

    public static class CalibrationController
    {
        public static bool Active;
        public static int Count;
        public static int Phase;
        public static Action<int> OnKey;
    }

    private static void DebugLog(string line)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ObbyistMacro");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "fps-macro.log"),
                DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + Environment.NewLine);
        }
        catch { }
    }

    private static string GetCursorLog()
    {
        return InputSender.GetCursorPosition(out int x, out int y) ? $"({x},{y})" : "unknown";
    }
}

/// <summary>
/// Wallhop macro, based on Spencer Macro Utilities wallhop defaults:
/// 150-degree flick, jump key Space (optional), flick-back on, 19 ms wallhop
/// length, 0 ms bonus delay. Flick pixels are computed from the global
/// Roblox sensitivity.
/// </summary>
public class WallhopMacro
{
    private readonly AppSettings _settings;

    public WallhopMacro(AppSettings settings) => _settings = settings;

    public int ComputePixels()
    {
        double sens = _settings.RobloxSensitivity;
        if (sens <= 0) sens = 0.01;
        return (int)Math.Round(150 * 720.0 / (360.0 * sens));
    }

    public void Trigger()
    {
        int px = ComputePixels();
        bool jump = _settings.Wallhop.Jump;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            InputSender.MoveMouse(px, 0);                    // initial flick
            if (jump)
                InputSender.SendScancode(InputSender.SC_SPACE);  // hold jump (scan code, like Spencer)
            Thread.Sleep(19);                                // wallhop length (fixed default)
            InputSender.MoveMouse(-px, 0);                   // flick back
            if (jump)
            {
                Thread.Sleep(81);                            // 100 ms total jump hold
                InputSender.SendScancode(InputSender.SC_SPACE, up: true);
            }
        });
    }
}

/// <summary>
/// Freeze macro: suspends / resumes the whole Roblox process (NtSuspendProcess),
/// ported from the Prison Life Macro Suite. Toggle or Hold modes.
/// </summary>
public class FreezeMacro
{
    private readonly AppSettings _settings;
    public event Action<string> Notify;

    private bool _frozen;

    public FreezeMacro(AppSettings settings) => _settings = settings;

    public bool IsFrozen => _frozen;

    public void Toggle()
    {
        if (_frozen) { Resume(); }
        else { Suspend(); }
    }

    public void HoldDown()
    {
        if (!_frozen) Suspend();
    }

    public void HoldUp()
    {
        if (_frozen) Resume();
    }

    private void Suspend()
    {
        bool any = Roblox.Suspend();
        if (any)
        {
            _frozen = true;
            Notify?.Invoke("Roblox frozen.");
        }
    }

    private void Resume()
    {
        bool any = Roblox.Resume();
        if (any)
        {
            _frozen = false;
            Notify?.Invoke("Roblox resumed.");
        }
    }
}

/// <summary>
/// Alignment macro: presses Roblox's legacy camera-alignment keys.
/// Left alignment = ',' (rotate the camera 45° counter-clockwise),
/// right alignment = '.' (45° clockwise). Roblox matches these by character,
/// so the physical keys depend on the keyboard layout (English Canada: , / .;
/// French AZERTY, German, Russian, Dvorak, ...: different keys). The macro
/// auto-detects the active layout and resolves the exact keys. The trigger
/// hotkeys (any key, including mouse buttons) are matched by MacroEngine.
/// </summary>
public class AlignMacro
{
    private readonly AppSettings _settings;

    public AlignMacro(AppSettings settings) => _settings = settings;

    public void Trigger(bool left)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            // Resolve against the active layout every time. Modifier state
            // (Shift / AltGr) is honored: a few layouts (e.g. Canadian
            // Multilingual Standard) type ',' or '.' only with a modifier.
            if (KeyboardLayout.Resolve(left ? ',' : '.', out int _, out int scan, out bool extended, out byte mods))
            {
                bool shift = (mods & 0x01) != 0;
                bool altGr = (mods & 0x06) == 0x06; // Ctrl+Alt = AltGr
                bool ctrl = (mods & 0x02) != 0 && !altGr;
                bool alt = (mods & 0x04) != 0 && !altGr;
                if (ctrl) InputSender.SendVkDown(KeyCodes.VK_CONTROL);
                if (alt) InputSender.SendVkDown(KeyCodes.VK_MENU);
                if (altGr) { InputSender.SendVkDown(KeyCodes.VK_CONTROL); InputSender.SendVkDown(KeyCodes.VK_MENU); }
                if (shift) InputSender.SendVkDown(KeyCodes.VK_SHIFT);
                InputSender.TapScancode((byte)scan, 20, extended);
                if (shift) InputSender.SendVkUp(KeyCodes.VK_SHIFT);
                if (altGr) { InputSender.SendVkUp(KeyCodes.VK_MENU); InputSender.SendVkUp(KeyCodes.VK_CONTROL); }
                if (alt) InputSender.SendVkUp(KeyCodes.VK_MENU);
                if (ctrl) InputSender.SendVkUp(KeyCodes.VK_CONTROL);
                return;
            }
            // Last-resort fallback: US-layout comma/period scan codes.
            InputSender.TapScancode(left ? (byte)0x33 : (byte)0x34, 20);
        });
    }
}

/// <summary>
/// Wall walk macro, ported from Spencer Macro Utilities defaults: a looping
/// flick-right / flick-left that keeps the character glued to walls. Flick
/// distance is computed from the global Roblox sensitivity (Spencer formula:
/// round((360 / sens) * 0.13), i.e. 94 px at 0.5 sens) and the flick timing
/// from the Roblox FPS (one flick per frame, ~73 ms between cycles).
/// Toggle or Hold modes.
/// </summary>
public class WallWalkMacro
{
    private const int BetweenCyclesMs = 73; // Spencer's RobloxWallWalkValueDelay = 72720 µs

    private readonly AppSettings _settings;
    public event Action<string> Notify;

    private volatile bool _running;

    public WallWalkMacro(AppSettings settings) => _settings = settings;

    public bool IsRunning => _running;

    public int ComputePixels()
    {
        double sens = _settings.RobloxSensitivity;
        if (sens <= 0) sens = 0.01;
        return (int)Math.Round((360.0 / sens) * 0.13);
    }

    private int FrameDelayMs()
    {
        int fps = Math.Clamp(_settings.RobloxFps, 1, 300);
        return Math.Max(1, (int)((1000.0 / fps + 0.5) * 1.1));
    }

    public void Toggle()
    {
        if (_running) { Stop(); }
        else { Start(); }
    }

    public void HoldDown()
    {
        if (!_running) Start();
    }

    public void HoldUp()
    {
        if (_running) Stop();
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        ThreadPool.QueueUserWorkItem(_ => Loop());
    }

    public void Stop()
    {
        _running = false;
    }

    private void Loop()
    {
        int px = ComputePixels();
        int delay = FrameDelayMs();
        while (_running)
        {
            if (!Roblox.IsForeground())
            {
                _running = false;
                Notify?.Invoke("Wall walk stopped (Roblox lost focus).");
                break;
            }
            InputSender.MoveMouse(px, 0);
            Thread.Sleep(delay);
            if (!_running) break;
            InputSender.MoveMouse(-px, 0);
            Thread.Sleep(BetweenCyclesMs);
        }
    }
}