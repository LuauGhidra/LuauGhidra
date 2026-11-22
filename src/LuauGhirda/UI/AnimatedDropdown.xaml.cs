using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using LuauGhirda.Core;

namespace LuauGhirda.UI;

/// <summary>
/// A dropdown whose open/close is a fully authored motion sequence:
/// panel springs in with Back easing, items cascade in with a stagger,
/// the chevron flips, and the whole thing collapses smoothly on close.
/// Rendered in the window's overlay host so nothing clips it.
/// Visual: dark rounded panel, violet selection marker.
/// </summary>
public partial class AnimatedDropdown : UserControl
{
    private readonly List<FrameworkElement> _rows = new();
    private readonly List<Tween> _openTweens = new();

    private Border? _panel;
    private Border? _backdrop;
    private ScaleTransform? _panelScale;
    private TranslateTransform? _panelMove;
    private bool _open;

    private IReadOnlyList<string> _items = Array.Empty<string>();

    public event Action<int>? SelectionChanged;

    public int SelectedIndex { get; private set; } = -1;

    public string SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] : "";

    public AnimatedDropdown()
    {
        InitializeComponent();
        MouseEnter += (_, _) => TweenHeader(true);
        MouseLeave += (_, _) => TweenHeader(false);
        Unloaded += (_, _) => KillTweens();
    }

    public void SetItems(IReadOnlyList<string> items, int initialIndex = 0)
    {
        _items = items;
        SelectedIndex = items.Count > 0 ? Math.Clamp(initialIndex, 0, items.Count - 1) : -1;
        SelectedText.Text = SelectedItem;
    }

    public void Select(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        SelectedIndex = index;
        SelectedText.Text = _items[index];
    }

    // ---------------------------------------------------------------- header hover

    private void TweenHeader(bool entering)
    {
        if (_open) return;
        _openTweens.RemoveAll(t => !t._alive);
        _openTweens.Add(UiTween.Color(
            entering ? Color.FromRgb(0x10, 0x10, 0x16) : Color.FromRgb(0x16, 0x16, 0x21),
            entering ? Color.FromRgb(0x16, 0x16, 0x21) : Color.FromRgb(0x10, 0x10, 0x16),
            0.16,
            c => HeaderBorder.Background = new SolidColorBrush(c)));
        _openTweens.Add(UiTween.Color(
            entering ? Color.FromRgb(0x22, 0x22, 0x2C) : Color.FromRgb(0x2E, 0x2E, 0x3C),
            entering ? Color.FromRgb(0x2E, 0x2E, 0x3C) : Color.FromRgb(0x22, 0x22, 0x2C),
            0.16,
            c => HeaderBorder.BorderBrush = new SolidColorBrush(c)));
    }

    // ---------------------------------------------------------------- open / close

    private void OnHeaderClick(object sender, MouseButtonEventArgs e)
    {
        if (_open) Close(); else Open();
    }

    private void Open()
    {
        if (_items.Count == 0 || _open) return;
        var window = Window.GetWindow(this);
        var overlay = OverlayService.Resolve(this);
        if (window is null || overlay is null) return;

        _open = true;
        KillTweens();

        // Chevron flip.
        TweenManager.Fire(0.24, Easing.Resolve("quint", EaseMode.Out),
            p => (Chevron.RenderTransform as RotateTransform)!.Angle = 180 * p);

        // Backdrop swallows outside clicks.
        _backdrop = new Border
        {
            Background = Brushes.Transparent,
            Width = window.ActualWidth,
            Height = window.ActualHeight,
        };
        _backdrop.PreviewMouseLeftButtonDown += (_, _) => Close();
        overlay.Children.Add(_backdrop);

        // The items panel.
        _panelScale = new ScaleTransform(0.92, 0.6);
        _panelMove = new TranslateTransform(0, -6);
        var group = new TransformGroup();
        group.Children.Add(_panelScale);
        group.Children.Add(_panelMove);

        var host = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromRgb(0x19, 0x19, 0x24)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x3C)),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                BlurRadius = 22,
                ShadowDepth = 6,
                Opacity = 0.5,
                Color = Colors.Black,
            },
            RenderTransform = group,
            RenderTransformOrigin = new Point(0.5, 0),
            Opacity = 0,
            MaxHeight = 300,
        };

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            ClipToBounds = true,
        };
        var stack = new StackPanel { Margin = new Thickness(4) };

        _rows.Clear();
        for (int i = 0; i < _items.Count; i++)
        {
            var row = BuildRow(i);
            row.Opacity = 0;
            var rowMove = new TranslateTransform(0, -8);
            row.RenderTransform = rowMove;
            stack.Children.Add(row);
            _rows.Add(row);
        }

        scroll.Content = stack;
        host.Child = scroll;

        // Position under the header, inside the overlay.
        var origin = HeaderBorder.TransformToVisual(overlay).Transform(new Point(0, HeaderBorder.ActualHeight + 5));
        host.HorizontalAlignment = HorizontalAlignment.Left;
        host.VerticalAlignment = VerticalAlignment.Top;
        host.Margin = new Thickness(origin.X, origin.Y, 0, 0);
        host.Width = Math.Max(HeaderBorder.ActualWidth, 190);

        _panel = host;
        overlay.Children.Add(host);

        // Spring the panel in.
        _openTweens.Add(UiTween.Fade(host, 1.0, 0.15, "cubic"));
        _openTweens.Add(TweenManager.Fire(0.3, Easing.Resolve("back", EaseMode.Out), p =>
        {
            _panelScale.ScaleX = 0.92 + 0.08 * p;
            _panelScale.ScaleY = 0.6 + 0.4 * p;
            _panelMove.Y = -6 + 6 * p;
        }));

        // Stagger the rows.
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var move = (TranslateTransform)row.RenderTransform;
            double delay = 0.04 + i * 0.016;
            _openTweens.Add(UiTween.Fade(row, 1.0, 0.2, "quint", EaseMode.Out, delay));
            _openTweens.Add(TweenManager.Fire(0.26, Easing.Resolve("back", EaseMode.Out),
                p => move.Y = -8 + 8 * p, null, delay));
        }
    }

    private Border BuildRow(int index)
    {
        bool selected = index == SelectedIndex;
        var text = new TextBlock
        {
            Text = _items[index],
            FontSize = 11.5,
            Foreground = selected
                ? new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC))
                : new SolidColorBrush(Color.FromRgb(0xD6, 0xD6, 0xE0)),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var hover = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x30)),
            Opacity = 0,
        };

        var marker = new TextBlock
        {
            Text = "▪",
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC)),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = selected ? 1 : 0,
        };

        var row = new Border
        {
            Height = 32,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 1, 0, 1),
            Child = new Grid
            {
                Children =
                {
                    hover,
                    new Grid
                    {
                        Margin = new Thickness(11, 0, 11, 0),
                        Children = { text, marker },
                    },
                },
            },
            Cursor = Cursors.Hand,
            Tag = hover,
        };
        marker.SetValue(Grid.HorizontalAlignmentProperty, HorizontalAlignment.Right);

        row.MouseEnter += (_, _) => UiTween.Number(hover.Opacity, 1, 0.13, v => hover.Opacity = v, "cubic", EaseMode.Out);
        row.MouseLeave += (_, _) => UiTween.Number(hover.Opacity, 0, 0.16, v => hover.Opacity = v, "cubic", EaseMode.Out);
        row.MouseLeftButtonUp += (_, _) =>
        {
            Select(index);
            SelectionChanged?.Invoke(index);
            Close();
        };
        return row;
    }

    private void Close()
    {
        if (!_open || _panel is null) return;
        _open = false;
        var panel = _panel;
        var backdrop = _backdrop;
        _panel = null;
        _backdrop = null;

        KillTweens();
        TweenManager.Fire(0.2, Easing.Resolve("quint", EaseMode.Out),
            p => (Chevron.RenderTransform as RotateTransform)!.Angle = 180 * (1 - p));

        TweenManager.Fire(0.14, Easing.Resolve("cubic", EaseMode.In), p =>
        {
            _panelScale!.ScaleX = 1.0 - 0.08 * p;
            _panelScale.ScaleY = 1.0 - 0.28 * p;
            _panelMove!.Y = -5 * p;
            panel.Opacity = 1 - p;
        }, _ =>
        {
            var overlay = OverlayService.Resolve(this);
            overlay?.Children.Remove(panel);
            if (backdrop is not null && overlay is not null) overlay.Children.Remove(backdrop);
        });
    }

    private void KillTweens()
    {
        foreach (var t in _openTweens) t.Kill();
        _openTweens.Clear();
    }
}
