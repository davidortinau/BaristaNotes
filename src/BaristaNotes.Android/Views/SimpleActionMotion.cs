using Android.Animation;
using Android.Views;
using Android.Views.Animations;

namespace BaristaNotes.AndroidApp.Views;

internal static class SimpleActionMotion
{
    public static async Task AppearAsync(View card, View host, CancellationToken cancellation)
    {
        await WaitForLayoutAsync(card, cancellation);
        cancellation.ThrowIfCancellationRequested();
        var distance = (host.Width + card.Width) / 2f;
        card.TranslationX = distance;
        card.Alpha = 1;
        await AnimateAsync(distance, 0, 400, springOut: true, value => card.TranslationX = value, cancellation);
    }

    public static async Task DisappearAsync(View card, View host, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        await AnimateAsync(card.ScaleX, 1.2f, 150, springOut: false,
            value => card.ScaleX = card.ScaleY = value, cancellation);
        await AnimateAsync(1.2f, .5f, 150, springOut: false,
            value => card.ScaleX = card.ScaleY = value, cancellation);
        cancellation.ThrowIfCancellationRequested();
        var distance = (host.Width + card.Width) / 2f;
        await AnimateAsync(card.TranslationX, -distance, 500, springOut: true,
            value => card.TranslationX = value, cancellation);
    }

    private static async Task AnimateAsync(float from, float to, long milliseconds, bool springOut,
        Action<float> updateValue, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (Android.OS.Looper.MyLooper() != Android.OS.Looper.MainLooper
            || SynchronizationContext.Current is not { } uiContext)
            throw new InvalidOperationException("SimpleAction motion requires the Android UI context.");
        var animator = ValueAnimator.OfFloat(from, to)
            ?? throw new InvalidOperationException("The native SimpleAction animator is unavailable.");
        using var easing = new Interpolator(springOut);
        using var lifetime = new UiAnimationLifetime(uiContext, cancellation, animator.Cancel, animator.Dispose);
        animator.SetDuration(milliseconds);
        animator.SetInterpolator(easing);
        EventHandler<ValueAnimator.AnimatorUpdateEventArgs> update = (_, _) =>
        {
            if (lifetime.IsActive && animator.AnimatedValue is Java.Lang.Float value)
                updateValue(value.FloatValue());
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
        cancellation.ThrowIfCancellationRequested();
        if (view.Width > 0 && view.Height > 0)
            return;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<View.LayoutChangeEventArgs> handler = (_, _) =>
        {
            if (view.Width > 0 && view.Height > 0)
                completed.TrySetResult();
        };
        view.LayoutChange += handler;
        using var registration = cancellation.Register(() => completed.TrySetCanceled(cancellation));
        try { await completed.Task; }
        finally { view.LayoutChange -= handler; }
    }

    private sealed class Interpolator(bool springOut) : Java.Lang.Object, IInterpolator
    {
        public float GetInterpolation(float input)
        {
            if (!springOut)
                return input;
            // Pinned MAUI Easing.SpringOut; no filter-bottom-sheet easing.
            const float overshoot = 1.70158f;
            var x = input - 1;
            return x * x * ((overshoot + 1) * x + overshoot) + 1;
        }
    }
}
