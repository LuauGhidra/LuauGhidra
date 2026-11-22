using System.Windows.Threading;

namespace LuauGhirda.Core;

/// <summary>
/// A single tween instance. Created through <see cref="TweenManager.Fire"/>; drive it
/// from any thread-safe UI context — callbacks always run on the dispatcher that
/// initialized the manager.
/// </summary>
public sealed class Tween
{
    internal double _delay;
    internal double _elapsed;
    internal bool _alive;

    /// <summary>Total duration in seconds (delay excluded).</summary>
    public double Duration { get; }

    /// <summary>Eased progress in [0,1] most recently pushed to <see cref="OnUpdate"/>.</summary>
    public double Progress { get; internal set; }

    /// <summary>Easing function mapping raw linear t to eased t.</summary>
    public Easing.Func Ease { get; }

    public Action<double>? OnUpdate { get; set; }
    public Action<Tween>? OnComplete { get; set; }

    internal Tween(double duration, Easing.Func ease, double delay)
    {
        Duration = Math.Max(0.0001, duration);
        Ease = ease;
        _delay = Math.Max(0, delay);
        _alive = true;
    }

    public void Kill() => TweenManager.Kill(this);

    /// <summary>Chain a follow-up tween that starts when this one completes.</summary>
    public Tween Then(Tween next)
    {
        next._held = true;
        OnComplete += _ => next._held = false;
        return next;
    }

    internal bool _held;
}

/// <summary>
/// Frame-driven tween engine. Hooks WPF's <see cref="System.Windows.Media.CompositionTarget.Rendering"/>
/// so every tween advances exactly once per rendered frame with real delta time —
/// no DispatcherTimers, no stutters, everything stays in lockstep with composition.
/// </summary>
public static class TweenManager
{
    private static readonly List<Tween> Active = new();
    private static readonly List<Tween> CompletedBuffer = new();
    private static TimeSpan _lastFrame;
    private static bool _hasLastFrame;

    /// <summary>Number of live tweens right now (debug / status bar).</summary>
    public static int LiveCount { get; private set; }

    public static void Initialize()
    {
        System.Windows.Media.CompositionTarget.Rendering -= OnRendering;
        System.Windows.Media.CompositionTarget.Rendering += OnRendering;
    }

    public static Tween Fire(
        double durationSeconds,
        Easing.Func ease,
        Action<double>? onUpdate,
        Action<Tween>? onComplete = null,
        double delaySeconds = 0)
    {
        var t = new Tween(durationSeconds, ease, delaySeconds)
        {
            OnUpdate = onUpdate,
            OnComplete = onComplete,
        };
        Active.Add(t);
        return t;
    }

    public static Tween Fire(
        double durationSeconds,
        string easeName,
        EaseMode mode,
        Action<double>? onUpdate,
        Action<Tween>? onComplete = null,
        double delaySeconds = 0)
        => Fire(durationSeconds, Easing.Resolve(easeName, mode), onUpdate, onComplete, delaySeconds);

    public static void Kill(Tween t)
    {
        if (t is null) return;
        t._alive = false;
    }

    public static void KillAll()
    {
        foreach (var t in Active) t._alive = false;
    }

    private static void OnRendering(object? sender, EventArgs e)
    {
        if (e is not System.Windows.Media.RenderingEventArgs args) return;
        var ct = args.RenderingTime;
        if (!_hasLastFrame)
        {
            _lastFrame = ct;
            _hasLastFrame = true;
            return;
        }
        var dt = (ct - _lastFrame).TotalSeconds;
        _lastFrame = ct;
        if (dt <= 0) return;
        Step(Math.Min(dt, 0.1));
    }

    private static void Step(double dt)
    {
        CompletedBuffer.Clear();
        int live = 0;

        for (int i = 0; i < Active.Count; i++)
        {
            var t = Active[i];
            if (!t._alive) { CompletedBuffer.Add(t); continue; }
            if (t._held) { live++; continue; }

            if (t._delay > 0)
            {
                t._delay -= dt;
                if (t._delay > 0) { live++; continue; }
            }

            t._elapsed += dt;
            var raw = Math.Clamp(t._elapsed / t.Duration, 0.0, 1.0);
            t.Progress = t.Ease(raw);
            try { t.OnUpdate?.Invoke(t.Progress); }
            catch { t._alive = false; }

            if (raw >= 1.0)
            {
                t._alive = false;
                CompletedBuffer.Add(t);
            }
            else live++;
        }

        LiveCount = live;

        if (CompletedBuffer.Count > 0)
        {
            foreach (var done in CompletedBuffer)
            {
                Active.Remove(done);
                try { done.OnComplete?.Invoke(done); }
                catch { /* a dead callback must never break the frame loop */ }
            }
            CompletedBuffer.Clear();
        }
    }
}
