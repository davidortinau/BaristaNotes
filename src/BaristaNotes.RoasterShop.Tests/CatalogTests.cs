using System.Text.Json;
using BaristaNotes.RoasterShop.Shop;

namespace BaristaNotes.RoasterShop.Tests;

public class CatalogTests
{
    private readonly TestShop _shop = new();

    [Fact]
    public void Has_six_roasters_and_eighteen_coffees_with_the_id_scheme()
    {
        Assert.Equal(["lanternfish", "wickerline", "ninefold", "tidewell", "copperhaze", "fennick"],
            _shop.Catalog.Roasters.Select(r => r.Handle));
        Assert.Equal(18, _shop.Catalog.Products.Count);
        Assert.All(_shop.Catalog.Products, p =>
        {
            Assert.Equal(p.Roaster.Number * 100 + p.N, p.Id);
            Assert.Equal(p.Id * 10 + 1, p.VariantId);
        });
        Assert.Equal(["Kochere Natural", "Sumatra Wet-hulled", "Sidamo Natural"],
            _shop.Catalog.Products.Where(p => !p.Available).Select(p => p.Title));
    }

    [Fact]
    public void Product_json_matches_the_shopify_example()
    {
        var guji = _shop.Catalog.FindVariant(TestShop.GujiVariant)!;

        var json = JsonSerializer.Serialize(_shop.Catalog.ToJson(guji), ShopJson.Options);
        using var doc = JsonDocument.Parse(json);
        var p = doc.RootElement;

        Assert.Equal(501, p.GetProperty("id").GetInt64());
        Assert.Equal("Guji Natural", p.GetProperty("title").GetString());
        Assert.Equal("guji-natural", p.GetProperty("handle").GetString());
        Assert.Equal("Copperhaze Coffee", p.GetProperty("vendor").GetString());
        Assert.Equal("Coffee", p.GetProperty("product_type").GetString());
        Assert.StartsWith("<p>", p.GetProperty("body_html").GetString());
        // Fixed clock: 2026-10-21, roasted 5 days ago.
        Assert.Equal(
            ["origin:Ethiopia", "region:Guji", "process:natural", "roast:light", "notes:blueberry", "notes:strawberry", "roasted:2026-10-16"],
            p.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
        var v = p.GetProperty("variants")[0];
        Assert.Equal(5011, v.GetProperty("id").GetInt64());
        Assert.Equal("250 g", v.GetProperty("title").GetString());
        Assert.Equal("17.50", v.GetProperty("price").GetString());
        Assert.True(v.GetProperty("available").GetBoolean());
        Assert.Equal("COP-501-250", v.GetProperty("sku").GetString());
        Assert.Equal(250, v.GetProperty("grams").GetInt32());
    }

    [Fact]
    public void Decaf_is_tagged_and_the_blend_has_two_origins()
    {
        var today = _shop.Catalog.Today;
        var decaf = _shop.Catalog.FindVariant(1041)!;
        var blend = _shop.Catalog.FindVariant(1031)!;

        Assert.Contains("decaf:yes", Catalog.Tags(decaf, today));
        Assert.Contains("notes:red apple", Catalog.Tags(decaf, today));
        Assert.Equal(["origin:Brazil", "origin:Colombia"], Catalog.Tags(blend, today).Where(t => t.StartsWith("origin:")));
        Assert.Equal("Brazil and Colombia blend", blend.OriginLabel);
    }

    [Fact]
    public void Roast_dates_follow_the_clock()
    {
        var kirinyaga = _shop.Catalog.FindVariant(4021)!;

        Assert.Equal(new DateOnly(2026, 9, 11), kirinyaga.RoastDate(_shop.Catalog.Today));
        Assert.Equal(new DateOnly(2026, 12, 21), kirinyaga.RoastDate(new DateOnly(2027, 1, 30)));
    }

    [Theory]
    [InlineData("Gesha Lot 7", "gesha-lot-7")]
    [InlineData("Sumatra Wet-hulled", "sumatra-wet-hulled")]
    [InlineData("Kirinyaga AA", "kirinyaga-aa")]
    public void Handles_are_slugs(string title, string handle) => Assert.Equal(handle, CatalogProduct.Slug(title));

    [Fact]
    public void Roaster_json_has_money_as_a_string()
    {
        var json = JsonSerializer.Serialize(Catalog.ToJson(_shop.Catalog.FindRoaster("lanternfish")!), ShopJson.Options);

        Assert.Equal("""{"handle":"lanternfish","name":"Lanternfish Coffee","city":"Utrecht","shipping":"4.90","currency":"EUR"}""", json);
    }

    [Theory]
    [InlineData("5011:1", true)]
    [InlineData("5011:2,5031:1", true)]
    [InlineData("5011", false)]
    [InlineData("abc:1", false)]
    [InlineData("5011:-1", false)]
    public void Cart_permalinks_parse(string spec, bool ok) => Assert.Equal(ok, ShopApi.TryParsePermalink(spec, out _));
}
