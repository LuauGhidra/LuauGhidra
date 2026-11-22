using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LuauGhirda.Core;
using LuauGhirda.UI;

namespace LuauGhirda.Pages;

public partial class DeobfuscatorPage : Grid, IStaggeredPage
{
    private readonly MainWindow _window;
    private readonly AnimatedDropdown _engineDropdown = new();
    private readonly TextBox _inputBox = new() { IsReadOnly = true, Text = "" };
    private readonly TextBox _outputBox = new() { IsReadOnly = true, Text = "" };
    private readonly TextBox _linkBox = new() { IsReadOnly = true, Text = "", Visibility = Visibility.Collapsed };
    private readonly TextBlock _detectChip = new();
    private readonly ToggleSwitch _devirt = new("DEVIRTUALIZE VM DISPATCHERS", true);
    private readonly ToggleSwitch _postClean = new("POST-CLEAN PASS (LUACLEAN)", true);
    private readonly ToggleSwitch _names = new("READABLE NAMES (LUAOBFUSCATOR)", false);
    private readonly Button _runButton;
    private readonly Button _linkButton;
    private readonly Border _progressTrack = new();
    private readonly Border _progressFill = new();
    private readonly Rectangle _dropzoneRect = new();
    private readonly TextBlock _dropText = new();

    // result card (replaces the old console — no log dump, just outcome + actions)
    private readonly TextBlock _resultTitle = new();
    private readonly TextBlock _resultDetail = new();
    private readonly Image _resultArt = new();
    private string? _lastNote;

    private readonly PageKit.StatCard _statInput;
    private readonly PageKit.StatCard _statDetected;
    private readonly PageKit.StatCard _statRuntimes;
    private readonly PageKit.StatCard _statLast;

    private readonly Mascot _mascot;
    private readonly TextBlock _bubble = new();
    private readonly StackPanel _runtimeRows = new();
    private Tween? _progressTween;
    private Tween? _zoneTween;
    private Tween? _resultTween;
    private bool _running;
    private bool _sharing;
    private CancellationTokenSource? _cts;

    /// <summary>Dropdown options — lua.expert listed up top as a first-class decompiler.</summary>
    private static readonly (string Label, string? Id)[] EngineChoices =
    {
        ("Auto-detect (recommended)", null),
        ("lua.expert — cloud decompiler", "luaexpert"),
        ("Luraph v15.2", "luraph152"),
        ("Luraph v15.1", "luraph151"),
        ("Luraph (v15 / v14.7 / v14.8 / v14.9)", "caomod"),
        ("MoonSec V3", "moonsec"),
        ("Prometheus", "prometheus"),
        ("luaobfuscator.com (python 3)", "luaobf"),
    };

    public DeobfuscatorPage(MainWindow window)
    {
        _window = window;
        _runButton = PageKit.Primary("RUN DEOBFUSCATION", Run, 190);
        _linkButton = PageKit.Ghost("GET LINK", GetLink, 82);

        // ---- dashboard grid: main column + assistant column ----------------
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

        var main = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        main.SetValue(Grid.ColumnProperty, 0);

        var side = new StackPanel();
        side.SetValue(Grid.ColumnProperty, 1);

        // ---- stat row (like the reference dashboard) -----------------------
        var stats = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        for (int i = 0; i < 4; i++)
            stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _statInput = new PageKit.StatCard("upload", "—", "INPUT SCRIPT");
        _statDetected = new PageKit.StatCard("sparkles", "—", "DETECTED");
        _statRuntimes = new PageKit.StatCard("cpu", "…", "RUNTIMES READY");
        _statLast = new PageKit.StatCard("zap", "—", "LAST RUN");
        var statCards = new[] { _statInput, _statDetected, _statRuntimes, _statLast };
        for (int i = 0; i < statCards.Length; i++)
        {
            statCards[i].Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0);
            statCards[i].SetValue(Grid.ColumnProperty, i);
            stats.Children.Add(statCards[i]);
        }
        main.Children.Add(stats);

        // ---- input card ----------------------------------------------------
        var browse = PageKit.Ghost("BROWSE", Browse, 0);
        browse.VerticalAlignment = VerticalAlignment.Center;
        var inputCard = PageKit.Card("Input", "upload", out var inBody, headerRight: browse);
        inBody.Children.Add(BuildDropzone());

        var pathRow = new Grid { Margin = new Thickness(0, 10, 0, 4) };
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _inputBox.Style = (Style)FindResource("Box");
        _inputBox.SetValue(Grid.ColumnProperty, 0);
        _inputBox.TextChanged += (_, _) => UpdateDetectChip();
        pathRow.Children.Add(_inputBox);
        inBody.Children.Add(pathRow);

        inBody.Children.Add(_detectChip);
        main.Children.Add(inputCard);

        // ---- engine card ---------------------------------------------------
        var autoChip = PageKit.Chip("AUTO-DETECT ON", Color.FromRgb(0xC0, 0x84, 0xFC), Color.FromRgb(0x1D, 0x14, 0x28));
        autoChip.HorizontalAlignment = HorizontalAlignment.Right;
        var engineCard = PageKit.Card("Engine & output", "zap", out var engBody, headerRight: autoChip);

        var engRow = new Grid();
        engRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        engRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        engRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _engineDropdown.SetItems(EngineChoices.Select(c => c.Label).ToArray(), 0);
        _engineDropdown.SelectionChanged += i =>
        {
            autoChip.Text = i == 0 ? "AUTO-DETECT ON" : "MANUAL: " + EngineChoices[i].Id!.ToUpperInvariant();
        };
        var ddHost = new Grid { Children = { _engineDropdown } };
        ddHost.SetValue(Grid.ColumnProperty, 0);
        engRow.Children.Add(ddHost);

        var outputLabel = PageKit.FieldLabel("OUTPUT");
        outputLabel.SetValue(Grid.ColumnProperty, 1);
        engRow.Children.Add(outputLabel);

        _outputBox.Style = (Style)FindResource("Box");
        _outputBox.SetValue(Grid.ColumnProperty, 2);
        engRow.Children.Add(_outputBox);
        engBody.Children.Add(engRow);

        var outHint = PageKit.Micro("OUTPUTS SAVE TO <EXE DIR>\\OUTPUTS — EDITABLE");
        outHint.Margin = new Thickness(2, 8, 0, 0);
        engBody.Children.Add(outHint);

        var options = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 12, 0, 12) };
        options.Children.Add(_devirt);
        options.Children.Add(new Separator { Width = 22, Opacity = 0 });
        options.Children.Add(_postClean);
        options.Children.Add(new Separator { Width = 22, Opacity = 0 });
        options.Children.Add(_names);
        engBody.Children.Add(options);

        var runRow = new Grid();
        runRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        runRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _runButton.SetValue(Grid.ColumnProperty, 0);
        runRow.Children.Add(_runButton);

        _progressTrack.Height = 4;
        _progressTrack.CornerRadius = new CornerRadius(2);
        _progressTrack.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x2C));
        _progressTrack.VerticalAlignment = VerticalAlignment.Center;
        _progressTrack.Margin = new Thickness(18, 0, 0, 0);
        _progressTrack.Opacity = 0;
        _progressTrack.SetValue(Grid.ColumnProperty, 1);
        _progressTrack.ClipToBounds = true;
        _progressFill.Width = 110;
        _progressFill.CornerRadius = new CornerRadius(2);
        _progressFill.Background = (Brush)FindResource("Brush.Accent");
        _progressFill.HorizontalAlignment = HorizontalAlignment.Left;
        var fillMove = new TranslateTransform(-110, 0);
        _progressFill.RenderTransform = fillMove;
        _progressTrack.Child = _progressFill;
        runRow.Children.Add(_progressTrack);
        engBody.Children.Add(runRow);

        main.Children.Add(engineCard);

        // ---- result card (replaces the console) ----------------------------
        main.Children.Add(BuildResultCard());

        // ---- assistant column ----------------------------------------------
        var assistantCard = PageKit.Card("Assistant", "sparkles", out var asBody, bodyPadding: 14);
        _bubble.Text = "Drop a script and I'll tell you what made it — or just press BROWSE. I'm fast, promise.";
        _bubble.FontSize = 11.5;
        _bubble.LineHeight = 17;
        _bubble.TextWrapping = TextWrapping.Wrap;
        _bubble.Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xC9, 0xD6));
        var bubble = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromRgb(0x1D, 0x14, 0x28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x24, 0x4A)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10, 12, 10),
            Child = _bubble,
            Margin = new Thickness(0, 0, 0, 12),
        };
        asBody.Children.Add(bubble);
        _mascot = new Mascot(118);
        asBody.Children.Add(new Grid
        {
            Height = 120,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _mascot },
        });
        side.Children.Add(assistantCard);



        // ---- runtimes & links card (missing tool → official download page) --
        var runtimesCard = PageKit.Card("Runtimes & links", "externallink", out var rtBody);
        rtBody.Children.Add(_runtimeRows);
        rtBody.Children.Add(new TextBlock
        {
            Text = "missing a runtime? press GET — it opens the official download page",
            FontSize = 10,
            FontFamily = PageKit.Mono,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x70)),
            Margin = new Thickness(2, 10, 0, 0),
        });
        side.Children.Add(runtimesCard);

        // ---- illustration card (flat SaaS artwork) --------------------------
        var artCard = PageKit.Card("The workshop", "settings2", out var artBody);
        var art = new Image
        {
            Source = LoadAsset("illust_bug.png"),
            MaxHeight = 168,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 2, 0, 0),
        };
        artBody.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Child = art,
        });
        side.Children.Add(artCard);

        Children.Add(main);
        Children.Add(side);

        AllowDrop = true;
        Drop += OnDrop;
        DragOver += OnDragOver;
        _ = RefreshRuntimeStatAsync();
        SetResult("Ready when you are",
            "outputs land in <exe>\\outputs — REVEAL, SAVE TO… or GET LINK to share on rubis.app",
            ResultKind.Idle);
    }

    public IEnumerable<UIElement> StaggerChildren() => new UIElement[] { Children[0], Children[1] };

    // ---------------------------------------------------------------- result card

    private enum ResultKind { Idle, Busy, Ok, Fail }

    private UIElement BuildResultCard()
    {
        var card = PageKit.Card("Result", "check", out var body);

        var grid = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _resultTitle.Text = "Ready when you are";
        _resultTitle.FontSize = 14.5;
        _resultTitle.FontWeight = FontWeights.SemiBold;
        _resultTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7));
        left.Children.Add(_resultTitle);

        _resultDetail.Text = "";
        _resultDetail.FontFamily = PageKit.Mono;
        _resultDetail.FontSize = 10.5;
        _resultDetail.LineHeight = 15;
        _resultDetail.TextWrapping = TextWrapping.Wrap;
        _resultDetail.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x98));
        _resultDetail.Margin = new Thickness(0, 4, 0, 0);
        left.Children.Add(_resultDetail);

        // actions
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        actions.Children.Add(PageKit.Ghost("REVEAL IN FOLDER", RevealOutput, 118));
        actions.Children.Add(Spacer(10));
        actions.Children.Add(PageKit.Ghost("SAVE TO…", SaveOutputAs, 84));
        actions.Children.Add(Spacer(10));
        actions.Children.Add(_linkButton);
        actions.Children.Add(Spacer(10));
        actions.Children.Add(PageKit.Ghost("COPY PATH", CopyOutputPath, 88));
        left.Children.Add(actions);

        // share link row (appears after a GET LINK upload)
        _linkBox.Style = (Style)FindResource("Box");
        _linkBox.Margin = new Thickness(0, 12, 0, 0);
        _linkBox.FontFamily = PageKit.Mono;
        _linkBox.FontSize = 11;
        _linkBox.Cursor = Cursors.Hand;
        _linkBox.MouseLeftButtonUp += (_, _) => CopyLink();
        _linkBox.ToolTip = "click to copy the rubis.app link";
        left.Children.Add(_linkBox);

        grid.Children.Add(left);
        left.SetValue(Grid.ColumnProperty, 0);

        _resultArt.Source = LoadAsset("illust_bug.png");
        _resultArt.MaxHeight = 92;
        _resultArt.MaxWidth = 200;
        _resultArt.Stretch = Stretch.Uniform;
        _resultArt.VerticalAlignment = VerticalAlignment.Center;
        _resultArt.Margin = new Thickness(14, 0, 0, 0);
        _resultArt.SetValue(Grid.ColumnProperty, 1);
        grid.Children.Add(_resultArt);

        body.Children.Add(grid);
        return card;
    }

    private void SetResult(string title, string detail, ResultKind kind)
    {
        _resultTitle.Text = title;
        _resultDetail.Text = detail;
        Color accent = kind switch
        {
            ResultKind.Ok => Color.FromRgb(0x4A, 0xDE, 0x80),
            ResultKind.Fail => Color.FromRgb(0xF8, 0x71, 0x71),
            ResultKind.Busy => Color.FromRgb(0xC0, 0x84, 0xFC),
            _ => Color.FromRgb(0xF2, 0xF2, 0xF7),
        };
        _resultTitle.Foreground = new SolidColorBrush(accent);
        _resultArt.Source = LoadAsset(kind == ResultKind.Ok ? "illust_celebrate.png" : "illust_bug.png");

        _resultTween?.Kill();
        _resultTween = UiTween.Number(0.97, 1.0, 0.22, v => _resultTitle.FontSize = 14.5 * v, "back", EaseMode.Out);

        if (kind != ResultKind.Ok) _linkBox.Visibility = Visibility.Collapsed;
    }

    private static FrameworkElement Spacer(double w) => new Border { Width = w, Opacity = 0 };

    private static System.Windows.Media.Imaging.BitmapImage? LoadAsset(string name)
    {
        try
        {
            return new System.Windows.Media.Imaging.BitmapImage(
                new Uri($"pack://application:,,,/Assets/{name}"));
        }
        catch { return null; }
    }

    // ---------------------------------------------------------------- input

    private UIElement BuildDropzone()
    {
        _dropText.Text = "DROP .LUA / .LUAU HERE";
        _dropText.FontSize = 12;
        _dropText.FontWeight = FontWeights.SemiBold;
        _dropText.Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0));
        _dropText.HorizontalAlignment = HorizontalAlignment.Center;

        var micro = new TextBlock
        {
            Text = "OR BROWSE · AUTO-DETECT RUNS ON DROP",
            FontSize = 9.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x70)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
        };

        var content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        content.Children.Add(_dropText);
        content.Children.Add(micro);

        _dropzoneRect.Stroke = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x3A));
        _dropzoneRect.StrokeThickness = 1.4;
        _dropzoneRect.StrokeDashArray = new DoubleCollection { 6, 5 };
        _dropzoneRect.RadiusX = 12;
        _dropzoneRect.RadiusY = 12;
        _dropzoneRect.Fill = new SolidColorBrush(Color.FromArgb(0x50, 0x1D, 0x14, 0x28));

        var zone = new Grid { Height = 92, Cursor = Cursors.Hand };
        zone.Children.Add(_dropzoneRect);
        zone.Children.Add(content);
        zone.AllowDrop = true;
        zone.Drop += OnDrop;
        zone.DragOver += OnDragOver;
        zone.MouseLeftButtonUp += (_, _) => Browse();
        zone.MouseEnter += (_, _) => ZoneGlow(Color.FromRgb(0x2C, 0x2C, 0x3A), Color.FromRgb(0x7C, 0x3A, 0xED));
        zone.MouseLeave += (_, _) => ZoneGlow(Color.FromRgb(0x7C, 0x3A, 0xED), Color.FromRgb(0x2C, 0x2C, 0x3A));
        return zone;
    }

    private void ZoneGlow(Color from, Color to)
    {
        _zoneTween?.Kill();
        var f = _dropText.Foreground is SolidColorBrush sc ? sc.Color : Color.FromRgb(0xA0, 0xA0, 0xB0);
        var tTo = to.R > 0x40 ? Color.FromRgb(0xF2, 0xF2, 0xF7) : Color.FromRgb(0xA0, 0xA0, 0xB0);
        _zoneTween = TweenManager.Fire(0.18, Easing.Resolve("cubic", EaseMode.Out), p =>
        {
            _dropzoneRect.Stroke = new SolidColorBrush(Blend(from, to, p));
            _dropText.Foreground = new SolidColorBrush(Blend(f, tTo, p));
        });
    }

    private static Color Blend(Color a, Color b, double p) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * p),
        (byte)Math.Round(a.G + (b.G - a.G) * p),
        (byte)Math.Round(a.B + (b.B - a.B) * p));

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        SetInput(files[0]);
        e.Handled = true;
    }

    private void Browse()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open obfuscated Lua/Luau script",
            Filter = "Lua scripts (*.lua;*.luau)|*.lua;*.luau|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true) SetInput(dlg.FileName);
    }

    public void SetInput(string path)
    {
        _inputBox.Text = path;
        SuggestOutput();
        var name = System.IO.Path.GetFileName(path);
        _statInput.SetValueText(name.Length > 22 ? name[..22] + "…" : name);
        Say($"Got it — {name}. Checking what made it…");
        _mascot.SetMood(Mascot.Mood.Busy);
        _ = Task.Delay(1400).ContinueWith(_ => Dispatcher.Invoke(() =>
            _mascot.SetMood(Mascot.Mood.Idle)));
    }

    private void SuggestOutput()
    {
        if (string.IsNullOrWhiteSpace(_inputBox.Text)) return;
        try
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(_inputBox.Text);
            _outputBox.Text = System.IO.Path.Combine(Paths.EnsureOutputs(), name + ".deobf.lua");
        }
        catch { /* invalid path — leave as is */ }
    }

    private void UpdateDetectChip()
    {
        try
        {
            if (!File.Exists(_inputBox.Text))
            {
                _detectChip.Text = "";
                return;
            }
            var script = File.ReadAllText(_inputBox.Text);
            var ranked = EngineRegistry.Rank(script);
            var family = ranked[0].Family;
            _detectChip.Text = $"✦ {family.ToUpperInvariant()} MARKERS · ORDER: {string.Join(" → ", ranked.Take(3).Select(e => e.DisplayName.ToUpperInvariant()))}";
            _detectChip.FontFamily = PageKit.Mono;
            _detectChip.FontSize = 10;
            _detectChip.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC));
            _detectChip.Margin = new Thickness(2, 10, 0, 0);
            _statDetected.SetValueText(family, new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC)));
        }
        catch
        {
            _detectChip.Text = "";
        }
    }

    // ---------------------------------------------------------------- runtimes card

    private async Task RefreshRuntimeStatAsync()
    {
        var scan = await EnvironmentScanner.ScanAsync().ConfigureAwait(true);
        int ready = scan.Count(t => t.Found);
        _statRuntimes.SetValueText($"{ready}/{scan.Count}",
            ready == scan.Count
                ? new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80))
                : new SolidColorBrush(Color.FromRgb(0xF5, 0xB8, 0x41)));
        RebuildRuntimeRows(scan);
        _window.UpdateRuntimeChips();
    }

    private void RebuildRuntimeRows(IReadOnlyList<EnvironmentScanner.ToolInfo> scan)
    {
        _runtimeRows.Children.Clear();
        foreach (var t in scan)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(t.Found ? Color.FromRgb(0x4A, 0xDE, 0x80) : Color.FromRgb(0xF5, 0xB8, 0x41)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            dot.SetValue(Grid.ColumnProperty, 0);
            row.Children.Add(dot);

            var name = new TextBlock
            {
                Text = t.Found ? $"{t.Name}  ·  {t.Version ?? "installed"}" : t.Name,
                FontSize = 11,
                Foreground = new SolidColorBrush(t.Found ? Color.FromRgb(0xC9, 0xC9, 0xD6) : Color.FromRgb(0xF5, 0xB8, 0x41)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = t.Hint,
            };
            name.SetValue(Grid.ColumnProperty, 1);
            row.Children.Add(name);

            if (!t.Found && !string.IsNullOrEmpty(t.Url))
            {
                var get = PageKit.Ghost("GET", () => OpenUrl(t.Url!), 0);
                get.Padding = new Thickness(10, 3, 10, 3);
                get.FontSize = 10;
                get.SetValue(Grid.ColumnProperty, 2);
                ToolTipService.SetToolTip(get, $"opens {t.Url}");
                row.Children.Add(get);
            }
            else
            {
                var okChip = PageKit.Chip(t.Found ? "OK" : "BUNDLED",
                    Color.FromRgb(0x8A, 0x8A, 0x98));
                okChip.SetValue(Grid.ColumnProperty, 2);
                row.Children.Add(okChip);
            }

            _runtimeRows.Children.Add(row);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { /* browser launch blocked — user copies the link manually */ }
    }

    // ---------------------------------------------------------------- assistant

    private void Say(string text)
    {
        _bubble.Text = text;
        UiTween.Fade(_bubble, 1.0, 0.2);
    }

    // ---------------------------------------------------------------- output actions

    private string CurrentOutput => _outputBox.Text;

    private void RevealOutput()
    {
        try
        {
            var path = CurrentOutput;
            var dir = File.Exists(path)
                ? System.IO.Path.GetDirectoryName(path) ?? Paths.EnsureOutputs()
                : Paths.EnsureOutputs();
            if (File.Exists(path) && OperatingSystem.IsWindows())
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else
            {
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
        }
        catch (Exception ex) { }
    }

    private void SaveOutputAs()
    {
        try
        {
            if (!File.Exists(CurrentOutput))
            {
                SetResult("Nothing to save yet", "run a deobfuscation first — the result lands in <exe>\\outputs", ResultKind.Fail);
                return;
            }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save deobfuscated script",
                Filter = "Lua scripts (*.lua;*.luau)|*.lua;*.luau|All files (*.*)|*.*",
                InitialDirectory = System.IO.Path.GetDirectoryName(CurrentOutput),
                FileName = System.IO.Path.GetFileName(CurrentOutput),
            };
            if (dlg.ShowDialog() == true)
            {
                File.Copy(CurrentOutput, dlg.FileName, overwrite: true);
                Say("Saved where you wanted it. Anything else?");
            }
        }
        catch (Exception ex) {  }
    }

    private void CopyOutputPath()
    {
        try
        {
            Clipboard.SetText(CurrentOutput);
        }
        catch (Exception ex)
        { 
        }
    }

    private void CopyLink()
    {
        try
        {
            Clipboard.SetText(_linkBox.Text);
            Say("Link copied — share it anywhere.");
        }
        catch (Exception ex) {  }
    }

    private async void GetLink()
    {
        if (_sharing) return;
        if (!File.Exists(CurrentOutput))
        {
            SetResult("Nothing to share yet", "run a deobfuscation first, then GET LINK uploads the result to rubis.app", ResultKind.Fail);
            return;
        }

        _sharing = true;
        _linkButton.IsEnabled = false;
        _linkButton.Content = "UPLOADING…";
        Say("Pasting it to rubis.app — one sec…");

        try
        {
            var (ok, linkOrError) = await ShareUploader.UploadAsync(CurrentOutput).ConfigureAwait(true);
            if (ok)
            {
                _linkBox.Text = linkOrError;
                _linkBox.Visibility = Visibility.Visible;
                try { Clipboard.SetText(linkOrError); } catch { /* clipboard can be locked */ }
                Say("Link is on your clipboard. Paste it anywhere — it opens a read-only view.");
            }
            else
            {

                SetResult("Share failed", linkOrError + " — the file itself is still in <exe>\\outputs", ResultKind.Fail);
                Say("The upload refused. File is safe in outputs\\ — SAVE TO… or REVEAL it instead.");
            }
        }
        catch (OperationCanceledException)
        {

        }
        finally
        {
            _sharing = false;
            _linkButton.IsEnabled = true;
            _linkButton.Content = "GET LINK";
        }
    }

    // ---------------------------------------------------------------- run logic

    private async void Run()
    {
        if (_running) return;
        if (!File.Exists(_inputBox.Text))
        {
            SetResult("No input yet", "drop a script on the dashed zone or press BROWSE", ResultKind.Fail);
            Say("No input yet! Drop a script on the dashed zone first.");
            _mascot.SetMood(Mascot.Mood.Sad);
            return;
        }
        if (string.IsNullOrWhiteSpace(_outputBox.Text))
        {
            SuggestOutput();
        }

        _running = true;
        _cts = new CancellationTokenSource();
        SetBusy(true);
        _mascot.SetMood(Mascot.Mood.Busy);
        Say("Engines spinning — no log spam, just the result. I'll hop when it's done.");
        SetResult("Working…", $"{EngineChoices[Math.Max(0, _engineDropdown.SelectedIndex)].Label}", ResultKind.Busy);
        _lastNote = null;

        var choice = EngineChoices[Math.Max(0, _engineDropdown.SelectedIndex)];
        var options = new DeobfOptions(
            Devirtualize: _devirt.IsOn,
            PostClean: _postClean.IsOn,
            ReadableNames: _names.IsOn,
            AutoDetect: choice.Id is null,
            ForcedEngineId: choice.Id);

        var outputPath = _outputBox.Text;
        try
        {
            var outDir = System.IO.Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
        }
        catch (Exception ex)
        {
            SetResult("Cannot create output folder", ex.Message, ResultKind.Fail);
            SetBusy(false);
            _running = false;
            return;
        }

        void Note(LogKind kind, string line)
        {
            // no console anymore: keep only the last meaningful line for the result detail
            if (kind is LogKind.Error or LogKind.Warn)
                _lastNote = line.TrimStart('─', ' ');
        }

        try
        {
            var report = await EngineRunner.RunAsync(_inputBox.Text, outputPath, options,
                (kind, line) => Dispatcher.Invoke(() => Note(kind, line)), _cts.Token).ConfigureAwait(true);

            if (report.Success)
            {
                _statLast.SetValueText($"{report.Elapsed.TotalSeconds:0.0}s");
                _mascot.SetMood(Mascot.Mood.Happy);
                SetResult("Deobfuscated ✓",
                    $"{report.EngineUsed} · {report.Elapsed.TotalSeconds:0.0}s · {report.OutputPath}",
                    ResultKind.Ok);
                Say("Clean code! It's saved next to the exe in outputs\\ — REVEAL it, or GET LINK to share.");
            }
            else
            {
                _mascot.SetMood(Mascot.Mood.Sad);
                SetResult("Failed",
                    (_lastNote is null ? "" : _lastNote + " — ")
                    + "try a specific engine from the dropdown (CaoMod covers Luraph v15 / v14.7 / v14.8 / v14.9)",
                    ResultKind.Fail);
                Say("That one fought back. Pick the exact engine from the dropdown and run it again.");
            }
        }
        catch (OperationCanceledException)
        {
            SetResult("Cancelled", "stopped by user — partial files are cleaned up automatically", ResultKind.Idle);
            Say("Stopped. No rush — I'll be here when you're ready.");
            _mascot.SetMood(Mascot.Mood.Idle);
        }
        catch (Exception ex)
        {
            SetResult("Unexpected failure", ex.Message, ResultKind.Fail);
            _mascot.SetMood(Mascot.Mood.Sad);
            Say("Something broke on the way — the detail line has the reason.");
        }
        finally
        {
            SetBusy(false);
            _running = false;
        }
    }

    private void SetBusy(bool busy)
    {
        _runButton.IsEnabled = !busy;
        _runButton.Content = busy ? "RUNNING…" : "RUN DEOBFUSCATION";
        UiTween.Fade(_runButton, busy ? 0.6 : 1.0, 0.18);

        if (busy)
        {
            _progressTrack.Opacity = 1;
            StartShimmer();
        }
        else
        {
            _progressTween?.Kill();
            UiTween.Fade(_progressTrack, 0, 0.3);
        }
    }

    private void StartShimmer()
    {
        var fillMove = _progressFill.RenderTransform as TranslateTransform;
        double w = double.IsNaN(ActualWidth) ? 500 : ActualWidth;
        _progressTween = TweenManager.Fire(1.3, Easing.Resolve("sine", EaseMode.InOut), p =>
        {
            fillMove!.X = -110 + (w + 110) * p;
        }, _ => { if (_running) StartShimmer(); });
    }
}
