using MauiReactor;
using BaristaNotes.Models;
using BaristaNotes.Styles;

namespace BaristaNotes.Components.Feedback;

class ToastComponentState
{
    public bool IsVisible { get; set; } = true;
}

partial class ToastComponent : Component<ToastComponentState>
{
    [Prop]
    FeedbackMessage? _message;

    [Prop]
    Action<Guid>? _onDismiss;

    private bool _isComponentMounted = false;

    protected override void OnMounted()
    {
        _isComponentMounted = true;
        if (_message == null) return;

        // Auto-dismiss after duration
        Task.Delay(_message.DurationMs).ContinueWith(_ =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_isComponentMounted) // Only set state if still mounted
                {
                    Dismiss();
                }
            });
        });

        base.OnMounted();
    }

    protected override void OnWillUnmount()
    {
        _isComponentMounted = false;
        base.OnWillUnmount();
    }

    void Dismiss()
    {
        if (_isComponentMounted)
        {
            SetState(s => s.IsVisible = false);
        }
        _onDismiss?.Invoke(_message?.Id ?? Guid.Empty);
    }

    public override VisualNode Render()
    {
        if (_message == null || !State.IsVisible)
            return null!;

        var (bgColor, iconText) = GetThemeValues(_message.Type);

        return Border(
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
        .Stroke(bgColor)
        .StrokeThickness(1)
        .Padding(12)
        .Margin(16)
        .MinimumHeightRequest(60)
        .OnTapped(Dismiss);
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
