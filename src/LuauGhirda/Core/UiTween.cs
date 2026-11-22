using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LuauGhirda.Core;

/// <summary>
/// High-level tween helpers for WPF visuals: opacity, translate/scale transforms,
/// colors and staggered group reveals. Everything routes through <see cref="TweenManager"/>,
/// so all motion shares one frame clock and one easing vocabulary.
/// </summary>
public static class UiTween
{
    // ---------- generic value ----------

    public static Tween Number(double from, double to, double dur,
        Action<double> set, string ease = "quint", EaseMode mode = EaseMode.Out,
        double delay = 0, Action? done = null)
    {
        return TweenManager.Fire(dur, Easing.Resolve(ease, mode),
            p => set(from + (to - from) * p),
            _ => done?.Invoke(), delay);
    }

    public static Tween NumberTo(double from, double to, double dur,
        Action<double> set, string ease, EaseMode mode, double delay = 0, Action? done = null)
        => Number(from, to, dur, set, ease, mode, delay, done);

    // ---------- opacity ----------

    public static Tween Fade(UIElement el, double to, double dur = 0.22,
        string ease = "cubic", EaseMode mode = EaseMode.Out, double delay = 0, Action? done = null)
    {
        double from = el.Opacity;
        return Number(from, to, dur, v => el.Opacity = v, ease, mode, delay, done);
    }

    /// <summary>Set opacity to 0 instantly then fade in.</summary>
    public static Tween FadeIn(UIElement el, double dur = 0.32, double delay = 0, string ease = "quint")
    {
        el.Opacity = 0;
        return Fade(el, 1.0, dur, ease, EaseMode.Out, delay);
    }

    // ---------- translate ----------

    public static TranslateTransform TranslateOf(FrameworkElement el)
    {
        switch (el.RenderTransform)
        {
            case TranslateTransform t:
                return t;
            case TransformGroup g:
                foreach (var tr in g.Children)
                    if (tr is TranslateTransform tt) return tt;
                var add = new TranslateTransform();
                g.Children.Add(add);
                return add;
            case Transform nullOrNone when nullOrNone is null || nullOrNone is MatrixTransform or TransformGroup:
                var group = new TransformGroup();
                var trans = new TranslateTransform();
                group.Children.Add(trans);
                el.RenderTransform = group;
                return trans;
            default:
                // Preserve an existing single transform by wrapping it in a group.
                var existing = el.RenderTransform;
                var grp = new TransformGroup();
                var t2 = new TranslateTransform();
                grp.Children.Add(existing);
                grp.Children.Add(t2);
                el.RenderTransform = grp;
                return t2;
        }
    }

    public static Tween Move(FrameworkElement el, double x, double y, double dur = 0.3,
        string ease = "quint", EaseMode mode = EaseMode.Out, double delay = 0, Action? done = null)
    {
        var t = TranslateOf(el);
        double x0 = t.X, y0 = t.Y;
        return TweenManager.Fire(dur, Easing.Resolve(ease, mode), p =>
        {
            t.X = x0 + (x - x0) * p;
            t.Y = y0 + (y - y0) * p;
        }, _ => done?.Invoke(), delay);
    }

    // ---------- scale ----------

    public static ScaleTransform ScaleOf(FrameworkElement el)
    {
        switch (el.RenderTransform)
        {
            case ScaleTransform s:
                return s;
            case TransformGroup g:
                foreach (var tr in g.Children)
                    if (tr is ScaleTransform st) return st;
                var add = new ScaleTransform(1, 1);
                g.Children.Add(add);
                return add;
            default:
                var grp = new TransformGroup();
                var s2 = new ScaleTransform(1, 1);
                grp.Children.Add(s2);
                el.RenderTransform = grp;
                return s2;
        }
    }

    public static Tween Zoom(FrameworkElement el, double to, double dur = 0.25,
        string ease = "back", EaseMode mode = EaseMode.Out, double delay = 0, Action? done = null)
    {
        var s = ScaleOf(el);
        double from = s.ScaleX;
        return TweenManager.Fire(dur, Easing.Resolve(ease, mode), p =>
        {
            var v = from + (to - from) * p;
            s.ScaleX = v;
            s.ScaleY = v;
        }, _ => done?.Invoke(), delay);
    }

    /// <summary>
    /// Press feedback for buttons: squash on press, springy release on let-go.
    /// Call once per element; handlers are attached exactly once.
    /// </summary>
    public static void PressPulse(FrameworkElement el, double depth = 0.94)
    {
        if (ReferenceEquals(el.Tag, "__pressPulse")) return;
        el.Tag = "__pressPulse";
        el.PreviewMouseLeftButtonDown += (_, _) => Zoom(el, depth, 0.06, "cubic", EaseMode.Out);
        el.PreviewMouseLeftButtonUp += (_, _) => Zoom(el, 1.0, 0.34, "back", EaseMode.Out);
        el.MouseLeave += (_, _) => Zoom(el, 1.0, 0.25, "cubic", EaseMode.Out);
    }

    // ---------- color ----------

    public static Tween Color(System.Windows.Media.Color from, System.Windows.Media.Color to, double dur,
        Action<System.Windows.Media.Color> set, string ease = "cubic", EaseMode mode = EaseMode.Out)
    {
        return TweenManager.Fire(dur, Easing.Resolve(ease, mode), p =>
        {
            set(System.Windows.Media.Color.FromRgb(
                (byte)Math.Round(from.R + (to.R - from.R) * p),
                (byte)Math.Round(from.G + (to.G - from.G) * p),
                (byte)Math.Round(from.B + (to.B - from.B) * p)));
        });
    }

    // ---------- group reveals ----------

    /// <summary>
    /// Staggered entrance: each child slides up + fades in, slightly delayed after
    /// the previous one. Used for page transitions and list reveals.
    /// </summary>
    public static void StaggerIn(IEnumerable<UIElement> items, double step = 0.035, double dur = 0.42)
    {
        double i = 0;
        foreach (var el in items)
        {
            double idx = i;
            if (el is FrameworkElement fe)
            {
                var t = TranslateOf(fe);
                t.X = 0;
                t.Y = 18;
            }
            el.Opacity = 0;
            Fade(el, 1.0, dur, "quint", EaseMode.Out, i * step);
            if (el is FrameworkElement fe2)
                Move(fe2, 0, 0, dur, "quint", EaseMode.Out, i * step);
            i++;
        }
    }

    /// <summary>Wobble-free hover lift for cards/panels.</summary>
    public static void HoverLift(FrameworkElement el, bool entering, double lift = -2.0)
    {
        var t = TranslateOf(el);
        Move(el, 0, entering ? lift : 0, 0.24, "cubic", EaseMode.Out);
    }
}
