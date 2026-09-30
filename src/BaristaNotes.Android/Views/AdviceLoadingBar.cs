using Android.Animation;
using Android.Graphics;
using Android.Views;
using Android.Views.Animations;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class AdviceLoadingBar : View
{
    private readonly NativeStyle _style;
    private readonly Paint _paint = new();
    private readonly LinearGradient _gradient;
    private readonly LinearInterpolator _linear = new();
    private ValueAnimator? _animator;
    private float _position;
    private bool _disposed;

    public AdviceLoadingBar(NativeStyle style) : base(style.Context)
    {
        _style = style;
        _gradient = new LinearGradient(0, 0, style.Dp(120), 0,
            [Color.Transparent.ToArgb(), style.Primary.ToArgb(), Color.Transparent.ToArgb()],
            [0f, .5f, 1f], Shader.TileMode.Clamp!);
        _paint.SetShader(_gradient);
        ImportantForAccessibility = ImportantForAccessibility.No;
        Clickable = Focusable = false;
        TranslationY = -style.Dp(3);
        Visibility = ViewStates.Gone;
        NativeStyle.Identify(this, "AdviceLoadingBar");
    }

    public void Start()
    {
        if (_disposed || _animator is not null) return;
        var animator = ValueAnimator.OfFloat(-_style.Dp(120), _style.Dp(400))
            ?? throw new InvalidOperationException("The advice loading animator is unavailable.");
        _animator = animator;
        _position = -_style.Dp(120);
        animator.SetDuration(1000);
        animator.RepeatCount = ValueAnimator.Infinite;
        animator.SetInterpolator(_linear);
        animator.Update += OnUpdate;
        Visibility = ViewStates.Visible;
        animator.Start();
    }

    private void OnUpdate(object? sender, ValueAnimator.AnimatorUpdateEventArgs args)
    {
        if (!_disposed && _animator?.AnimatedValue is Java.Lang.Float value)
        {
            _position = value.FloatValue();
            Invalidate();
        }
    }

    public void Stop()
    {
        var animator = _animator;
        _animator = null;
        if (animator is not null)
        {
            animator.Update -= OnUpdate;
            animator.Cancel();
            animator.Dispose();
        }
        if (!_disposed) Visibility = ViewStates.Gone;
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        canvas.Save();
        canvas.Translate(_position, 0);
        canvas.DrawRect(0, 0, _style.Dp(120), _style.Dp(4), _paint);
        canvas.Restore();
    }

    protected override void OnDetachedFromWindow()
    {
        Stop();
        base.OnDetachedFromWindow();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            Stop();
            _disposed = true;
            _paint.Dispose();
            _gradient.Dispose();
            _linear.Dispose();
        }
        base.Dispose(disposing);
    }
}
