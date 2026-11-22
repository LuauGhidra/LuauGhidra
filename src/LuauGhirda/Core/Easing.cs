namespace LuauGhirda.Core;

/// <summary>Which end of the curve the ease is applied to.</summary>
public enum EaseMode
{
    In,
    Out,
    InOut
}

/// <summary>
/// Full Robert-Penner style easing set, evaluated as pure functions of t in [0,1].
/// All curves are hand-checked so that In(0)=0, Out(1)=1 and every InOut is C1-continuous.
/// </summary>
public static class Easing
{
    public delegate double Func(double t);

    public static Func Linear => t => t;

    public static Func Sine(EaseMode m) => m switch
    {
        EaseMode.In => t => 1.0 - Math.Cos(t * Math.PI / 2.0),
        EaseMode.Out => t => Math.Sin(t * Math.PI / 2.0),
        _ => t => -(Math.Cos(Math.PI * t) - 1.0) / 2.0,
    };

    public static Func Quad(EaseMode m) => m switch
    {
        EaseMode.In => t => t * t,
        EaseMode.Out => t => 1.0 - (1.0 - t) * (1.0 - t),
        _ => t => t < 0.5 ? 2.0 * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 2) / 2.0,
    };

    public static Func Cubic(EaseMode m) => m switch
    {
        EaseMode.In => t => t * t * t,
        EaseMode.Out => t => 1.0 - Math.Pow(1.0 - t, 3),
        _ => t => t < 0.5 ? 4.0 * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 3) / 2.0,
    };

    public static Func Quart(EaseMode m) => m switch
    {
        EaseMode.In => t => t * t * t * t,
        EaseMode.Out => t => 1.0 - Math.Pow(1.0 - t, 4),
        _ => t => t < 0.5 ? 8.0 * t * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 4) / 2.0,
    };

    public static Func Quint(EaseMode m) => m switch
    {
        EaseMode.In => t => t * t * t * t * t,
        EaseMode.Out => t => 1.0 - Math.Pow(1.0 - t, 5),
        _ => t => t < 0.5 ? 16.0 * t * t * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 5) / 2.0,
    };

    public static Func Expo(EaseMode m) => m switch
    {
        EaseMode.In => t => t <= 0 ? 0 : Math.Pow(2, 10.0 * t - 10.0),
        EaseMode.Out => t => t >= 1 ? 1 : 1.0 - Math.Pow(2, -10.0 * t),
        _ => t => t switch
        {
            0.0 => 0.0,
            1.0 => 1.0,
            < 0.5 => Math.Pow(2, 20.0 * t - 10.0) / 2.0,
            _ => (2.0 - Math.Pow(2, -20.0 * t + 10.0)) / 2.0,
        },
    };

    public static Func Circ(EaseMode m) => m switch
    {
        EaseMode.In => t => 1.0 - Math.Sqrt(1.0 - t * t),
        EaseMode.Out => t => Math.Sqrt(1.0 - Math.Pow(t - 1.0, 2)),
        _ => t => t < 0.5
            ? (1.0 - Math.Sqrt(1.0 - 4.0 * t * t)) / 2.0
            : (Math.Sqrt(-4.0 * t * t + 8.0 * t - 3.0) + 1.0) / 2.0,
    };

    // Overshoot constants.
    private const double C1 = 1.70158;
    private const double C3 = C1 + 1.0;
    private const double C2 = 2.5949095;

    public static Func Back(EaseMode m) => m switch
    {
        EaseMode.In => t => C3 * t * t * t - C1 * t * t,
        EaseMode.Out => t => 1.0 + C3 * Math.Pow(t - 1.0, 3) + C1 * Math.Pow(t - 1.0, 2),
        _ => t => t < 0.5
            ? Math.Pow(2.0 * t, 2) * ((C2 + 1.0) * 2.0 * t - C2) / 2.0
            : (Math.Pow(2.0 * t - 2.0, 2) * ((C2 + 1.0) * (t * 2.0 - 2.0) + C2) + 2.0) / 2.0,
    };

    private const double ElasticC4 = 2.0 * Math.PI / 3.0;

    public static Func Elastic(EaseMode m) => m switch
    {
        EaseMode.In => t => t switch
        {
            0.0 => 0.0,
            1.0 => 1.0,
            _ => -Math.Pow(2, 10.0 * t - 10.0) * Math.Sin((t * 10.0 - 10.75) * ElasticC4),
        },
        EaseMode.Out => t => t switch
        {
            0.0 => 0.0,
            1.0 => 1.0,
            _ => Math.Pow(2, -10.0 * t) * Math.Sin((t * 10.0 - 0.75) * ElasticC4) + 1.0,
        },
        _ => t => t switch
        {
            0.0 => 0.0,
            1.0 => 1.0,
            < 0.5 => -(Math.Pow(2, 20.0 * t - 10.0) * Math.Sin((20.0 * t - 11.125) * ElasticC4)) / 2.0,
            _ => (Math.Pow(2, -20.0 * t + 10.0) * Math.Sin((20.0 * t - 11.125) * ElasticC4)) / 2.0 + 1.0,
        },
    };

    private static double BounceOutCore(double t) => t switch
    {
        < 1.0 / 2.75 => 7.5625 * t * t,
        < 2.0 / 2.75 => 7.5625 * (t -= 1.5 / 2.75) * t + 0.75,
        < 2.5 / 2.75 => 7.5625 * (t -= 2.25 / 2.75) * t + 0.9375,
        _ => 7.5625 * (t -= 2.625 / 2.75) * t + 0.984375,
    };

    public static Func Bounce(EaseMode m) => m switch
    {
        EaseMode.In => t => 1.0 - BounceOutCore(1.0 - t),
        EaseMode.Out => BounceOutCore,
        _ => t => t < 0.5
            ? (1.0 - BounceOutCore(1.0 - 2.0 * t)) / 2.0
            : (1.0 + BounceOutCore(2.0 * t - 1.0)) / 2.0,
    };

    /// <summary>Resolve a named curve + mode into a callable. Unknown names fall back to Cubic.Out.</summary>
    public static Func Resolve(string name, EaseMode mode)
    {
        return (name?.Trim().ToLowerInvariant()) switch
        {
            "linear" => Linear,
            "sine" => Sine(mode),
            "quad" => Quad(mode),
            "cubic" => Cubic(mode),
            "quart" => Quart(mode),
            "quint" => Quint(mode),
            "expo" => Expo(mode),
            "circ" => Circ(mode),
            "back" => Back(mode),
            "elastic" => Elastic(mode),
            "bounce" => Bounce(mode),
            _ => Cubic(EaseMode.Out),
        };
    }
}
