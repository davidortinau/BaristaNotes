using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private BeanMapState? _beanMapReturnState;
    private Android.OS.IParcelable? _beanListReturnState;
    private bool _beanTitlePinned;

    private BeanMapPanel AddBeanMap(NativeScreen screen)
    {
        var panel = new BeanMapPanel(_style, _logger, _app.Services.GetRequiredService<IOriginGeocoder>(), _beanMapReturnState);
        screen.OnDispose(() => _beanMapReturnState = panel.CaptureState() ?? _beanMapReturnState);
        Bind(screen, panel.ResetButton, panel.ClearSelection);
        Bind(screen, panel.RetryButton, panel.RetryOrigins);
        return panel;
    }

    private void RestoreBeanListPosition(SelectorRecyclerView list, NativeScreen screen)
    {
        list.Post(() =>
        {
            if (_destroyed || !ReferenceEquals(_transient, screen) || _beanListReturnState == null) return;
            list.GetLayoutManager()?.OnRestoreInstanceState(_beanListReturnState);
            _beanListReturnState.Dispose();
            _beanListReturnState = null;
        });
    }
}
