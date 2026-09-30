using System.Globalization;

namespace BaristaNotes.AndroidApp.Views;

internal static class NumericChoiceIdentity
{
    // Display precision must not merge distinct rows (for example 201 and 201.002).
    public static string For(decimal value) =>
        "NumericValue_" + value.ToString("G29", CultureInfo.InvariantCulture);
}
