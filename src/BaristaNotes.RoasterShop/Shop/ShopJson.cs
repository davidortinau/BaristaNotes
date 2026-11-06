using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BaristaNotes.RoasterShop.Shop;

/// <summary>
/// JSON settings for the Shopify-shaped API and the webhook body: snake_case
/// names, lowercase enum values, and money as strings ("17.50").
/// </summary>
public static class ShopJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        options.Converters.Add(new MoneyJsonConverter());
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}

/// <summary>Every decimal in this shop is money. Shopify sends money as strings.</summary>
public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
            : reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteStringValue(Money.Format(value));
}

public static class Money
{
    public const string Currency = "EUR";

    /// <summary>"17.50", as in the JSON.</summary>
    public static string Format(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>"€17.50", for pages.</summary>
    public static string Display(decimal value) => "€" + Format(value);
}
