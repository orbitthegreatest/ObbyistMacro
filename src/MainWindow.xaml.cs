using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ObbyistMacro.Core;
using ObbyistMacro.Macros;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using DropShadowEffect = System.Windows.Media.Effects.DropShadowEffect;

namespace ObbyistMacro;

public partial class MainWindow : Window
{
    // DWM border-color removal (Windows 11 system window outline).
    private const int DWMWA_BORDER_COLOR = 34;
    private const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);


    private readonly App _app;
    private readonly AppSettings _settings;
    private readonly MacroEngine _engine;

    private readonly Grid[] _pages;
    private readonly TranslateTransform[] _pageMoves;
    private readonly Button[] _tabButtons;
    private readonly Path[] _tabIcons;
    private readonly TextBlock[] _tabLabels;
    private int _activeTab = -1;
    private string _captureTarget;          // which keybind is being captured
    private readonly Random _rng = new();
    private readonly DispatcherTimer _robloxTimer;
    private readonly List<UIElement> _particles = new();
    private CalibrationTipWindow _calibrationTip;

    public MainWindow(MacroEngine engine, AppSettings settings)
    {
        _engine = engine;
        _settings = settings;
        _app = (App)System.Windows.Application.Current;

        InitializeComponent();

        // ClipToBounds only clips children to the rectangular bounding box —
        // it does NOT clip to CornerRadius. Without an explicit rounded Clip,
        // the square content (title bar, status bar, etc.) covers RootBorder's
        // rounded background right up to the corners, so nothing ever actually
        // looked rounded. Keep this Clip's size synced to the border itself
        // since the window is resizable.
        RootBorder.SizeChanged += (_, _) =>
        {
            RootBorder.Clip = new RectangleGeometry(
                new Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight),
                RootBorder.CornerRadius.TopLeft, RootBorder.CornerRadius.TopLeft);
        };

        // AllowsTransparency windows default their HwndSource compositor
        // backing color to white, which bleeds through as a faint white
        // fringe around anti-aliased rounded corners/edges (RootBorder's
        // CornerRadius). Forcing it to Transparent here removes that.
        SourceInitialized += (_, _) =>
        {
            var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            if (hwndSource != null)
            {
                hwndSource.CompositionTarget.BackgroundColor = Colors.Transparent;

                // Windows 11 draws its own thin accent-colored border around
                // top-level windows via DWM, independent of WindowStyle /
                // AllowsTransparency / ResizeMode. That system border is
                // what shows up as the sharp, square-cornered white outline
                // hugging the window bounds. DWMWA_COLOR_NONE turns it off.
                try
                {
                    int color = unchecked((int)DWMWA_COLOR_NONE);
                    DwmSetWindowAttribute(hwndSource.Handle, DWMWA_BORDER_COLOR, ref color, sizeof(int));
                }
                catch { }
            }
        };

        _pages = new[] { PageHome, PageFps, PageWallhop, PageFreeze, PageAlign, PageWallWalk };
        _pageMoves = new[] { PageHomeMove, PageFpsMove, PageWallhopMove, PageFreezeMove, PageAlignMove, PageWallWalkMove };
        _tabButtons = new[] { TabHome, TabFps, TabWallhop, TabFreeze, TabAlign, TabWallWalk };
        _tabIcons = new[] { TabHomeIcon, TabFpsIcon, TabWallhopIcon, TabFreezeIcon, TabAlignIcon, TabWallWalkIcon };
        _tabLabels = new[]
        {
            (TextBlock)((StackPanel)TabHome.Content).Children[1],
            (TextBlock)((StackPanel)TabFps.Content).Children[1],
            (TextBlock)((StackPanel)TabWallhop.Content).Children[1],
            (TextBlock)((StackPanel)TabFreeze.Content).Children[1],
            (TextBlock)((StackPanel)TabAlign.Content).Children[1],
            (TextBlock)((StackPanel)TabWallWalk.Content).Children[1],
        };

        StatusVersion.Text = App.Version;
        VersionBadge.Text = "v" + App.Version;

        WireSettingsControls();
        RefreshAllUi();

        _engine.CaptureKeyPressed = OnKeyCaptured;
        _engine.Notify += msg => Dispatcher.BeginInvoke(() =>
        {
            StatusLeft.Text = msg;
            if (msg.StartsWith("Roblox FPS cap set to") && int.TryParse(msg.Substring(21), out int cap))
            {
                _settings.RobloxFps = cap;
                FpsBox.Text = cap.ToString();
                Save();
                _calibrationTip ??= new CalibrationTipWindow();
                _calibrationTip.ShowTip(msg, 1500);
            }
            RefreshChips();
        });
        _engine.IsOwnWindowFocused = () =>
            Roblox.GetForegroundWindowHandle() == new WindowInteropHelper(this).Handle;

        _engine.SuspendChanged += suspended => Dispatcher.BeginInvoke(() => OnSuspendChanged(suspended));

        _robloxTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _robloxTimer.Tick += (s, e) => UpdateRobloxStatus();
        _robloxTimer.Start();
        UpdateRobloxStatus();

        // Re-detect the keyboard layout whenever the window regains focus,
        // so layout switches (Alt+Shift) show up instantly in the Align tab.
        Activated += (s, e) => RefreshChips();

        Loaded += (s, e) =>
        {
            WireButtonAnimations();
            StartAmbientAnimation();
            StartHeroAnimation();
            StartRobloxDotPulse();
            SwitchTab(0);
        };
    }

    // =====================================================================
    //  Tab switching
    // =====================================================================
    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        int index = int.Parse((string)((Button)sender).Tag);
        SwitchTab(index);
    }

    private int _fadeToken;

    private void SwitchTab(int index)
    {
        if (index == _activeTab) return;
        int token = ++_fadeToken;
        if (_activeTab >= 0)
        {
            // fade out current page quickly, then collapse it so it stops
            // intercepting hit tests on the newly shown page
            var oldPage = _pages[_activeTab];
            var outAnim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(110));
            outAnim.Completed += (s, e) =>
            {
                if (token == _fadeToken) oldPage.Visibility = Visibility.Collapsed;
            };
            oldPage.BeginAnimation(OpacityProperty, outAnim);
            var outMove = new DoubleAnimation(-8, TimeSpan.FromMilliseconds(110))
            { EasingFunction = (EasingFunctionBase)FindResource("EaseInCubic") };
            _pageMoves[_activeTab].BeginAnimation(TranslateTransform.YProperty, outMove);
        }

        _activeTab = index;
        var page = _pages[index];
        page.Visibility = Visibility.Visible;

        var inAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
        { EasingFunction = (EasingFunctionBase)FindResource("EaseOutCubic") };
        page.BeginAnimation(OpacityProperty, inAnim);

        var inMove = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(340))
        { EasingFunction = (EasingFunctionBase)FindResource("EaseOutCubic") };
        _pageMoves[index].BeginAnimation(TranslateTransform.YProperty, inMove);

        var slide = new DoubleAnimation(index * 150, TimeSpan.FromMilliseconds(320))
        { EasingFunction = (EasingFunctionBase)FindResource("EaseOutCubic") };
        TabIndicatorMove.BeginAnimation(TranslateTransform.XProperty, slide);

        var accent = (SolidColorBrush)FindResource("AccentBrush");
        var dim = (SolidColorBrush)FindResource("TextDimBrush");
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            bool active = i == index;
            _tabIcons[i].Stroke = active ? accent : dim;
            _tabLabels[i].Foreground = active ? accent : dim;
        }
    }

    // =====================================================================
    //  Title bar buttons (animated)
    // =====================================================================
    private void WireButtonAnimations()
    {
        WireTitleButton(TrayButton, "YellowGlow", 1.07, 0.93, false);
        WireTitleButton(CloseButton, "RedGlow", 1.07, 0.93, true);
    }

    private void WireTitleButton(Button btn, string glowResource, double hoverScale, double pressScale, bool red)
    {
        var scale = new ScaleTransform(1, 1);
        btn.RenderTransform = scale;
        btn.RenderTransformOrigin = new Point(0.5, 0.5);
        btn.ApplyTemplate();
        var border = (Border)btn.Template.FindName("bd", btn);
        var glow = border?.Effect as DropShadowEffect;

        var ease = (EasingFunctionBase)FindResource("EaseOutCubic");
        btn.MouseEnter += (s, e) =>
        {
            if (glow != null)
            {
                var glowAnim = new DoubleAnimation(red ? 0.85 : 0.9, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease };
                glow.BeginAnimation(DropShadowEffect.OpacityProperty, glowAnim);
            }
            var scaleAnim = new DoubleAnimation(hoverScale, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        };
        btn.MouseLeave += (s, e) =>
        {
            if (glow != null)
            {
                var glowAnim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };
                glow.BeginAnimation(DropShadowEffect.OpacityProperty, glowAnim);
            }
            var scaleAnim = new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        };
        btn.PreviewMouseLeftButtonDown += (s, e) =>
        {
            var scaleAnim = new DoubleAnimation(pressScale, TimeSpan.FromMilliseconds(70)) { EasingFunction = ease };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        };
        btn.PreviewMouseLeftButtonUp += (s, e) =>
        {
            var scaleAnim = new DoubleAnimation(hoverScale, TimeSpan.FromMilliseconds(120)) { EasingFunction = ease };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        };
    }

    private void TrayButton_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => _app.ExitApp();

    public void HideToTray()
    {
        Hide();
        _app.ShowTrayBalloon();
    }

    public void ShowFromTray()
    {
        Show();
        Activate();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    // =====================================================================
    //  Settings controls
    // =====================================================================
    private void WireSettingsControls()
    {
        SensBox.TextChanged += (s, e) => ParseSensitivity();
        SensBox.LostKeyboardFocus += (s, e) => SensBox.Text = _settings.RobloxSensitivity > 0 ? _settings.RobloxSensitivity.ToString("0.###") : "";
        FpsBox.TextChanged += (s, e) => ParseFps();
        FpsBox.LostKeyboardFocus += (s, e) => FpsBox.Text = _settings.RobloxFps.ToString();

        StartMinToggle.Checked += (s, e) => { _settings.StartMinimized = true; Save(); };
        StartMinToggle.Unchecked += (s, e) => { _settings.StartMinimized = false; Save(); };

        FpsEnablePageToggle.Checked += (s, e) => SetFpsEnabled(true);
        FpsEnablePageToggle.Unchecked += (s, e) => SetFpsEnabled(false);
        WallhopEnablePageToggle.Checked += (s, e) => SetWallhopEnabled(true);
        WallhopEnablePageToggle.Unchecked += (s, e) => SetWallhopEnabled(false);
        WallhopJumpToggle.Checked += (s, e) => SetWallhopJump(true);
        WallhopJumpToggle.Unchecked += (s, e) => SetWallhopJump(false);
        FreezeEnablePageToggle.Checked += (s, e) => SetFreezeEnabled(true);
        FreezeEnablePageToggle.Unchecked += (s, e) => SetFreezeEnabled(false);
        AlignEnablePageToggle.Checked += (s, e) => SetAlignEnabled(true);
        AlignEnablePageToggle.Unchecked += (s, e) => SetAlignEnabled(false);
        WallWalkEnablePageToggle.Checked += (s, e) => SetWallWalkEnabled(true);
        WallWalkEnablePageToggle.Unchecked += (s, e) => SetWallWalkEnabled(false);
    }

    private void ParseSensitivity()
    {
        if (double.TryParse(SensBox.Text.Replace(',', '.'), out double v))
        {
            _settings.RobloxSensitivity = Math.Clamp(v, 0.01, 10);
            Save();
            RefreshChips();
        }
    }

    private void ParseFps()
    {
        if (int.TryParse(FpsBox.Text, out int v))
        {
            _settings.RobloxFps = Math.Clamp(v, 15, 300);
            Save();
            RefreshChips();
        }
    }

    private void SetFpsEnabled(bool on)
    {
        _settings.Fps.Enabled = on;
        Save();
        RefreshMacroStates();
    }

    private void SetWallhopEnabled(bool on)
    {
        _settings.Wallhop.Enabled = on;
        Save();
        RefreshMacroStates();
    }

    private void SetWallhopJump(bool on)
    {
        _settings.Wallhop.Jump = on;
        Save();
    }

    private void SetFreezeEnabled(bool on)
    {
        _settings.Freeze.Enabled = on;
        Save();
        RefreshMacroStates();
    }

    private void SetAlignEnabled(bool on)
    {
        _settings.Align.Enabled = on;
        Save();
        RefreshMacroStates();
    }

    private void SetWallWalkEnabled(bool on)
    {
        _settings.WallWalk.Enabled = on;
        Save();
        RefreshMacroStates();
    }

    private void Save() => SettingsService.Save(_settings);

    // =====================================================================
    //  Keybind capture
    // =====================================================================
    private void FpsKeyButton_Click(object sender, RoutedEventArgs e) => StartCapture("fps", FpsKeyButton);
    private void WallhopKeyButton_Click(object sender, RoutedEventArgs e) => StartCapture("wallhop", WallhopKeyButton);
    private void FreezeKeyButton_Click(object sender, RoutedEventArgs e) => StartCapture("freeze", FreezeKeyButton);
    private void AlignLeftKeyButton_Click(object sender, RoutedEventArgs e) => StartCapture("alignleft", AlignLeftKeyButton);
    private void AlignRightKeyButton_Click(object sender, RoutedEventArgs e) => StartCapture("alignright", AlignRightKeyButton);
    private void WallWalkKeyButton_Click(object sender, RoutedEventArgs e) => StartCapture("wallwalk", WallWalkKeyButton);
    private void SuspendKeyButton_Click(object sender, RoutedEventArgs e) => StartCapture("suspend", SuspendKeyButton);

    private void StartCapture(string target, Button btn)
    {
        _captureTarget = target;
        _engine.StartCapture();
        btn.Content = "Press a key or click a button... (Esc cancels)";
        ShowToast("Press any key or mouse button... (Esc cancels)");
    }

    private void OnKeyCaptured(int vk)
    {
        var btn = _captureTarget switch
        {
            "fps" => FpsKeyButton,
            "wallhop" => WallhopKeyButton,
            "freeze" => FreezeKeyButton,
            "alignleft" => AlignLeftKeyButton,
            "alignright" => AlignRightKeyButton,
            "wallwalk" => WallWalkKeyButton,
            _ => SuspendKeyButton,
        };
        if (vk == 0)
        {
            btn.Content = DisplayKey(_captureTarget);
            ShowToast("Capture cancelled.");
            _captureTarget = null;
            return;
        }

        string name = KeyCodes.Name(vk);
        switch (_captureTarget)
        {
            case "fps": _settings.Fps.Key = name; break;
            case "wallhop": _settings.Wallhop.Key = name; break;
            case "freeze": _settings.Freeze.Key = name; break;
            case "alignleft": _settings.Align.LeftHotkey = name; break;
            case "alignright": _settings.Align.RightHotkey = name; break;
            case "wallwalk": _settings.WallWalk.Key = name; break;
            case "suspend": _settings.SuspendKey = name; break;
        }
        btn.Content = name;
        Save();
        RefreshMacroStates();
        ShowToast("Hotkey set: " + name);
        _captureTarget = null;
    }

    private string DisplayKey(string target) => target switch
    {
        "fps" => _settings.Fps.Key,
        "wallhop" => _settings.Wallhop.Key,
        "freeze" => _settings.Freeze.Key,
        "alignleft" => _settings.Align.LeftHotkey,
        "alignright" => _settings.Align.RightHotkey,
        "wallwalk" => _settings.WallWalk.Key,
        _ => _settings.SuspendKey,
    };

    // =====================================================================
    //  Global suspend
    // =====================================================================
    private void OnSuspendChanged(bool suspended)
    {
        SuspendStatusText.Text = suspended ? "SUSPENDED — all macros off" : "All macros active";
        SuspendStatusText.Foreground = (Brush)FindResource(suspended ? "TrayYellowBrush" : "AccentBrush");
        _calibrationTip ??= new CalibrationTipWindow();
        _calibrationTip.ShowTip(suspended ? "SUSPENDED — all macros off" : "RESUMED — macros active", 1800);
    }

    // =====================================================================
    //  FPS macro actions
    // =====================================================================
    private void CalibrateButton_Click(object sender, RoutedEventArgs e)
    {
        if (FpsMacro.CalibrationController.Active) return; // already calibrating
        CalibrateButton.IsEnabled = false;
        _calibrationTip ??= new CalibrationTipWindow();
        _engine.Fps.StartCalibration(
            status => Dispatcher.BeginInvoke(() =>
            {
                CalibrationStatus.Text = status;
                _calibrationTip.ShowTip(status);
            }),
            () => Dispatcher.BeginInvoke(() =>
            {
                CalibrateButton.IsEnabled = true;
                _calibrationTip.HideTip();
                Save(); // persist the calibration count
                RefreshAllUi();
            }));
        Roblox.Focus(); // bring Roblox to the foreground so the steps work immediately
    }

    private void StepCount_Click(object sender, RoutedEventArgs e)
    {
        var parts = ((string)((Button)sender).Tag).Split(',');
        string which = parts[0];
        int delta = int.Parse(parts[1]);
        if (which == "fpsdown") _settings.Fps.FpsDown = Math.Max(0, _settings.Fps.FpsDown + delta);
        Save();
        RefreshFpsCounts();
    }

    // =====================================================================
    //  Freeze mode
    // =====================================================================
    private void FreezeMode_Click(object sender, RoutedEventArgs e)
    {
        string mode = (string)((Button)sender).Tag;
        _settings.Freeze.Mode = mode;
        Save();
        UpdateFreezeModeUi();
        ShowToast("Freeze mode: " + mode);
    }

    private void UpdateFreezeModeUi()
    {
        bool toggle = _settings.Freeze.Mode != "Hold";
        var accent = (SolidColorBrush)FindResource("AccentBrush");
        var soft = (SolidColorBrush)FindResource("AccentSoftBrush");
        ToggleModeWrap.Background = toggle ? soft : (Brush)new SolidColorBrush(Color.FromRgb(0x24, 0x1D, 0x3C));
        ToggleModeWrap.BorderBrush = toggle ? accent : (Brush)new SolidColorBrush(Color.FromRgb(0x3A, 0x2F, 0x5E));
        HoldModeWrap.Background = !toggle ? soft : (Brush)new SolidColorBrush(Color.FromRgb(0x24, 0x1D, 0x3C));
        HoldModeWrap.BorderBrush = !toggle ? accent : (Brush)new SolidColorBrush(Color.FromRgb(0x3A, 0x2F, 0x5E));
        ToggleModeBtn.Foreground = toggle ? accent : (Brush)FindResource("TextDimBrush");
        HoldModeBtn.Foreground = !toggle ? accent : (Brush)FindResource("TextDimBrush");
    }

    // =====================================================================
    //  Wall walk mode
    // =====================================================================
    private void WallWalkMode_Click(object sender, RoutedEventArgs e)
    {
        string mode = (string)((Button)sender).Tag;
        _settings.WallWalk.Mode = mode;
        Save();
        UpdateWallWalkModeUi();
        ShowToast("Wall walk mode: " + mode);
    }

    private void UpdateWallWalkModeUi()
    {
        bool toggle = _settings.WallWalk.Mode != "Hold";
        var accent = (SolidColorBrush)FindResource("AccentBrush");
        var soft = (SolidColorBrush)FindResource("AccentSoftBrush");
        WallWalkToggleWrap.Background = toggle ? soft : (Brush)new SolidColorBrush(Color.FromRgb(0x24, 0x1D, 0x3C));
        WallWalkToggleWrap.BorderBrush = toggle ? accent : (Brush)new SolidColorBrush(Color.FromRgb(0x3A, 0x2F, 0x5E));
        WallWalkHoldWrap.Background = !toggle ? soft : (Brush)new SolidColorBrush(Color.FromRgb(0x24, 0x1D, 0x3C));
        WallWalkHoldWrap.BorderBrush = !toggle ? accent : (Brush)new SolidColorBrush(Color.FromRgb(0x3A, 0x2F, 0x5E));
        WallWalkToggleModeBtn.Foreground = toggle ? accent : (Brush)FindResource("TextDimBrush");
        WallWalkHoldModeBtn.Foreground = !toggle ? accent : (Brush)FindResource("TextDimBrush");
    }

    // =====================================================================
    //  UI refresh
    // =====================================================================
    private void RefreshAllUi()
    {
        SensBox.Text = _settings.RobloxSensitivity > 0 ? _settings.RobloxSensitivity.ToString("0.###") : "";
        FpsBox.Text = _settings.RobloxFps.ToString();
        StartMinToggle.IsChecked = _settings.StartMinimized;

        FpsKeyButton.Content = string.IsNullOrEmpty(_settings.Fps.Key) ? "(none)" : _settings.Fps.Key;
        WallhopKeyButton.Content = string.IsNullOrEmpty(_settings.Wallhop.Key) ? "(none)" : _settings.Wallhop.Key;
        FreezeKeyButton.Content = string.IsNullOrEmpty(_settings.Freeze.Key) ? "(none)" : _settings.Freeze.Key;
        AlignLeftKeyButton.Content = string.IsNullOrEmpty(_settings.Align.LeftHotkey) ? "(none)" : _settings.Align.LeftHotkey;
        AlignRightKeyButton.Content = string.IsNullOrEmpty(_settings.Align.RightHotkey) ? "(none)" : _settings.Align.RightHotkey;
        WallWalkKeyButton.Content = string.IsNullOrEmpty(_settings.WallWalk.Key) ? "(none)" : _settings.WallWalk.Key;
        SuspendKeyButton.Content = string.IsNullOrEmpty(_settings.SuspendKey) ? "(none)" : _settings.SuspendKey;

        FpsEnablePageToggle.IsChecked = _settings.Fps.Enabled;
        WallhopEnablePageToggle.IsChecked = _settings.Wallhop.Enabled;
        WallhopJumpToggle.IsChecked = _settings.Wallhop.Jump;
        FreezeEnablePageToggle.IsChecked = _settings.Freeze.Enabled;
        AlignEnablePageToggle.IsChecked = _settings.Align.Enabled;
        WallWalkEnablePageToggle.IsChecked = _settings.WallWalk.Enabled;

        RefreshFpsCounts();
        RefreshChips();
        RefreshMacroStates();
        UpdateFreezeModeUi();
        UpdateWallWalkModeUi();
        RefreshCalibrationHint();
    }

    private void RefreshFpsCounts()
    {
        FpsDownValue.Text = _settings.Fps.FpsDown.ToString();
    }

    private void RefreshChips()
    {
        FpsCapChip.Text = _settings.Fps.CurrentCap;
        WallhopSensChip.Text = _settings.RobloxSensitivity > 0 ? _settings.RobloxSensitivity.ToString("0.###") : "–";
        WallhopFpsChip.Text = _settings.RobloxFps.ToString();
        AlignLayoutChip.Text = KeyboardLayout.DisplayName();
        AlignAutoKeysChip.Text = (KeyboardLayout.Resolve(',', out int _, out int _, out bool _, out byte _) ? "," : "?")
            + "  /  " + (KeyboardLayout.Resolve('.', out int _, out int _, out bool _, out byte _) ? "." : "?");
        WallWalkSensChip.Text = _settings.RobloxSensitivity > 0 ? _settings.RobloxSensitivity.ToString("0.###") : "–";
        WallWalkFpsChip.Text = _settings.RobloxFps.ToString();
        WallWalkPixelsChip.Text = _engine.WallWalk.ComputePixels() + " px";
    }

    private void RefreshMacroStates()
    {
        int armed = (_settings.Fps.Enabled ? 1 : 0) + (_settings.Wallhop.Enabled ? 1 : 0)
            + (_settings.Freeze.Enabled ? 1 : 0) + (_settings.Align.Enabled ? 1 : 0)
            + (_settings.WallWalk.Enabled ? 1 : 0);
        ArmedChip.Text = armed + "/5";
    }

    private void RefreshCalibrationHint()
    {
        if (_engine.Fps.IsCalibrated)
            CalibrationStatus.Text = "Calibrated: " + _settings.Fps.FpsDown + " down presses";
        else
            CalibrationStatus.Text = "Not calibrated yet.";
    }

    private void UpdateRobloxStatus()
    {
        bool running = Roblox.IsRunning();
        RobloxStatusText.Text = running ? "Roblox: running" : "Roblox: not running";
        RobloxStatusText.Foreground = running ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("TextFaintBrush");
        RobloxDotBrush.Color = running ? Color.FromRgb(0x3B, 0xFF, 0x88) : Color.FromRgb(0x6E, 0x67, 0x91);
        CalibrateButton.IsEnabled = running;
    }

    // =====================================================================
    //  Toasts
    // =====================================================================
    public void ShowToast(string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ToastHost.Children.Clear();

            var border = new Border { Style = (Style)FindResource("ToastCard") };
            border.Margin = new Thickness(0, 6, 0, 0);
            border.MaxWidth = 380;

            var text = new TextBlock
            {
                Text = message,
                FontFamily = (FontFamily)FindResource("BodyFont"),
                FontSize = 12.5,
                Foreground = (Brush)FindResource("TextBrush"),
                TextWrapping = TextWrapping.Wrap,
            };
            border.Child = text;
            ToastHost.Children.Add(border);

            var slide = new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(260))
            { EasingFunction = (EasingFunctionBase)FindResource("EaseOutCubic") };
            ToastHostMove.BeginAnimation(TranslateTransform.YProperty, slide);

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            { EasingFunction = (EasingFunctionBase)FindResource("EaseOutCubic") };
            border.BeginAnimation(OpacityProperty, fade);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2800) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                var outFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260));
                outFade.Completed += (s2, e2) =>
                {
                    ToastHost.Children.Remove(border);
                    var back = new DoubleAnimation(ToastHostMove.Y + 24, TimeSpan.FromMilliseconds(260));
                    ToastHostMove.BeginAnimation(TranslateTransform.YProperty, back);
                };
                border.BeginAnimation(OpacityProperty, outFade);
            };
            timer.Start();
        });
    }

    // =====================================================================
    //  Ambient animations
    // =====================================================================
    private void StartAmbientAnimation()
    {
        // drifting radial glows
        AddGlow(0.32, 1.0, 0.10, 0.08, 520, 420, TimeSpan.FromSeconds(26), TimeSpan.FromSeconds(30));
        AddGlow(0.22, 0.0, 0.85, 0.92, 460, 380, TimeSpan.FromSeconds(34), TimeSpan.FromSeconds(38));
        AddGlow(0.16, 1.0, 1.05, 0.75, 300, 260, TimeSpan.FromSeconds(42), TimeSpan.FromSeconds(45));

        // floating particles
        for (int i = 0; i < 14; i++)
        {
            AddParticle();
        }
    }

    private void AddGlow(double opacity, double startX, double startY, double endX, double endY,
        double size, TimeSpan dur1, TimeSpan dur2)
    {
        var ellipse = new Ellipse
        {
            Width = size,
            Height = size,
            Opacity = opacity,
            IsHitTestVisible = false,
        };
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x66, 0x3B, 0xFF, 0x88), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x3B, 0xFF, 0x88), 1));
        ellipse.Fill = brush;
        Canvas.SetLeft(ellipse, startX * ActualWidth - size / 2);
        Canvas.SetTop(ellipse, startY * ActualHeight - size / 2);
        AmbientLayer.Children.Add(ellipse);

        var move = new DoubleAnimation(startX * ActualWidth - size / 2, endX * ActualWidth - size / 2, dur1)
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        var moveY = new DoubleAnimation(startY * ActualHeight - size / 2, endY * ActualHeight - size / 2, dur2)
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        ellipse.BeginAnimation(Canvas.LeftProperty, move);
        ellipse.BeginAnimation(Canvas.TopProperty, moveY);
    }

    private void AddParticle()
    {
        var dot = new Ellipse
        {
            Width = _rng.Next(2, 5),
            Height = _rng.Next(2, 5),
            Opacity = 0,
            IsHitTestVisible = false,
        };
        dot.Fill = (Brush)FindResource("AccentBrush");
        double x = _rng.NextDouble() * ActualWidth;
        double y = _rng.NextDouble() * ActualHeight;
        Canvas.SetLeft(dot, x);
        Canvas.SetTop(dot, y);
        AmbientLayer.Children.Add(dot);

        double durationMs = _rng.Next(9000, 18000);
        var rise = new DoubleAnimation(y, y - _rng.Next(60, 180), TimeSpan.FromMilliseconds(durationMs))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        var drift = new DoubleAnimation(x, x + _rng.Next(-40, 40), TimeSpan.FromMilliseconds(durationMs * 1.4))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        var fade = new DoubleAnimation(0, _rng.NextDouble() * 0.5 + 0.15, TimeSpan.FromMilliseconds(durationMs / 2))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        dot.BeginAnimation(Canvas.TopProperty, rise);
        dot.BeginAnimation(Canvas.LeftProperty, drift);
        dot.BeginAnimation(OpacityProperty, fade);
        _particles.Add(dot);
    }

    private void StartHeroAnimation()
    {
        var pulse = new DoubleAnimation(0.85, 1.08, TimeSpan.FromMilliseconds(2400))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        HeroGlowScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        HeroGlowScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);

        var glow = new DoubleAnimation(0, 0.85, TimeSpan.FromMilliseconds(1400))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        HeroLogoGlow.BeginAnimation(DropShadowEffect.OpacityProperty, glow);

        var titleGlow = new DoubleAnimation(0.4, 0.8, TimeSpan.FromMilliseconds(2600))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        TitleLogoGlow.BeginAnimation(DropShadowEffect.OpacityProperty, titleGlow);
    }

    private void StartRobloxDotPulse()
    {
        var pulse = new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(1200))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        RobloxDot.BeginAnimation(OpacityProperty, pulse);
    }

    // =====================================================================
    //  Resizing
    // =====================================================================
    private void ResizeThumb_Drag(object sender, DragDeltaEventArgs e)
    {
        string tag = (string)((Thumb)sender).Tag;
        double minW = MinWidth, minH = MinHeight;
        double left = Left, top = Top, width = Width, height = Height;

        if (tag.Contains("W"))
        {
            double delta = Math.Min(e.HorizontalChange, width - minW);
            left += delta;
            width -= delta;
        }
        else if (tag.Contains("E"))
        {
            width = Math.Max(minW, width + e.HorizontalChange);
        }
        if (tag.Contains("N"))
        {
            double delta = Math.Min(e.VerticalChange, height - minH);
            top += delta;
            height -= delta;
        }
        else if (tag.Contains("S"))
        {
            height = Math.Max(minH, height + e.VerticalChange);
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }
}