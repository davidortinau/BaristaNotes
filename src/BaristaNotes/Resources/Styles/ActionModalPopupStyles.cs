using Microsoft.Maui.Devices;
using UXDivers.Popups.Maui.Controls;
using Controls = Microsoft.Maui.Controls;

namespace BaristaNotes.Resources.Styles;

internal sealed class ActionModalPopupStyles : Controls.ResourceDictionary
{
    private const string PackageStyleKey = "DefaultActionModalPopupStyle";

    public ActionModalPopupStyles(Controls.ResourceDictionary packageStyles)
    {
        if (packageStyles[PackageStyleKey] is not Controls.Style packageStyle)
        {
            throw new InvalidOperationException(
                $"UXDivers popup resources do not contain '{PackageStyleKey}'.");
        }

        var style = new Controls.Style(typeof(ActionModalPopup))
        {
            ApplyToDerivedTypes = true,
            BasedOn = packageStyle
        };
        style.Setters.Add(new Controls.Setter
        {
            Property = Controls.TemplatedView.ControlTemplateProperty,
            Value = new Controls.ControlTemplate(CreateTemplate)
        });

        Add(style);
    }

    private static Controls.View CreateTemplate()
    {
        var root = new Controls.Grid
        {
            SafeAreaEdges = new SafeAreaEdges(
                SafeAreaRegions.None,
                SafeAreaRegions.None,
                SafeAreaRegions.None,
                SafeAreaRegions.None),
            VerticalOptions = DeviceInfo.Current.Idiom == DeviceIdiom.Phone
                ? Controls.LayoutOptions.Fill
                : Controls.LayoutOptions.Center,
            HorizontalOptions = Controls.LayoutOptions.Fill,
            RowDefinitions =
            {
                new Controls.RowDefinition { Height = GridLength.Auto },
                new Controls.RowDefinition { Height = GridLength.Star },
                new Controls.RowDefinition { Height = GridLength.Auto }
            }
        };
        root.SetDynamicResource(Controls.Grid.RowSpacingProperty, "SpacingMedium");
        root.SetDynamicResource(Controls.Grid.PaddingProperty, "PopupAirSpacing");

        var background = new Controls.Border
        {
            Margin = new Thickness(-24),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(24)
            },
            HorizontalOptions = Controls.LayoutOptions.Fill
        };
        background.SetDynamicResource(Controls.Border.BackgroundColorProperty, "BackgroundColor");
        Controls.Grid.SetRowSpan(background, 3);

        var title = new Controls.Label
        {
            HorizontalTextAlignment = TextAlignment.Center
        };
        title.SetDynamicResource(Controls.Label.StyleProperty, "TitleStyle");
        title.SetDynamicResource(Controls.Label.FontSizeProperty, "SmallTitleFontSize");
        title.SetBinding(
            Controls.Label.TextProperty,
            static (ActionModalPopup popup) => popup.Title,
            source: Controls.RelativeBindingSource.TemplatedParent);

        var closeButton = new Controls.Label();
        closeButton.SetDynamicResource(Controls.Label.StyleProperty, "CloseButtonStyle");
        closeButton.SetBinding(
            Controls.Label.TextProperty,
            static (ActionModalPopup popup) => popup.CloseButtonIconText,
            source: Controls.RelativeBindingSource.TemplatedParent);
        closeButton.SetBinding(
            Controls.Label.TextColorProperty,
            static (ActionModalPopup popup) => popup.CloseButtonIconColor,
            source: Controls.RelativeBindingSource.TemplatedParent);

        var closeGesture = new Controls.TapGestureRecognizer();
        closeGesture.SetBinding(
            Controls.TapGestureRecognizer.CommandProperty,
            static (ActionModalPopup popup) => popup.CloseButtonCommand,
            source: Controls.RelativeBindingSource.TemplatedParent);
        closeButton.GestureRecognizers.Add(closeGesture);

        var content = new Controls.ContentPresenter();
        content.SetBinding(
            Controls.ContentPresenter.ContentProperty,
            static (ActionModalPopup popup) => popup.Content,
            source: Controls.RelativeBindingSource.TemplatedParent);
        Controls.Grid.SetRow(content, 1);

        var actionButton = new Controls.Button();
        actionButton.SetDynamicResource(Controls.Button.StyleProperty, "PrimaryActionButtonStyle");
        actionButton.SetBinding(
            Controls.Button.TextProperty,
            static (ActionModalPopup popup) => popup.ActionButtonText,
            source: Controls.RelativeBindingSource.TemplatedParent);
        actionButton.SetBinding(
            Controls.Button.CommandProperty,
            static (ActionModalPopup popup) => popup.ActionButtonCommand,
            source: Controls.RelativeBindingSource.TemplatedParent);
        actionButton.SetBinding(
            Controls.Button.IsVisibleProperty,
            static (ActionModalPopup popup) => popup.ShowActionButton,
            source: Controls.RelativeBindingSource.TemplatedParent);
        Controls.Grid.SetRow(actionButton, 2);

        root.Children.Add(background);
        root.Children.Add(title);
        root.Children.Add(closeButton);
        root.Children.Add(content);
        root.Children.Add(actionButton);

        return root;
    }
}
