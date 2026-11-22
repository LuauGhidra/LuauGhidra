using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LuauGhirda.UI;

/// <summary>
/// The snake + Lua moon artwork. Two sources: the full artwork (logo.png, used
/// large on the About page) and the flat brand mark (mark.png, crisp at rail
/// size — no soft glow or speckles, so it never reads as glitched).
/// </summary>
public sealed class LogoImage : Border
{
    private static ImageBrush? _brush;
    private static ImageBrush? _markBrush;

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(LogoImage),
        new FrameworkPropertyMetadata(34.0, FrameworkPropertyMetadataOptions.AffectsRender, OnChanged));

    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(double), typeof(LogoImage),
        new FrameworkPropertyMetadata(-1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnChanged));

    public static readonly DependencyProperty MarkProperty = DependencyProperty.Register(
        nameof(Mark), typeof(bool), typeof(LogoImage),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnChanged));

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double Radius
    {
        get => (double)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <summary>Use the flat mark.png instead of the full artwork.</summary>
    public bool Mark
    {
        get => (bool)GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    public LogoImage()
    {
        IsHitTestVisible = false;
        // XAML may set Size equal to the DP default (no change callback fires),
        // so make sure the brush is applied on load regardless.
        Loaded += (_, _) => Rebuild();
    }

    public LogoImage(double size, double radius = -1) : this()
    {
        Size = size;
        Radius = radius;
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LogoImage logo) logo.Rebuild();
    }

    private static ImageBrush Artwork()
    {
        _brush ??= new ImageBrush
        {
            ImageSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/logo.png")),
            Stretch = Stretch.UniformToFill,
        };
        return _brush;
    }

    private static ImageBrush MarkBrush()
    {
        _markBrush ??= new ImageBrush
        {
            ImageSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/mark.png")),
            Stretch = Stretch.UniformToFill,
        };
        return _markBrush;
    }

    private void Rebuild()
    {
        double size = Size;
        double radius = Radius < 0 ? size * 0.22 : Radius;
        Width = Height = size;
        CornerRadius = new CornerRadius(radius);
        Background = Mark ? MarkBrush() : Artwork();
    }
}
