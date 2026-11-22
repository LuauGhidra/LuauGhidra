using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LuauGhirda.Core;
using LuauGhirda.UI;

namespace LuauGhirda.Pages;

/// <summary>A page that wants its top-level children revealed with a stagger.</summary>
public interface IStaggeredPage
{
    IEnumerable<UIElement> StaggerChildren();
}

/// <summary>
/// Dashboard building blocks: rounded cards with icon-tile headers, stat cards,
/// purple primary buttons, dark ghost buttons. One violet accent everywhere.
/// </summary>
public static class PageKit
{
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");

    private static readonly Color ColCard = Color.FromRgb(0x15, 0x15, 0x1D);
    private static readonly Color ColCardBorder = Color.FromRgb(0x22, 0x22, 0x2C);
    private static readonly Color ColCardBorderHi = Color.FromRgb(0x2E, 0x2E, 0x3C);
    private static readonly Color ColTint = Color.FromRgb(0x1D, 0x14, 0x28);
    private static readonly Color ColAccent = Color.FromRgb(0xA8, 0x55, 0xF7);
    private static readonly Color ColAccentHi = Color.FromRgb(0xC0, 0x84, 0xFC);
    private static readonly Color ColAccentDeep = Color.FromRgb(0x7C, 0x3A, 0xED);

    public static StackPanel Root() => new();

    // ---------------------------------------------------------------- card

    /// <summary>
    /// Rounded dashboard card with an icon-tile header row.
    /// body is the padded content StackPanel — fill it with rows.
    /// </summary>
    public static Border Card(string title, string iconKey, out StackPanel body,
        UIElement? headerRight = null, double bodyPadding = 14, double marginBottom = 14)
    {
        body = new StackPanel();

        var head = new Grid { Margin = new Thickness(0) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var tile = IconTile(iconKey, 30);
        tile.SetValue(Grid.ColumnProperty, 0);
        head.Children.Add(tile);

        var t = new TextBlock
        {
            Text = title,
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
        };
        t.SetValue(Grid.ColumnProperty, 1);
        head.Children.Add(t);

        if (headerRight is not null)
        {
            headerRight.SetValue(Grid.ColumnProperty, 2);
            head.Children.Add(headerRight);
        }

        var inner = new Grid();
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        head.SetValue(Grid.RowProperty, 0);
        head.Margin = new Thickness(14, 12, 14, 10);
        var sep = new Border { Height = 1, Background = new SolidColorBrush(ColCardBorder), Margin = new Thickness(14, 0, 14, 0) };
        sep.SetValue(Grid.RowProperty, 1);
        body.Margin = new Thickness(14, 12, 14, bodyPadding);
        inner.Children.Add(head);
        inner.Children.Add(sep);
        inner.Children.Add(body);
        body.SetValue(Grid.RowProperty, 2);

        var card = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(ColCard),
            BorderBrush = new SolidColorBrush(ColCardBorder),
            BorderThickness = new Thickness(1),
            Child = inner,
            Margin = new Thickness(0, 0, 0, marginBottom),
        };
        return card;
    }

    /// <summary>Purple-tinted rounded tile with a lucide icon.</summary>
    public static Border IconTile(string iconKey, double size = 30)
    {
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.28),
            Background = new SolidColorBrush(ColTint),
            Child = new Icon(iconKey, size * 0.55, new SolidColorBrush(ColAccentHi)),
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    // ---------------------------------------------------------------- stat card

    /// <summary>Reference-style stat card: icon tile + big value + small label.</summary>
    public sealed class StatCard : Border
    {
        public TextBlock Value { get; }
        private readonly TextBlock _label;

        public StatCard(string iconKey, string value, string label)
        {
            Value = new TextBlock
            {
                Text = value,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            _label = new TextBlock
            {
                Text = label,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x70)),
                Margin = new Thickness(0, 2, 0, 0),
            };

            var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(Value);
            right.Children.Add(_label);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var tile = IconTile(iconKey, 34);
            tile.SetValue(Grid.ColumnProperty, 0);
            right.SetValue(Grid.ColumnProperty, 1);
            grid.Children.Add(tile);
            grid.Children.Add(right);

            Child = grid;
            CornerRadius = new CornerRadius(14);
            Background = new SolidColorBrush(ColCard);
            BorderBrush = new SolidColorBrush(ColCardBorder);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(12, 10, 10, 10);
        }

        public void SetValueText(string text, Brush? brush = null)
        {
            Value.Text = text;
            Value.Foreground = brush ?? new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7));
            UiTween.Number(0.94, 1.0, 0.25, v => Value.FontSize = 15 * v, "back", EaseMode.Out);
        }

        public void SetLabel(string text) => _label.Text = text;
    }

    // ---------------------------------------------------------------- buttons

    /// <summary>Flat template shared by code-built buttons (purple or ghost).</summary>
    public static ControlTemplate PurpleTemplate(double rounded = 10)
    {
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.Name = "bd";
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(rounded));
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Button.PaddingProperty));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(presenter);
        return new ControlTemplate(typeof(Button)) { VisualTree = factory };
    }

    public static Button Ghost(string text, Action onClick, double minWidth = 0)
    {
        var btn = new Button
        {
            Content = text,
            Style = Application.Current.FindResource("GhostButton") as Style,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
        };
        if (minWidth > 0) btn.MinWidth = minWidth;
        btn.Click += (_, _) => onClick();
        UiTween.PressPulse(btn, 0.97);
        return btn;
    }

    /// <summary>Purple gradient action button, white label.</summary>
    public static Button Primary(string text, Action onClick, double minWidth = 150)
    {
        var btn = new Button
        {
            Content = text,
            Foreground = Brushes.White,
            Background = (Brush)Application.Current.FindResource("Brush.AccentGrad"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(20, 10, 20, 10),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = minWidth,
            Template = PurpleTemplate(10),
        };

        btn.Click += (_, _) => onClick();
        btn.MouseEnter += (_, _) => TweenManager.Fire(0.16, Easing.Resolve("cubic", EaseMode.Out), p =>
            btn.Opacity = 1 - 0.12 * p);
        btn.MouseLeave += (_, _) => TweenManager.Fire(0.2, Easing.Resolve("cubic", EaseMode.Out), p =>
            btn.Opacity = 0.12 + 0.88 * p);
        UiTween.PressPulse(btn, 0.97);
        return btn;
    }

    // ---------------------------------------------------------------- text bits

    public static TextBlock Chip(string text, Color color, Color? bg = null) => new()
    {
        Text = text,
        FontFamily = Mono,
        FontSize = 9.5,
        FontWeight = FontWeights.Medium,
        Foreground = new SolidColorBrush(color),
        Background = bg is null ? new SolidColorBrush(Color.FromArgb(0x00, 0, 0, 0)) : new SolidColorBrush(bg.Value),
        Padding = new Thickness(7, 3, 7, 3),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    public static TextBlock Body(string text) => new()
    {
        Text = text,
        FontSize = 12.5,
        LineHeight = 19.5,
        TextWrapping = TextWrapping.Wrap,
        Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0xB8, 0xC4)),
        Margin = new Thickness(0, 0, 0, 10),
    };

    public static TextBlock Micro(string text) => new()
    {
        Text = text,
        FontFamily = Mono,
        FontSize = 9.5,
        Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x70)),
    };

    public static TextBlock FieldLabel(string text) => new()
    {
        Text = text,
        FontFamily = Mono,
        FontSize = 9.5,
        Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x70)),
        Margin = new Thickness(2, 0, 8, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };
}
