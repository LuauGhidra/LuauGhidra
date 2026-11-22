using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LuauGhirda.Core;

namespace LuauGhirda.UI;

/// <summary>
/// Ghirda — the assistant mascot. A cute purple snake (same palette as the logo)
/// drawn as vector shapes: S-curve body, round head, big blinking eyes, blush and
/// a tiny tongue. Idles with a gentle bob, blinks on a loop, and reacts to app
/// events through <see cref="SetMood"/> (faster bob while busy, a hop on success).
/// </summary>
public sealed class Mascot : Viewbox
{
    public enum Mood { Idle, Busy, Happy, Sad }

    private Grid _root = null!;
    private Grid _eyes = null!;
    private Path _tongue = null!;
    private TranslateTransform _bobMove = null!;

    private Tween? _bobTween;
    private Tween? _blinkTween;
    private Tween? _hopTween;
    private Mood _mood = Mood.Idle;
    private readonly Random _rng = new(0x61ADA);

    public Mascot(double size = 120)
    {
        Width = Height = size;
        Child = Build(size / 100.0);
        Hook();
        Unloaded += (_, _) => KillLoops();
    }

    private UIElement Build(double k)
    {
        _root = new Grid { Width = 100 * k, Height = 100 * k };

        var bodyColor = new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6));
        var headColor = new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7));
        var dark = new SolidColorBrush(Color.FromRgb(0x1E, 0x10, 0x33));

        // body: S-curve, tail bottom-left rising to the head at top
        var body = new Path
        {
            Data = Geometry.Parse(
                "M 30 92 C 22 76 34 64 50 68 C 66 72 78 66 78 52 C 78 40 68 33 56 35"),
            Stroke = bodyColor,
            StrokeThickness = 11 * k,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.None,
        };

        var head = new System.Windows.Shapes.Ellipse
        {
            Width = 26 * k,
            Height = 26 * k,
            Fill = headColor,
        };
        head.SetValue(Canvas.LeftProperty, 39 * k);
        head.SetValue(Canvas.TopProperty, 17 * k);

        // tongue: forked, from under the head
        _tongue = new Path
        {
            Data = Geometry.Parse("M 50 41 L 48 47 M 48 47 L 45 50 M 48 47 L 51 50"),
            Stroke = new SolidColorBrush(Color.FromRgb(0xF4, 0x72, 0xB6)),
            StrokeThickness = 2 * k,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.None,
        };

        // eyes: whites + pupils in a grid we can squash for blinking
        _eyes = new Grid { Width = 24 * k, Height = 14 * k };
        _eyes.SetValue(Canvas.LeftProperty, 41 * k);
        _eyes.SetValue(Canvas.TopProperty, 21 * k);
        foreach (var ex in new[] { 5.0, 15.0 })
        {
            var white = new System.Windows.Shapes.Ellipse
            {
                Width = 10 * k,
                Height = 10 * k,
                Fill = Brushes.White,
            };
            white.SetValue(Canvas.LeftProperty, ex * k - 5 * k);
            white.SetValue(Canvas.TopProperty, 2 * k);

            var pupilHost = new Grid
            {
                Width = 4.8 * k,
                Height = 4.8 * k,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            var blink = new ScaleTransform(1, 1);
            pupilHost.RenderTransform = blink;
            pupilHost.Tag = blink;
            pupilHost.SetValue(Canvas.LeftProperty, ex * k - 2.4 * k);
            pupilHost.SetValue(Canvas.TopProperty, 4.6 * k);
            pupilHost.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 4.8 * k,
                Height = 4.8 * k,
                Fill = dark,
            });
            _eyes.Children.Add(white);
            _eyes.Children.Add(pupilHost);
        }

        // blush
        foreach (var (bx, by) in new[] { (43.0, 34.0), (62.0, 33.0) })
        {
            var blush = new System.Windows.Shapes.Ellipse
            {
                Width = 8 * k,
                Height = 5 * k,
                Fill = new SolidColorBrush(Color.FromArgb(110, 0xC0, 0x84, 0xFC)),
            };
            blush.SetValue(Canvas.LeftProperty, bx * k - 4 * k);
            blush.SetValue(Canvas.TopProperty, by * k - 2.5 * k);
            _root.Children.Add(blush);
        }

        var canvas = new Canvas();
        canvas.Children.Add(body);
        canvas.Children.Add(head);
        canvas.Children.Add(_tongue);
        canvas.Children.Add(_eyes);
        _root.Children.Add(canvas);

        _bobMove = new TranslateTransform(0, 0);
        _root.RenderTransform = _bobMove;
        return _root;
    }

    private void Hook()
    {
        StartBob();
        BlinkLoop();
    }

    private double BobPeriod => _mood == Mood.Busy ? 0.85 : 1.7;
    private double BobAmp => _mood == Mood.Sad ? 1.5 : 3.0;

    private void StartBob()
    {
        double period = BobPeriod;
        _bobTween = TweenManager.Fire(period, Easing.Resolve("sine", EaseMode.InOut), p =>
        {
            _bobMove.Y = -BobAmp * Math.Sin(p * Math.PI);
        }, _ => { if (IsLoaded) StartBob(); });
    }

    private void BlinkLoop()
    {
        double wait = 1.6 + _rng.NextDouble() * 2.4;
        _blinkTween = TweenManager.Fire(wait, Easing.Resolve("linear", EaseMode.Out), null, _ =>
        {
            // quick close-open on every pupil (scaleY squash)
            foreach (var child in _eyes.Children)
                if (child is Grid g && g.Tag is ScaleTransform st)
                {
                    _blinkTween = TweenManager.Fire(0.13, Easing.Resolve("sine", EaseMode.InOut), p =>
                    {
                        st.ScaleY = p < 0.5 ? 1 - 0.9 * (p * 2) : 0.1 + 0.9 * ((p - 0.5) * 2);
                    });
                }
            BlinkLoop();
        });
    }

    /// <summary>Change mood: Idle bobs slowly, Busy bobs fast, Happy hops once, Sad slumps.</summary>
    public void SetMood(Mood mood)
    {
        if (_mood == mood) return;
        _mood = mood;
        _bobTween?.Kill();

        if (mood == Mood.Happy)
        {
            _hopTween?.Kill();
            var y0 = _bobMove.Y;
            _hopTween = TweenManager.Fire(0.5, Easing.Resolve("bounce", EaseMode.Out), p =>
            {
                _bobMove.Y = y0 - 10 * Math.Sin(Math.Min(p, 1.0) * Math.PI);
            }, _ => { StartBob(); });
            return;
        }

        StartBob();
    }

    private void KillLoops()
    {
        _bobTween?.Kill();
        _blinkTween?.Kill();
        _hopTween?.Kill();
    }
}
