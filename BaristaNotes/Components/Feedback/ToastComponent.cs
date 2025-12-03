using MauiReactor;
using BaristaNotes.Models;
using BaristaNotes.Theme;
using System.Reactive.Linq;

namespace BaristaNotes.Components.Feedback;

class ToastComponentState
{
    public bool IsVisible { get; set; }
    public double TranslationY { get; set; } = -50;
}

partial class ToastComponent : Component<ToastComponentState>
{
    [Prop]
    FeedbackMessage? _message;

    [Prop]
    Action<Guid>? _onDismiss;

    protected override void OnMounted()
    {
        if (_message == null) return;

        // Animate in
        AnimateIn();

        // Auto-dismiss after duration
        Task.Delay(_message.DurationMs).ContinueWith(_ =>
        {
            MainThread.BeginInvokeOnMainThread(() => Dismiss());
        });

        base.OnMounted();
    }

    void AnimateIn()
    {
        State.IsVisible = true;
        
        new Animation(v =>
        {
            SetState(s => s.TranslationY = v);
        }, -50, 0, Easing.CubicOut)
        .Commit(ContainerView, "SlideIn", 16, 300);
    }

    void Dismiss()
    {
        new Animation(v =>
        {
            SetState(s => s.TranslationY = v);
        }, 0, -50, Easing.CubicIn)
        .Commit(ContainerView, "SlideOut", 16, 200, finished: (v, cancelled) =>
        {
            SetState(s => s.IsVisible = false);
            _onDismiss?.Invoke(_message?.Id ?? Guid.Empty);
        });
    }

    public override VisualNode Render()
    {
        if (_message == null || !State.IsVisible)
            return null!;

        var (bgColor, iconText) = GetThemeValues(_message.Type);

        return Frame(
            VStack(spacing: 4,
                HStack(spacing: 8,
                    Label(iconText)
                        .FontSize(20)
                        .TextColor(Colors.White),
                    
                    VStack(spacing: 2,
                        Label(_message.Message)
                            .FontSize(16)
                            .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                            .TextColor(Colors.White)
                            .LineBreakMode(Microsoft.Maui.LineBreakMode.WordWrap),
                        
                        _message.RecoveryAction != null
                            ? Label(_message.RecoveryAction)
                                .FontSize(14)
                                .TextColor(new Color(255, 255, 255, 0.9f))
                                .LineBreakMode(Microsoft.Maui.LineBreakMode.WordWrap)
                            : null
                    )
                )
            )
            .Padding(12)
        )
        .BackgroundColor(bgColor)
        .CornerRadius(12)
        .HasShadow(true)
        .TranslationY(State.TranslationY)
        .Margin(16)
        .MinimumHeightRequest(60)
        .OnTapped(Dismiss)
        .SemanticProperties(sp => sp
            .Description(_message.RecoveryAction != null 
                ? $"{_message.Message}. {_message.RecoveryAction}"
                : _message.Message)
            .Announce(_message.Message));
    }

    (Color bgColor, string iconText) GetThemeValues(FeedbackType type)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        
        return type switch
        {
            FeedbackType.Success => (
                isDark ? Color.FromArgb("#8BC34A") : Color.FromArgb("#689F38"),
                "✓"
            ),
            FeedbackType.Error => (
                isDark ? Color.FromArgb("#E57373") : Color.FromArgb("#C62828"),
                "⚠"
            ),
            FeedbackType.Info => (
                isDark ? Color.FromArgb("#64B5F6") : Color.FromArgb("#1976D2"),
                "ℹ"
            ),
            FeedbackType.Warning => (
                isDark ? Color.FromArgb("#FFB74D") : Color.FromArgb("#F57C00"),
                "⚠"
            ),
            _ => (Colors.Gray, "•")
        };
    }
}
