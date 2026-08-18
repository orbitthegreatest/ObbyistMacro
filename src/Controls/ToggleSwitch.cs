using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using DropShadowEffect = System.Windows.Media.Effects.DropShadowEffect;

namespace ObbyistMacro.Controls;

/// <summary>Animated rounded toggle switch with a sliding thumb and glow.</summary>
public class ToggleSwitch : ToggleButton
{
    private Border _track;
    private Border _thumb;
    private TranslateTransform _thumbTransform;

    static ToggleSwitch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(typeof(ToggleSwitch)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _track = GetTemplateChild("PART_Track") as Border;
        _thumb = GetTemplateChild("PART_Thumb") as Border;
        _thumbTransform = _thumb?.RenderTransform as TranslateTransform;
        if (_track != null)
        {
            _track.Background = new SolidColorBrush(Color.FromArgb(0x26, 0x33, 0x2C, 0x55));
        }
        if (_thumb != null)
        {
            _thumb.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x2C, 0x55));
            _thumb.Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0,
                Color = Color.FromRgb(0x3B, 0xFF, 0x88),
            };
        }
        if (_thumbTransform != null) UpdateThumbPosition(IsChecked == true);
    }

    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        Animate(IsChecked == true);
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        Animate(false);
    }

    private void Animate(bool on)
    {
        if (_track == null || _thumbTransform == null) return;

        Color target = on ? (Color)FindResource("AccentColor") : Color.FromRgb(0x33, 0x2C, 0x55);
        Color trackOn = on ? Color.FromArgb(0x4D, 0x3B, 0xFF, 0x88) : Color.FromArgb(0x26, 0x33, 0x2C, 0x55);

        var trackAnim = new ColorAnimation(trackOn, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        _track.Background.BeginAnimation(SolidColorBrush.ColorProperty, trackAnim);

        var thumbAnim = new ColorAnimation(target, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        _thumb.Background.BeginAnimation(SolidColorBrush.ColorProperty, thumbAnim);

        var move = new DoubleAnimation(on ? 20 : 0, TimeSpan.FromMilliseconds(230))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        _thumbTransform.BeginAnimation(TranslateTransform.XProperty, move);

        var glow = new DoubleAnimation(on ? 0.55 : 0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        if (_thumb.Effect is DropShadowEffect dse)
            dse.BeginAnimation(DropShadowEffect.OpacityProperty, glow);
    }

    private void UpdateThumbPosition(bool on)
    {
        _thumbTransform.X = on ? 20 : 0;
        if (_track.Background is SolidColorBrush tb)
            tb.Color = on ? Color.FromArgb(0x4D, 0x3B, 0xFF, 0x88) : Color.FromArgb(0x26, 0x33, 0x2C, 0x55);
        if (_thumb.Background is SolidColorBrush ob)
            ob.Color = on ? (Color)FindResource("AccentColor") : Color.FromRgb(0x33, 0x2C, 0x55);
        if (_thumb.Effect is DropShadowEffect dse)
            dse.Opacity = on ? 0.55 : 0;
    }
}