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
        Thread.Sleep(150);
        InputSender.TapScancode(InputSender.SC_ESC);                   // close menu

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
}

/// <summary>
/// Wallhop macro, based on Spencer Macro Utilities wallhop defaults:
/// 150-degree flick, jump key Space, flick-back on, 19 ms wallhop length, 0 ms bonus delay.
/// Flick pixels are computed from the global Roblox sensitivity.
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
        ThreadPool.QueueUserWorkItem(_ =>
        {
            InputSender.MoveMouse(px, 0);        // initial flick
            InputSender.KeyDown(KeyCodes.VK_SPACE);  // hold jump
            Thread.Sleep(19);                    // wallhop length (fixed default)
            InputSender.MoveMouse(-px, 0);       // flick back
            Thread.Sleep(81);                    // 100 ms total jump hold
            InputSender.KeyUp(KeyCodes.VK_SPACE);
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