using Android.Animation;
using Android.Views;
using Android.Views.Animations;

namespace BaristaNotes.AndroidApp.Views;

internal static class NativeModalMotion
{
    public static async Task RunAsync(View content, View host, bool entering, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (Android.OS.Looper.MyLooper() != Android.OS.Looper.MainLooper
            || SynchronizationContext.Current is not { } uiContext)
            throw new InvalidOperationException("Native modal animation requires the Android UI context.");
        if (entering)
        {
            content.TranslationY = host.Height;
            await WaitForLayoutAsync(content, cancellation);
        }
        // Version-matched UXDivers Utils.CalculateTranslationAnimationDefaultDistance.
        var distance = (host.Height + content.Height) / 2f;
        var animator = ValueAnimator.OfFloat(entering ? distance : content.TranslationY, entering ? 0 : distance)
            ?? throw new InvalidOperationException("Native modal animator is unavailable.");
        using var easing = new CubicInterpolator(entering);
        using var lifetime = new UiAnimationLifetime(uiContext, cancellation, animator.Cancel, animator.Dispose);
        animator.SetDuration(entering ? 300 : 400);
        animator.SetInterpolator(easing);
        EventHandler<ValueAnimator.AnimatorUpdateEventArgs> update = (_, _) =>
        {
            if (lifetime.IsActive && animator.AnimatedValue is Java.Lang.Float value)
                content.TranslationY = value.FloatValue();
        };
        EventHandler end = (_, _) => lifetime.Complete();
        animator.Update += update;
        animator.AnimationEnd += end;
        try
        {
            lifetime.RegisterCancellation();
            cancellation.ThrowIfCancellationRequested();
            animator.Start();
            await lifetime.Completion;
            cancellation.ThrowIfCancellationRequested();
        }
        finally
        {
            animator.Update -= update;
            animator.AnimationEnd -= end;
        }
    }

    private static async Task WaitForLayoutAsync(View view, CancellationToken cancellation)
    {
        if (view.Height > 0)
            return;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<View.LayoutChangeEventArgs> handler = (_, _) =>
        {
            if (view.Height > 0)
                completed.TrySetResult();
        };
        view.LayoutChange += handler;
        using var registration = cancellation.Register(() => completed.TrySetCanceled(cancellation));
        try { await completed.Task; }
        finally { view.LayoutChange -= handler; }
    }

    private sealed class CubicInterpolator(bool entering) : Java.Lang.Object, IInterpolator
    {
        public float GetInterpolation(float input) => entering
            ? 1 - (1 - input) * (1 - input) * (1 - input)
            : input * input * input;
    }
}
