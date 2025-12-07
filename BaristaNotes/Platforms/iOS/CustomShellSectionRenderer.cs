using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using UIKit;
using Page = Microsoft.Maui.Controls.Page;

namespace BaristaNotes.Platforms.iOS;

/// <summary>
/// Custom ShellSectionRenderer that adds support for PrefersLargeTitles on iOS.
/// </summary>
public class CustomShellSectionRenderer : ShellSectionRenderer
{
    private bool _prefersLargeTitles;
    private readonly IShellContext _shellContext;

    public CustomShellSectionRenderer(IShellContext context) : base(context)
    {
        _shellContext = context;
    }

    public bool PrefersLargeTitles
    {
        get => _prefersLargeTitles;
        set
        {
            _prefersLargeTitles = value;
            UpdatePrefersLargeTitles();
        }
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        UpdatePrefersLargeTitles();
    }

    public override void PushViewController(UIViewController viewController, bool animated)
    {
        ConfigureViewControllerForLargeTitles(viewController);
        base.PushViewController(viewController, animated);
    }

    public override void SetViewControllers(UIViewController[] viewControllers, bool animated)
    {
        foreach (var vc in viewControllers)
        {
            ConfigureViewControllerForLargeTitles(vc);
        }
        base.SetViewControllers(viewControllers, animated);
    }

    private void UpdatePrefersLargeTitles()
    {
        if (NavigationBar is null)
            return;

        if (OperatingSystem.IsIOSVersionAtLeast(11) || OperatingSystem.IsMacCatalystVersionAtLeast(11))
        {
            NavigationBar.PrefersLargeTitles = _prefersLargeTitles;

            if (ViewControllers != null)
            {
                foreach (var vc in ViewControllers)
                {
                    ConfigureViewControllerForLargeTitles(vc);
                }
            }
        }
    }

    private void ConfigureViewControllerForLargeTitles(UIViewController viewController)
    {
        if (!_prefersLargeTitles)
            return;

        if (!OperatingSystem.IsIOSVersionAtLeast(11) && !OperatingSystem.IsMacCatalystVersionAtLeast(11))
            return;

        var page = GetPageFromViewController(viewController);

        if (page != null)
        {
            // Set LargeTitleDisplayMode
            var largeTitleDisplayMode = page.On<Microsoft.Maui.Controls.PlatformConfiguration.iOS>().LargeTitleDisplay();

            viewController.NavigationItem.LargeTitleDisplayMode = largeTitleDisplayMode switch
            {
                LargeTitleDisplayMode.Always => UINavigationItemLargeTitleDisplayMode.Always,
                LargeTitleDisplayMode.Never => UINavigationItemLargeTitleDisplayMode.Never,
                _ => UINavigationItemLargeTitleDisplayMode.Automatic
            };

            // THE KEY FIX: Set NavigationItem.Title directly from Page.Title
            // ShellPageRendererTracker. UpdateTitle() has a guard that checks for Shell.Toolbar
            // and uses Toolbar.Title.  This bypasses that and sets the title directly,
            // which is what iOS needs for the inline (collapsed) title to appear.
            if (!string.IsNullOrEmpty(page.Title))
            {
                viewController.NavigationItem.Title = page.Title;
            }

            // Subscribe to title changes
            page.PropertyChanged -= OnPagePropertyChanged;
            page.PropertyChanged += OnPagePropertyChanged;
        }
        else
        {
            viewController.NavigationItem.LargeTitleDisplayMode = UINavigationItemLargeTitleDisplayMode.Automatic;
        }
    }

    private void OnPagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != Page.TitleProperty.PropertyName)
            return;

        if (sender is not Page page)
            return;

        // Update the NavigationItem.Title when the page title changes
        if (page.Handler is IPlatformViewHandler handler && handler.ViewController != null)
        {
            handler.ViewController.NavigationItem.Title = page.Title;
        }
    }

    private Page? GetPageFromViewController(UIViewController viewController)
    {
        if (viewController is IPlatformViewHandler handler && handler.VirtualView is Page page)
        {
            return page;
        }

        return null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && ViewControllers != null)
        {
            foreach (var vc in ViewControllers)
            {
                var page = GetPageFromViewController(vc);
                if (page != null)
                {
                    page.PropertyChanged -= OnPagePropertyChanged;
                }
            }
        }
        base.Dispose(disposing);
    }
}