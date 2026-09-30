using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Tests.Mocks;

namespace BaristaNotes.Tests.Unit;

public sealed class ThemePreferenceTests
{
    [Fact]
    public void UnsetMode_FollowsSystemWithoutWriting()
    {
        var store = new MockPreferencesStore();
        Assert.Equal(ThemeMode.System, ThemePreference.Read(store));
        Assert.False(ThemePreference.TryRead(store, out var mode));
        Assert.Equal(ThemeMode.System, mode);
        Assert.Null(store.Get(ThemePreference.Key, (string?)null));
    }

    [Theory]
    [InlineData(ThemeMode.Light, "Light")]
    [InlineData(ThemeMode.Dark, "Dark")]
    [InlineData(ThemeMode.System, "System")]
    public void PersistedMode_UsesSourceKeyAndNames(ThemeMode mode, string text)
    {
        var store = new MockPreferencesStore();
        ThemePreference.Write(store, mode);
        Assert.Equal(text, store.Get("AppThemeMode", ""));
        Assert.Equal(mode, ThemePreference.Read(store));
        Assert.True(ThemePreference.TryRead(store, out var parsed));
        Assert.Equal(mode, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("dark")]
    [InlineData("LIGHT")]
    [InlineData("unsupported")]
    public void InvalidSourceText_FallsBackWithoutRepairingStoredValue(string text)
    {
        var store = new MockPreferencesStore();
        store.Set(ThemePreference.Key, text);
        Assert.False(ThemePreference.TryRead(store, out var mode));
        Assert.Equal(ThemeMode.System, mode);
        Assert.Equal(text, store.Get(ThemePreference.Key, ""));
    }

    [Theory]
    [InlineData("0", ThemeMode.Light)]
    [InlineData("1", ThemeMode.Dark)]
    [InlineData("2", ThemeMode.System)]
    [InlineData("99", (ThemeMode)99)]
    [InlineData(" Dark ", ThemeMode.Dark)]
    public void SourceEnumParsing_IsPreserved(string text, ThemeMode expected)
    {
        var store = new MockPreferencesStore();
        store.Set(ThemePreference.Key, text);
        Assert.Equal(expected, ThemePreference.Read(store));
    }
}
