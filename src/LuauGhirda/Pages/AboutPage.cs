using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LuauGhirda.Core;
using LuauGhirda.UI;

namespace LuauGhirda.Pages;

public partial class AboutPage : Grid, IStaggeredPage
{
    private readonly Mascot _mascot;
    private readonly TextBlock _bubble = new();

    public AboutPage()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

        var main = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        main.SetValue(Grid.ColumnProperty, 0);
        var side = new StackPanel();
        side.SetValue(Grid.ColumnProperty, 1);

        // ---- identity card ---------------------------------------------------
        var identity = PageKit.Card("LuauGhirda", "info", out var idBody);
        var row = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var logoHost = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
            Children = { new LogoImage(88, 20) },
        };
        logoHost.SetValue(Grid.ColumnProperty, 0);
        row.Children.Add(logoHost);

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(new TextBlock
        {
            Text = "LuauGhirda V1.0.0",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
        });
        right.Children.Add(new TextBlock
        {
            Text = "LUAU DEOBFUSCATION WORKBENCH",
            FontFamily = PageKit.Mono,
            FontSize = 9.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC)),
            Margin = new Thickness(0, 4, 0, 0),
        });
        right.Children.Add(new TextBlock
        {
            Text = "the snake reads what the moon hides",
            FontFamily = PageKit.Mono,
            FontSize = 9.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x70)),
            Margin = new Thickness(0, 3, 0, 0),
        });
        right.SetValue(Grid.ColumnProperty, 1);
        row.Children.Add(right);
        idBody.Children.Add(row);
        main.Children.Add(identity);

        // ---- what this is ----------------------------------------------------
        var what = PageKit.Card("What this is", "file-code", out var whatBody);
        whatBody.Children.Add(PageKit.Body(
                    "LuauGhirda is a unified GUI for community Luau deobfuscators. Drop in a script to " +
                    "auto-detect its obfuscator, run it through the right engine, and grab the cleaned output " +
                    "from the outputs folder next to the exe—or push it straight to rubis.app for a share link. " +
                    "Every dumped file gets stamped with the LuauGhirda ASCII banner."));
        whatBody.Children.Add(PageKit.Body(
            "Each engine runs as an isolated child process using its original entry point, so nothing is " +
            "altered under the hood. Compiled Luau bytecode routes to the lua.expert cloud decompiler with " +
            "zero local setup, while CaoMod handles Luraph v14.7–v15. You can also pick any engine manually " +
            "from the dropdown."));
        whatBody.Children.Add(PageKit.Body(
            "If Node.js or Python 3 is missing on your system, the Runtimes card turns amber with a GET " +
            "button pointing straight to the official installers."));
        main.Children.Add(what);

        // ---- illustration ------------------------------------------------------
        var made = PageKit.Card("Made for readability", "sparkles", out var madeBody);
        var madeArt = new Image
        {
            Source = LoadIllust("illust_dev.png"),
            MaxHeight = 190,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 2, 0, 0),
        };
        madeBody.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Child = madeArt,
        });
        main.Children.Add(made);

        // ---- engines & credits ----------------------------------------------
        var credits = PageKit.Card("Engines & credits", "package", out var credBody);
        foreach (var e in EngineRegistry.All)
        {
            var r = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new TextBlock
            {
                Text = e.DisplayName,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xEE)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            name.SetValue(Grid.ColumnProperty, 0);
            r.Children.Add(name);
            var credit = new TextBlock
            {
                Text = "by " + e.Credit,
                FontFamily = PageKit.Mono,
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x70)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            credit.SetValue(Grid.ColumnProperty, 1);
            r.Children.Add(credit);
            credBody.Children.Add(r);
        }
        credBody.Children.Add(new TextBlock
        {
            Text = "",
            FontSize = 10.5,
            FontFamily = PageKit.Mono,
            Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x70)),
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
        main.Children.Add(credits);

        // ---- assistant --------------------------------------------------------
        var assistantCard = PageKit.Card("Assistant", "sparkles", out var asBody);
        _bubble.Text = "I'm Ghirda — same snake as the logo, just with more personality. Drop a script in the Deobfuscator and let the engines unwrap it.";
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

        // ---- purpose ----------------------------------------------------------
        var purpose = PageKit.Card("Purpose", "shield-check", out var pBody);
        pBody.Children.Add(new TextBlock
        {
            Text = "This is a defensive analysis tool: use it on scripts you own or are allowed to review, to reveal " +
                   "hidden behavior before running anything.",
            FontSize = 11,
            LineHeight = 16.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x98)),
        });
        side.Children.Add(purpose);

        Children.Add(main);
        Children.Add(side);
    }

    public IEnumerable<UIElement> StaggerChildren() => new UIElement[] { Children[0], Children[1] };

    private static System.Windows.Media.Imaging.BitmapImage? LoadIllust(string name)
    {
        try
        {
            return new System.Windows.Media.Imaging.BitmapImage(
                new Uri($"pack://application:,,,/Assets/{name}"));
        }
        catch { return null; }
    }
}
