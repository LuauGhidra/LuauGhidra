using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LuauGhirda.Core;
using LuauGhirda.Pages;
using LuauGhirda.UI;

namespace LuauGhirda;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, FrameworkElement> _pages = new();
    private readonly Dictionary<string, (Button button, Border activeBg, Icon glyph)> _navItems = new();
    private readonly List<string> _navOrder = new();
    private FrameworkElement? _currentPage;
    private string _currentKey = "";
    private bool _navBusy;
    private TutorialOverlay? _tutorial;

    public MainWindow()
    {
        InitializeComponent();
        OverlayService.Register(OverlayHost);
        BuildNav();
        BuildRuntimeChips();
        WireWindowButtons();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Navigate("deobf", animated: false);
        _ = EnvironmentScanner.ScanAsync().ContinueWith(_ => Dispatcher.Invoke(() =>
        {
            UpdateRuntimeChips();

        }));

        if (!TutorialOverlay.Seen)
        {
            _ = Task.Delay(600).ContinueWith(_ => Dispatcher.Invoke(ShowTutorial));
        }
    }

    // ---------------------------------------------------------------- sidebar

    private void BuildNav()
    {
        var items = new (string key, string icon, string tooltip)[]
        {
            ("deobf", "filecode2", "Deobfuscator"),
            ("about", "info", "About"),
        };

        foreach (var (key, icon, tooltip) in items)
        {
            var activeBg = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = (Brush)FindResource("Brush.AccentGrad"),
                Opacity = 0,
            };

            var glyph = new Icon(icon, 20, new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)));
            var glyphHost = new Grid
            {
                Width = 44,
                Height = 44,
                Children = { activeBg, glyph },
            };

            var button = new Button
            {
                Content = glyphHost,
                Width = 44,
                Height = 44,
                Margin = new Thickness(0, 4, 0, 4),
                Focusable = false,
                Background = Brushes.Transparent,
                Template = PageKit.PurpleTemplate(11),
                ToolTip = tooltip,
                Tag = key,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            button.Click += (_, _) => Navigate(key);
            button.MouseEnter += (_, _) => HoverNav(key, true);
            button.MouseLeave += (_, _) => HoverNav(key, false);

            NavButtons.Children.Add(button);
            _navItems[key] = (button, activeBg, glyph);
            _navOrder.Add(key);
        }
    }

    private void HoverNav(string key, bool entering)
    {
        if (key == _currentKey) return;
        var (_, activeBg, glyph) = _navItems[key];
        UiTween.Fade(activeBg, entering ? 0.6 : 0, 0.16);
        var target = entering
            ? new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF2))
            : new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0));
        UiTween.Color((glyph.Stroke as SolidColorBrush)?.Color ?? Color.FromRgb(0xA0, 0xA0, 0xB0),
            target.Color, 0.16, c => glyph.Stroke = new SolidColorBrush(c));
    }

    private static readonly (string chip, string tool, string url)[] _runtimeLinks =
    {
        ("NODE", "Node.js", "https://nodejs.org/en/download"),
        ("PY3", "Python 3", "https://www.python.org/downloads/"),
        ("LUAU", "Luau runtime", ""),
    };

    private void BuildRuntimeChips()
    {
        foreach (var (chip, tool, url) in _runtimeLinks)
        {
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x46)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var label = new TextBlock
            {
                Text = chip,
                FontSize = 8.5,
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x70)),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 3),
                Tag = (dot, label, tool, url),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            row.Children.Add(dot);
            row.Children.Add(label);
            row.MouseLeftButtonUp += (_, _) => OpenRuntimeLink(tool, url);
            ToolTipService.SetToolTip(row, tool);
            RuntimeChips.Children.Add(row);
        }
    }

    /// <summary>
    /// Missing runtime chip click → open the official download page.
    /// The Luau runtime ships with the app, so it only ever reports status.
    /// </summary>
    private void OpenRuntimeLink(string tool, string url)
    {
        if (string.IsNullOrEmpty(url)) return; // bundled runtime — nothing to download
        var info = EnvironmentScanner.LastScan.FirstOrDefault(t => t.Name == tool);
        if (info is { Found: true }) return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
        }
    }

    public void UpdateRuntimeChips()
    {
        foreach (var child in RuntimeChips.Children)
        {
            if (child is not StackPanel row) continue;
            var (dot, label, tool, url) = ((System.Windows.Shapes.Ellipse, TextBlock, string, string))row.Tag!;

            var info = EnvironmentScanner.LastScan.FirstOrDefault(t => t.Name == tool);
            bool found = info?.Found ?? false;
            dot.Fill = new SolidColorBrush(found
                ? Color.FromRgb(0x4A, 0xDE, 0x80)
                : url.Length == 0 ? Color.FromRgb(0x60, 0x60, 0x70) : Color.FromRgb(0xF5, 0xB8, 0x41));
            label.Foreground = new SolidColorBrush(found
                ? Color.FromRgb(0x8A, 0x8A, 0x98)
                : url.Length == 0 ? Color.FromRgb(0x60, 0x60, 0x70) : Color.FromRgb(0xF5, 0xB8, 0x41));
            ToolTipService.SetToolTip(row, found
                ? $"{tool} — {(info?.Version ?? "installed")}"
                : url.Length == 0
                    ? $"{tool} — ships with the engines folder"
                    : $"{tool} missing — click to open the official download page");
            UiTween.Zoom(dot, 1.8, 0.15, "cubic", EaseMode.Out).Then(
                UiTween.Number(1.8, 1.0, 0.2, v => dot.Height = dot.Width = v, "cubic", EaseMode.Out));
        }
    }

    // ---------------------------------------------------------------- pages

    public void Navigate(string key, bool animated = true)
    {
        if (_currentKey == key && animated) return;
        if (_navBusy) return;

        (string title, string subtitle) = key switch
        {
            "deobf" => ("Deobfuscator", "unwrap luraph / moonsec / prometheus · lua.expert"),
            "about" => ("About", "what this is and where it comes from"),
            _ => ("LuauGhirda", ""),
        };
        PageTitle.Text = title;
        PageSubtitle.Text = subtitle;

        foreach (var (k, (_, activeBg, glyph)) in _navItems)
        {
            bool active = k == key;
            UiTween.Fade(activeBg, active ? 1.0 : 0, active ? 0.22 : 0.2);
            var target = active
                ? Color.FromRgb(0xFF, 0xFF, 0xFF)
                : Color.FromRgb(0xA0, 0xA0, 0xB0);
            UiTween.Color((glyph.Stroke as SolidColorBrush)?.Color ?? target, target, 0.2,
                c => glyph.Stroke = new SolidColorBrush(c));
        }

        var next = GetOrCreatePage(key);
        if (_currentPage is null || !animated)
        {
            Swap(key, next);
            Enter(next);
            return;
        }

        _navBusy = true;
        var old = _currentPage;
        TweenManager.Fire(0.1, Easing.Resolve("cubic", EaseMode.In), p =>
            old.Opacity = 1 - p, _ =>
        {
            Swap(key, next);
            Enter(next);
            _navBusy = false;
        });
    }

    private FrameworkElement GetOrCreatePage(string key)
    {
        if (_pages.TryGetValue(key, out var cached)) return cached;
        FrameworkElement page = key switch
        {
            "deobf" => new DeobfuscatorPage(this),
            "about" => new AboutPage(),
            _ => new AboutPage(),
        };
        _pages[key] = page;
        return page;
    }

    private void Swap(string key, FrameworkElement next)
    {
        PageHost.Content = next;
        _currentPage = next;
        _currentKey = key;
        PageScroll.ScrollToTop();
    }

    private static void Enter(FrameworkElement page)
    {
        page.Opacity = 1;
        var move = UiTween.TranslateOf(page);
        move.Y = 10;
        TweenManager.Fire(0.28, Easing.Resolve("quint", EaseMode.Out), p =>
        {
            move.Y = 10 - 10 * p;
            page.Opacity = p;
        });
        if (page is IStaggeredPage sp)
            UiTween.StaggerIn(sp.StaggerChildren(), 0.05, 0.4);
    }



    // ---------------------------------------------------------------- tutorial

    public void ShowTutorial()
    {
        _tutorial = new TutorialOverlay(OverlayHost, key => Navigate(key, animated: false), () => _tutorial.Show());
    }

    private void OnHelp(object sender, RoutedEventArgs e) => ShowTutorial();

    // ---------------------------------------------------------------- window buttons

    private void WireWindowButtons()
    {
        CloseButton.MouseEnter += (_, _) =>
            CloseGlyph.Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        CloseButton.MouseLeave += (_, _) =>
            CloseGlyph.Stroke = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0));
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        TweenManager.Fire(0.14, Easing.Resolve("cubic", EaseMode.In), p =>
        {
            var s = UiTween.ScaleOf(this);
            s.ScaleX = s.ScaleY = 1 - 0.03 * p;
            RootMargin.Opacity = 1 - p;
        }, _ => Close());
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        bool max = WindowState == WindowState.Maximized;
        RootMargin.Margin = max ? new Thickness(0) : new Thickness(12);
        Chrome.CornerRadius = max ? new CornerRadius(0) : new CornerRadius(10);
    }
}
