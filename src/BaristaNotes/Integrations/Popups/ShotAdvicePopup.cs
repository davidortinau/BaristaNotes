using BaristaNotes.Core.Services.DTOs;
using Controls = Microsoft.Maui.Controls;

namespace BaristaNotes.Integrations.Popups;

/// <summary>
/// Modal popup that displays AI-generated advice for an existing shot.
/// Shown after a successful call to <see cref="BaristaNotes.Services.IAIAdviceService.GetAdviceForShotAsync"/>
/// from <c>ShotLoggingGridPage</c>'s edit-mode AI tile.
/// </summary>
/// <remarks>
/// The UXDivers <see cref="ActionModalPopup"/> chrome is brand-dark-brown regardless of system
/// theme (matches <c>AddCoffeePopup</c>), so text colors are always sourced from
/// <c>AppColors.Dark.*</c> for correct contrast.
/// </remarks>
public class ShotAdvicePopup : ActionModalPopup
{
    private const int HorizontalPadding = 16;

    private readonly AIAdviceResponseDto _advice;
    private bool _showPromptDetails;

    public ShotAdvicePopup(AIAdviceResponseDto advice)
    {
        _advice = advice ?? throw new ArgumentNullException(nameof(advice));

        Title = "AI Suggestions";
        ActionButtonText = "Close";
        ActionButtonCommand = new Controls.Command(async () =>
        {
            await UXDivers.Popups.Services.IPopupService.Current.PopAsync();
        });

        PopupContent = BuildBody();
    }

    private Controls.View BuildBody()
    {
        var stack = new Controls.VerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(HorizontalPadding, 0)
        };

        var adjustments = _advice.Adjustments ?? new List<ShotAdjustment>();
        if (adjustments.Count > 0)
        {
            foreach (var adj in adjustments)
            {
                stack.Children.Add(BuildAdjustmentRow(adj));
            }
        }
        else
        {
            stack.Children.Add(new Controls.Label
            {
                Text = "No specific adjustments suggested.",
                TextColor = AppColors.Dark.TextSecondary,
                FontSize = 14
            });
        }

        if (!string.IsNullOrWhiteSpace(_advice.Reasoning))
        {
            stack.Children.Add(new Controls.Label
            {
                Text = _advice.Reasoning,
                TextColor = AppColors.Dark.TextSecondary,
                FontSize = 13,
                FontAttributes = Controls.FontAttributes.Italic,
                Margin = new Thickness(0, 4, 0, 0),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        var sourceRow = new Controls.Grid
        {
            ColumnDefinitions = new Controls.ColumnDefinitionCollection
            {
                new Controls.ColumnDefinition { Width = GridLength.Auto },
                new Controls.ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        if (!string.IsNullOrWhiteSpace(_advice.PromptSent))
        {
            var toggleButton = BuildPromptToggleButton();
            Controls.Grid.SetColumn(toggleButton, 0);
            sourceRow.Children.Add(toggleButton);
        }

        var sourceLabel = new Controls.Label
        {
            Text = _advice.Source ?? string.Empty,
            TextColor = AppColors.Dark.TextSecondary,
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center
        };
        Controls.Grid.SetColumn(sourceLabel, 1);
        sourceRow.Children.Add(sourceLabel);

        stack.Children.Add(sourceRow);

        if (_showPromptDetails && !string.IsNullOrWhiteSpace(_advice.PromptSent))
        {
            stack.Children.Add(new Controls.Label
            {
                Text = FormatPromptForDisplay(_advice.PromptSent!, _advice.HistoricalShotsCount),
                TextColor = AppColors.Dark.TextSecondary,
                FontSize = 11,
                FontFamily = "Manrope",
                LineBreakMode = LineBreakMode.WordWrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        return WrapInScrollView(stack);
    }

    private static Controls.View WrapInScrollView(Controls.View content)
    {
        // Cap the popup body at ~60% of screen height so it doesn't grow into the
        // close button (which lives in the popup chrome below PopupContent) and
        // scroll the inner content when it would overflow. This keeps the close
        // button anchored at a stable position regardless of "Show prompt" state.
        var display = DeviceDisplay.Current.MainDisplayInfo;
        var screenHeightDip = display.Height / display.Density;
        var maxHeight = Math.Max(240, screenHeightDip * 0.6);

        return new Controls.ScrollView
        {
            Content = content,
            MaximumHeightRequest = maxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Default
        };
    }

    private Controls.View BuildPromptToggleButton()
    {
        var glyph = new Controls.Label
        {
            Text = _showPromptDetails ? "▾" : "▸",
            TextColor = AppColors.Dark.OnPrimary,
            FontSize = 12,
            VerticalTextAlignment = TextAlignment.Center
        };

        var label = new Controls.Label
        {
            Text = _showPromptDetails ? "Hide prompt" : "Show prompt",
            TextColor = AppColors.Dark.OnPrimary,
            FontSize = 12,
            FontAttributes = Controls.FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };

        var content = new Controls.HorizontalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(10, 6),
            VerticalOptions = LayoutOptions.Center,
            Children = { glyph, label }
        };

        var border = new Controls.Border
        {
            BackgroundColor = AppColors.Dark.SurfaceVariant,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Content = content,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center
        };

        var tap = new Controls.TapGestureRecognizer();
        tap.Tapped += (_, _) => TogglePromptDetails();
        border.GestureRecognizers.Add(tap);
        return border;
    }

    private Controls.View BuildAdjustmentRow(ShotAdjustment adj)
    {
        return new Controls.HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Controls.Label
                {
                    Text = "•",
                    TextColor = AppColors.Dark.TextSecondary,
                    FontSize = 14,
                    VerticalTextAlignment = TextAlignment.Start
                },
                new Controls.Label
                {
                    Text = $"{Capitalize(adj.Direction)} {adj.Parameter} by {adj.Amount}",
                    TextColor = AppColors.Dark.TextPrimary,
                    FontSize = 14,
                    LineBreakMode = LineBreakMode.WordWrap
                }
            }
        };
    }

    private void TogglePromptDetails()
    {
        _showPromptDetails = !_showPromptDetails;
        // Defer PopupContent reassignment so the current tap gesture finishes resolving
        // before the visual tree is replaced. Replacing the Border that hosts the
        // TapGestureRecognizer mid-gesture was causing the second tap (Hide -> Show) to
        // be silently swallowed by iOS while the gesture state was still in flight from
        // the first tap. Application.Current.Dispatcher is guaranteed to be the MAUI UI
        // dispatcher regardless of which thread fires the tap event.
        var dispatcher = Microsoft.Maui.Controls.Application.Current?.Dispatcher
                         ?? Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread();
        if (dispatcher is not null)
        {
            dispatcher.Dispatch(() => PopupContent = BuildBody());
        }
        else
        {
            PopupContent = BuildBody();
        }
    }

    private static string Capitalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return char.ToUpper(value[0]) + value.Substring(1).ToLowerInvariant();
    }

    private static string FormatPromptForDisplay(string prompt, int historicalShotsCount)
    {
        if (historicalShotsCount <= 3) return prompt;
        return prompt + Environment.NewLine + Environment.NewLine
            + $"(prompt includes {historicalShotsCount} historical shots, abbreviated)";
    }
}
