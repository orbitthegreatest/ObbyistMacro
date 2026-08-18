using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using DropShadowEffect = System.Windows.Media.Effects.DropShadowEffect;

namespace ObbyistMacro;

/// <summary>
/// Always-on-top, click-through tooltip that follows the cursor.
/// Used to guide the FPS calibration steps while the user is in Roblox.
/// </summary>
public class CalibrationTipWindow : Window
{
    private readonly Border _card;
    private readonly TextBlock _text;
    private readonly DispatcherTimer _followTimer;
    private readonly DispatcherTimer _autoHideTimer;

    public CalibrationTipWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        Width = 340;

        var wrap = new Grid { IsHitTestVisible = false };
        _card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x10, 0x21)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0xFF, 0x88)),
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10),
            Opacity = 0,
        };
        _card.Effect = new DropShadowEffect
        {
            BlurRadius = 18,
            ShadowDepth = 0,
            Opacity = 0.5,
            Color = Color.FromRgb(0x00, 0x00, 0x00),
        };
        _text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 18,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE4, 0xF4)),
        };
        _card.Child = _text;
        wrap.Children.Add(_card);
        Content = wrap;

        _followTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _followTimer.Tick += (s, e) => FollowCursor();
        _autoHideTimer = new DispatcherTimer();
        _autoHideTimer.Tick += (s, e) =>
        {
            _autoHideTimer.Stop();
            HideTip();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // WS_EX_TRANSPARENT | WS_EX_NOACTIVATE: clicks pass through, never steals focus
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
    }

    public void ShowTip(string text) => ShowTip(text, 0);

    public void ShowTip(string text, double autoHideMs)
    {
        if (!IsVisible) Show();
        _text.Text = text;
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160));
        _card.BeginAnimation(OpacityProperty, fade);
        FollowCursor();
        _followTimer.Start();
        _autoHideTimer.Stop();
        if (autoHideMs > 0)
        {
            _autoHideTimer.Interval = TimeSpan.FromMilliseconds(autoHideMs);
            _autoHideTimer.Start();
        }
    }

    public void HideTip()
    {
        _followTimer.Stop();
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (s, e) =>
        {
            if (!_followTimer.IsEnabled) Hide();
        };
        _card.BeginAnimation(OpacityProperty, fade);
    }

    private void FollowCursor()
    {
        GetCursorPos(out POINT pt);
        double scale = 1.0;
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            var m = source.CompositionTarget.TransformFromDevice;
            scale = m.M11;
        }
        double x = pt.X / scale + 18;
        double y = pt.Y / scale + 18;
        var wa = SystemParameters.WorkArea;
        if (x + ActualWidth > wa.Right) x = pt.X / scale - ActualWidth - 12;
        if (y + ActualHeight > wa.Bottom) y = pt.Y / scale - ActualHeight - 12;
        if (x < wa.Left) x = wa.Left;
        if (y < wa.Top) y = wa.Top;
        Left = x;
        Top = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;
}