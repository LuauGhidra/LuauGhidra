using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LuauGhirda.Core;
using LuauGhirda.Pages;

namespace LuauGhirda.UI;

/// <summary>
/// First-launch tutorial: Ghirda the mascot walks the user through the app.
/// Full-window dim overlay with a rounded card — mascot on the left, speech on
/// the right, progress dots, NEXT + SKIP buttons. Steps navigate to the page
/// they describe. Skippable at any point; the seen flag persists in AppData so
/// it only auto-opens once (the rail's help button replays it anytime).
/// </summary>
public sealed class TutorialOverlay
{
    private static string SeenFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LuauGhirda", "tutorial_seen");

    public static bool Seen => File.Exists(SeenFile);

    public static void MarkSeen()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SeenFile)!);
            File.WriteAllText(SeenFile, DateTime.UtcNow.ToString("O"));
        }
        catch { /* non-fatal */ }
    }

    private sealed record Step(string? PageKey, string Title, string Text);

    private static readonly Step[] Steps =
    {
    new(null, "I'm Ghirda.",
        "I live in LuauGhirda, and my job is turning messy, obfuscated bytecode back into readable Lua. " +
        "Hit NEXT for the 30-second tour, or SKIP if you don't need hand-holding."),
    new("deobf", "Feed me a script",
        "Toss a .lua or .luau file onto the drop zone, or hit BROWSE. " +
        "I'll scan the signatures right away, guess the obfuscator, and update the stat cards up top."),
    new("deobf", "Engines on tap",
        "Take your pick: lua.expert, CaoMod Luraph (v14.7–v15), Luraph v15.1–v15.2, MoonSec V3, Prometheus, or luaobfuscator.com. " +
        "Or just leave it on Auto-detect and let me rank the best match for you."),
    new("deobf", "Clean outputs",
        "Skip the console spam. When a run finishes, the Result card lets you REVEAL it in outputs\\, " +
        "SAVE TO… wherever, or hit GET LINK to toss it onto rubis.app. Every dump gets a LuauGhirda ASCII stamp up top."),
    new("about", "Credit where it's due",
        "Every engine here belongs to its original creator and sits in its own directory—the About tab has the full breakdown. " +
        "Keep it clean: only deobfuscate code you own or have permission to inspect."),
    new(null, "Good to go",
        "That's the whole setup. Missing Node or Python? The amber GET button in Runtimes & links points straight to the downloads. " +
        "Hit the ? in the sidebar if you ever need me again."),
    };

    private readonly Panel _host;
    private Border? _card;
    private Border? _backdrop;
    private TextBlock _title = null!;
    private TextBlock _body = null!;
    private StackPanel _dots = null!;
    private Button _next = null!;
    private Button _skip = null!;
    private Mascot _mascot = null!;
    private int _step;
    private readonly Action<string> _navigate;
    private readonly Action _onClosed;

    public TutorialOverlay(Panel host, Action<string> navigate, Action onClosed)
    {
        _host = host;
        _navigate = navigate;
        _onClosed = onClosed;
    }

    public void Show(int startStep = 0)
    {
        Close(instant: true);
        _step = Math.Clamp(startStep, 0, Steps.Length - 1);

        _backdrop = new Border { Background = new SolidColorBrush(Color.FromArgb(0xD9, 0x08, 0x08, 0x0D)) };
        _host.Children.Add(_backdrop);

        _card = new Border
        {
            Width = 640,
            MinHeight = 250,
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x20)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x3C)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 46,
                ShadowDepth = 0,
                Opacity = 0.55,
                Color = Colors.Black,
            },
        };

        var grid = new Grid { MinHeight = 170 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _mascot = new Mascot(150);
        var mascotHost = new Grid
        {
            Width = 160,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 18, 0),
            Children = { _mascot },
        };
        mascotHost.SetValue(Grid.ColumnProperty, 0);
        grid.Children.Add(mascotHost);

        var right = new Grid();
        right.SetValue(Grid.ColumnProperty, 1);
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _title = new TextBlock
        {
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
        };
        _title.SetValue(Grid.RowProperty, 0);

        _body = new TextBlock
        {
            FontSize = 12.5,
            LineHeight = 19,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)),
            Margin = new Thickness(0, 10, 0, 0),
        };
        _body.SetValue(Grid.RowProperty, 1);

        var bottom = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        _dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        bottom.Children.Add(_dots);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        _skip = MakeButton("SKIP", true, () => Close());
        _next = MakeButton("NEXT", false, Next);
        buttons.Children.Add(_skip);
        buttons.Children.Add(_next);
        bottom.Children.Add(buttons);
        bottom.SetValue(Grid.RowProperty, 3);

        right.Children.Add(_title);
        right.Children.Add(_body);
        right.Children.Add(bottom);
        grid.Children.Add(right);
        _card.Child = grid;

        _host.Children.Add(_card);

        // entrance
        _backdrop.Opacity = 0;
        UiTween.Fade(_backdrop, 1.0, 0.25);
        var scale = UiTween.ScaleOf(_card);
        scale.ScaleX = scale.ScaleY = 0.94;
        _card.Opacity = 0;
        UiTween.Fade(_card, 1.0, 0.2);
        TweenManager.Fire(0.34, Easing.Resolve("back", EaseMode.Out), p =>
        {
            scale.ScaleX = scale.ScaleY = 0.94 + 0.06 * p;
        });

        RenderStep();
    }

    private void RenderStep()
    {
        var step = Steps[_step];
        _title.Text = step.Title;
        _body.Text = step.Text;
        _next.Content = _step == Steps.Length - 1 ? "LET'S GO" : "NEXT";

        _dots.Children.Clear();
        for (int i = 0; i < Steps.Length; i++)
        {
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 6,
                Height = 6,
                Margin = new Thickness(2, 0, 2, 0),
                Fill = i == _step
                    ? new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7))
                    : new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x38)),
            };
            _dots.Children.Add(dot);
        }

        if (step.PageKey is not null) _navigate(step.PageKey);
        _mascot.SetMood(_step == Steps.Length - 1 ? Mascot.Mood.Happy : Mascot.Mood.Idle);
    }

    private void Next()
    {
        if (_step >= Steps.Length - 1)
        {
            Close();
            return;
        }
        _step++;
        // quick crossfade on the text
        UiTween.Number(1, 0, 0.1, v => _card!.Opacity = v, "cubic", EaseMode.Out, 0, () =>
        {
            RenderStep();
            UiTween.Number(0, 1, 0.16, v => _card!.Opacity = v, "cubic", EaseMode.Out);
        });
    }

    private void Close(bool instant = false)
    {
        if (_card is null && _backdrop is null) return;
        var card = _card;
        var backdrop = _backdrop;
        _card = null;
        _backdrop = null;

        if (instant)
        {
            Remove(card, backdrop);
            return;
        }

        MarkSeen();
        var scale = UiTween.ScaleOf(card!);
        TweenManager.Fire(0.18, Easing.Resolve("cubic", EaseMode.In), p =>
        {
            card!.Opacity = 1 - p;
            scale.ScaleX = scale.ScaleY = 1 - 0.05 * p;
            backdrop!.Opacity = 1 - p;
        }, _ => Remove(card, backdrop));
        _onClosed.Invoke();
    }

    private void Remove(Border? card, Border? backdrop)
    {
        _host.Children.Remove(card);
        _host.Children.Remove(backdrop);
    }

    private static Button MakeButton(string text, bool ghost, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(18, 9, 18, 9),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        // shared page styles live in PageKit; tutorial is UI-layer so it restyles inline
        if (ghost)
        {
            btn.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x2A));
            btn.Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xC9, 0xD4));
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x38));
            btn.BorderThickness = new Thickness(1);
            btn.Template = FlatTemplate(rounded: 10);
            btn.Margin = new Thickness(0, 0, 10, 0);
        }
        else
        {
            btn.Background = new LinearGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0xA8, 0x55, 0xF7), 0.0),
                    new GradientStop(Color.FromRgb(0x7C, 0x3A, 0xED), 1.0),
                },
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
            };
            btn.Foreground = Brushes.White;
            btn.Template = FlatTemplate(rounded: 10);
        }
        btn.Click += (_, _) => onClick();
        UiTween.PressPulse(btn, 0.96);
        return btn;
    }

    private static System.Windows.Controls.ControlTemplate FlatTemplate(double rounded) =>
        PageKit.PurpleTemplate(rounded);
}
