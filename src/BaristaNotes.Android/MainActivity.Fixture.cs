#if NATIVE_UI_FIXTURE
using BaristaNotes.AndroidApp.Views;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private bool _failNextBagRefresh;

    private void OpenFixtureMenu()
    {
        // Only the isolated UI fixture build exposes source-supported routes
        // whose broader Settings navigation is not implemented in this slice.
        ShowChoices("UI FIXTURE ONLY",
        [
            new NativeChoice("FixtureCreateBean", "TEST: open native Bean Detail form", false,
                Choice(ShowBeanForm))
        ]);
    }
}
#endif
