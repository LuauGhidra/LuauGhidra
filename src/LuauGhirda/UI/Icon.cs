using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LuauGhirda.UI;

/// <summary>
/// Lucide line icon rendered from generated stroke geometry (IconLibrary.Data).
/// Scales to any square size via the Viewbox.
/// </summary>
public sealed class Icon : Viewbox
{
    public static readonly DependencyProperty IconNameProperty = DependencyProperty.Register(
        nameof(IconName), typeof(string), typeof(Icon),
        new FrameworkPropertyMetadata("info", FrameworkPropertyMetadataOptions.AffectsRender, OnChanged));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Icon),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)),
            FrameworkPropertyMetadataOptions.AffectsRender, OnChanged));

    public string IconName
    {
        get => (string)GetValue(IconNameProperty);
        set => SetValue(IconNameProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Icon()
    {
        Child = Build();
        IsHitTestVisible = false;
    }

    public Icon(string name, double size, Brush? stroke = null) : this()
    {
        IconName = name;
        Width = Height = size;
        if (stroke is not null) Stroke = stroke;
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Icon icon) icon.Child = icon.Build();
    }

    private Path Build()
    {
        var data = IconLibrary.Data.TryGetValue(IconName, out var d) ? d : IconLibrary.Data["info"];
        return new Path
        {
            Data = Geometry.Parse(d),
            Stroke = Stroke,
            StrokeThickness = 1.8,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Stretch = Stretch.None,
            Width = 24,
            Height = 24,
        };
    }
}
