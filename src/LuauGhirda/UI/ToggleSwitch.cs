using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LuauGhirda.Core;

namespace LuauGhirda.UI;

/// <summary>
/// Animated toggle switch: the thumb springs across with Back easing while the
/// track snaps to the violet accent. Builds its own visual tree so every animated
/// element is a direct reference.
/// </summary>
public sealed class ToggleSwitch : ContentControl
{
    private readonly Border _track = new()
    {
        Height = 19,
        Width = 36,
        CornerRadius = new CornerRadius(9.5),
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x38)),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly Border _thumb = new()
    {
        Width = 13,
        Height = 13,
        CornerRadius = new CornerRadius(6.5),
        Background = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)),
        HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(2.5, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly TextBlock _label = new()
    {
        Margin = new Thickness(10, 0, 0, 0),
        FontSize = 10,
        FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
        Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly TranslateTransform _thumbMove = new(0, 0);
    private readonly SolidColorBrush _trackBrush;

    public bool IsOn { get; private set; }

    public event Action<bool>? Toggled;

    public ToggleSwitch(string label, bool initial = false)
    {
        IsOn = initial;
        _label.Text = label;
        _thumb.RenderTransform = _thumbMove;
        _trackBrush = new SolidColorBrush(IsOn ? Color.FromRgb(0xA8, 0x55, 0xF7) : Color.FromRgb(0x1C, 0x1C, 0x26));
        _track.Background = _trackBrush;

        var trackHost = new Grid { Width = 36, Height = 19 };
        trackHost.Children.Add(_track);
        trackHost.Children.Add(_thumb);

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { trackHost, _label },
        };

        Cursor = Cursors.Hand;
        Focusable = false;
        Loaded += (_, _) => ApplyState(instant: true);
        MouseLeftButtonDown += (_, e) =>
        {
            IsOn = !IsOn;
            ApplyState(instant: false);
            Toggled?.Invoke(IsOn);
            e.Handled = true;
        };
        MouseEnter += (_, _) =>
            _label.Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7));
        MouseLeave += (_, _) =>
            _label.Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0));
    }

    private void ApplyState(bool instant)
    {
        double target = IsOn ? 16.5 : 0;
        var trackTo = IsOn ? Color.FromRgb(0xA8, 0x55, 0xF7) : Color.FromRgb(0x1C, 0x1C, 0x26);
        var thumbTo = IsOn ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0xA0, 0xA0, 0xB0);
        var trackFrom = _trackBrush.Color;
        var thumbFrom = ((SolidColorBrush)_thumb.Background).Color;

        if (instant)
        {
            _thumbMove.X = target;
            _trackBrush.Color = trackTo;
            _thumb.Background = new SolidColorBrush(thumbTo);
            return;
        }

        TweenManager.Fire(0.28, Easing.Resolve("back", EaseMode.Out), p =>
        {
            _thumbMove.X = (IsOn ? 0 : 16.5) + (target - (IsOn ? 0 : 16.5)) * p;
            _trackBrush.Color = Blend(trackFrom, trackTo, p);
            _thumb.Background = new SolidColorBrush(Blend(thumbFrom, thumbTo, p));
        });
    }

    private static Color Blend(Color a, Color b, double p) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * p),
        (byte)Math.Round(a.G + (b.G - a.G) * p),
        (byte)Math.Round(a.B + (b.B - a.B) * p));
}
