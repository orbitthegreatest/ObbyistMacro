using ObbyistMacro.Core;

namespace ObbyistMacro.Macros;

/// <summary>
/// Wires the low-level input hooks to the three macros. Handles keybind capture,
/// edge detection and Roblox-foreground gating (mirrors the AHK "IfWinActive" behavior).
/// </summary>
public class MacroEngine : IDisposable
{
    private readonly AppSettings _settings;
    private readonly FpsMacro _fps;
    private readonly WallhopMacro _wallhop;
    private readonly FreezeMacro _freeze;

    private readonly Dictionary<int, bool> _keyHeld = new();
    private bool _busy;

    public bool Capturing { get; private set; }
    public Action<int> CaptureKeyPressed;
    public Action<string> Notify;
    public event Action<string> Log;

    /// <summary>
    /// Returns true when ObbyistMacro's own window is the foreground window.
    /// Calibration ignores keys while our window is focused so the user's
    /// arrow/Enter presses don't leak into the step flow (or click our buttons).
    /// </summary>
    public Func<bool> IsOwnWindowFocused;

    public FpsMacro Fps => _fps;
    public WallhopMacro Wallhop => _wallhop;
    public FreezeMacro Freeze => _freeze;

    public MacroEngine(AppSettings settings)
    {
        _settings = settings;
        _fps = new FpsMacro(settings);
        _wallhop = new WallhopMacro(settings);
        _freeze = new FreezeMacro(settings);
        _fps.Notify += FireNotify;
        _freeze.Notify += FireNotify;
    }

    public void Start()
    {
        InputHooks.KeyDown += OnKeyDown;
        InputHooks.KeyUp += OnKeyUp;
        InputHooks.MouseDown += OnMouseDown;
        InputHooks.MouseUp += OnMouseUp;
        InputHooks.Start();
    }

    public void Stop()
    {
        InputHooks.KeyDown -= OnKeyDown;
        InputHooks.KeyUp -= OnKeyUp;
        InputHooks.MouseDown -= OnMouseDown;
        InputHooks.MouseUp -= OnMouseUp;
        InputHooks.Stop();
    }

    public void StartCapture() => Capturing = true;

    public void StopCapture() => Capturing = false;

    private void FireNotify(string msg)
    {
        Notify?.Invoke(msg);
        Log?.Invoke(msg);
    }

    private void OnKeyDown(int vk)
    {
        if (FpsMacro.CalibrationController.Active)
        {
            if (IsOwnWindowFocused?.Invoke() == true) return;
            FpsMacro.CalibrationController.OnKey?.Invoke(vk);
            return;
        }
        if (Capturing)
        {
            if (vk == KeyCodes.VK_ESCAPE)
            {
                StopCapture();
                CaptureKeyPressed?.Invoke(0); // 0 = cancelled
            }
            else if (!KeyCodes.IsMouseButton(vk))
            {
                StopCapture();
                CaptureKeyPressed?.Invoke(vk);
            }
            return;
        }
        if (_busy || !Roblox.IsForeground()) return;

        if (_settings.Fps.Enabled && MatchKey(_settings.Fps.Key, vk) && !IsHeld(vk))
        {
            MarkHeld(vk);
            RunMacro(() => _fps.Toggle());
        }
        else if (_settings.Wallhop.Enabled && MatchKey(_settings.Wallhop.Key, vk) && !IsHeld(vk))
        {
            MarkHeld(vk);
            RunMacro(() => _wallhop.Trigger());
        }
        else if (_settings.Freeze.Enabled && MatchKey(_settings.Freeze.Key, vk) && !IsHeld(vk))
        {
            MarkHeld(vk);
            if (_settings.Freeze.Mode == "Hold")
            {
                _freeze.HoldDown();
                Notify?.Invoke("Freeze: held");
            }
            else
            {
                RunMacro(_freeze.Toggle);
            }
        }
    }

    private void OnKeyUp(int vk)
    {
        _keyHeld[vk] = false;
        if (_busy || !Roblox.IsForeground()) return;
        if (_settings.Freeze.Enabled && _settings.Freeze.Mode == "Hold" &&
            MatchKey(_settings.Freeze.Key, vk))
        {
            _freeze.HoldUp();
        }
    }

    private void OnMouseDown(InputHooks.MouseButton button)
    {
        int vk = InputHooks.MouseButtonToVk(button);
        if (Capturing)
        {
            StopCapture();
            CaptureKeyPressed?.Invoke(vk);
            return;
        }
        if (_busy || !Roblox.IsForeground()) return;

        if (_settings.Fps.Enabled && MatchKey(_settings.Fps.Key, vk) && !IsHeld(vk))
        {
            MarkHeld(vk);
            RunMacro(() => _fps.Toggle());
        }
        else if (_settings.Wallhop.Enabled && MatchKey(_settings.Wallhop.Key, vk) && !IsHeld(vk))
        {
            MarkHeld(vk);
            RunMacro(() => _wallhop.Trigger());
        }
        else if (_settings.Freeze.Enabled && MatchKey(_settings.Freeze.Key, vk) && !IsHeld(vk))
        {
            MarkHeld(vk);
            if (_settings.Freeze.Mode == "Hold")
            {
                _freeze.HoldDown();
            }
            else
            {
                RunMacro(_freeze.Toggle);
            }
        }
    }

    private void OnMouseUp(InputHooks.MouseButton button)
    {
        int vk = InputHooks.MouseButtonToVk(button);
        _keyHeld[vk] = false;
        if (_busy || !Roblox.IsForeground()) return;
        if (_settings.Freeze.Enabled && _settings.Freeze.Mode == "Hold" &&
            MatchKey(_settings.Freeze.Key, vk))
        {
            _freeze.HoldUp();
        }
    }

    private bool IsHeld(int vk) => _keyHeld.TryGetValue(vk, out bool held) && held;

    private void MarkHeld(int vk) => _keyHeld[vk] = true;

    private void RunMacro(Action action)
    {
        _busy = true;
        // Run on a background thread: the macro sleeps between keys and calls
        // SendInput, which blocks until the app's own low-level hook (installed
        // on the UI thread) services the injected keys. Running it on the hook
        // thread would deadlock the sequence.
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { action(); }
            finally { _busy = false; }
        });
    }

    private static bool MatchKey(string stored, int vk)
        => !string.IsNullOrWhiteSpace(stored) && KeyCodes.TryParse(stored, out int wanted) && wanted == vk;

    public void Dispose() => Stop();
}