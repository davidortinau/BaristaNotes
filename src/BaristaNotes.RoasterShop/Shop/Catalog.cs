using System.Globalization;
using System.Text;

namespace BaristaNotes.RoasterShop.Shop;

public sealed record Roaster(int Number, string Handle, string Name, string City, decimal Shipping)
{
    public string SkuPrefix => Handle[..3].ToUpperInvariant();
}

/// <summary>
/// One coffee. Every coffee has a single 250 g variant.
/// Product id = roaster number * 100 + n. Variant id = product id * 10 + 1.
/// </summary>
public sealed record CatalogProduct(
    Roaster Roaster,
    int N,
    string Title,
    IReadOnlyList<string> Origins,
    string? Region,
    string Process,
    string Roast,
    IReadOnlyList<string> Notes,
    decimal Price,
    int RoastedDaysAgo,
    bool Available,
    bool Decaf = false)
{
    public const int Grams = 250;
    public const string VariantTitle = "250 g";

    public long Id => Roaster.Number * 100 + N;
    public long VariantId => Id * 10 + 1;
    public string Handle => Slug(Title);
    public string Sku => $"{Roaster.SkuPrefix}-{Id}-{Grams}";
    public bool IsBlend => Origins.Count > 1;

    /// <summary>"Ethiopia, Guji" or "Brazil and Colombia blend".</summary>
    public string OriginLabel => IsBlend
        ? $"{string.Join(" and ", Origins)} blend"
        : Region is null ? Origins[0] : $"{Origins[0]}, {Region}";

    public string ProcessLabel => Decaf ? $"{Process}, decaf" : Process;

    public DateOnly RoastDate(DateOnly today) => today.AddDays(-RoastedDaysAgo);

    public static string Slug(string title)
    {
        var sb = new StringBuilder(title.Length);
        foreach (var c in title.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }
}

// Shopify-shaped JSON for /roasters.json and /{handle}/products.json.
public sealed record RoasterJson(string Handle, string Name, string City, decimal Shipping, string Currency);

public sealed record ProductJson(
    long Id,
    string Title,
    string Handle,
    string Vendor,
    string ProductType,
    string BodyHtml,
    IReadOnlyList<string> Tags,
    IReadOnlyList<VariantJson> Variants);

public sealed record VariantJson(long Id, string Title, decimal Price, bool Available, string Sku, int Grams);

/// <summary>
/// The fixed catalog of six fictional roasters. Roast dates are computed from
/// "today" on every call so the demo data never goes stale.
/// </summary>
public sealed class Catalog
{
    private readonly TimeProvider _time;
    private readonly Dictionary<string, Roaster> _roastersByHandle;
    private readonly Dictionary<long, CatalogProduct> _productsByVariant;

    public Catalog(TimeProvider time)
    {
        _time = time;
        _roastersByHandle = Roasters.ToDictionary(r => r.Handle, StringComparer.OrdinalIgnoreCase);
        _productsByVariant = Products.ToDictionary(p => p.VariantId);
    }

    public IReadOnlyList<Roaster> Roasters { get; } = Fixture.Roasters;
    public IReadOnlyList<CatalogProduct> Products { get; } = Fixture.Products;

    public DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    public Roaster? FindRoaster(string? handle) =>
        handle is not null && _roastersByHandle.TryGetValue(handle, out var r) ? r : null;

    public CatalogProduct? FindVariant(long variantId) =>
        _productsByVariant.TryGetValue(variantId, out var p) ? p : null;

    public IReadOnlyList<CatalogProduct> ProductsFor(Roaster roaster) =>
        Products.Where(p => p.Roaster.Number == roaster.Number).ToList();

    public static RoasterJson ToJson(Roaster r) => new(r.Handle, r.Name, r.City, r.Shipping, Money.Currency);

    public ProductJson ToJson(CatalogProduct p) => ToJson(p, Today);

    public static ProductJson ToJson(CatalogProduct p, DateOnly today) => ToJson(p, today, p.Price);

    /// <summary>As above, with the current price (the price-increase fault may change it).</summary>
    public static ProductJson ToJson(CatalogProduct p, DateOnly today, decimal price) => new(
        p.Id,
        p.Title,
        p.Handle,
        p.Roaster.Name,
        "Coffee",
        BodyHtml(p),
        Tags(p, today),
        [new VariantJson(p.VariantId, CatalogProduct.VariantTitle, price, p.Available, p.Sku, CatalogProduct.Grams)]);

    /// <summary>
    /// Tag prefixes are lowercase. Origin and region keep their place-name
    /// capitals, as in the Shopify example ("origin:Ethiopia"). Process, roast,
    /// and notes are lowercase. The date is roasted:yyyy-MM-dd.
    /// </summary>
    public static IReadOnlyList<string> Tags(CatalogProduct p, DateOnly today)
    {
        var tags = new List<string>();
        tags.AddRange(p.Origins.Select(o => $"origin:{o}"));
        if (p.Region is not null) tags.Add($"region:{p.Region}");
        tags.Add($"process:{p.Process.ToLowerInvariant()}");
        tags.Add($"roast:{p.Roast.ToLowerInvariant()}");
        tags.AddRange(p.Notes.Select(n => $"notes:{n.ToLowerInvariant()}"));
        tags.Add($"roasted:{p.RoastDate(today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (p.Decaf) tags.Add("decaf:yes");
        return tags;
    }

    private static string BodyHtml(CatalogProduct p)
    {
        var what = p.IsBlend
            ? $"A {p.Process} blend of {string.Join(" and ", p.Origins)}"
            : $"A {p.Process}{(p.Decaf ? " decaf" : " coffee")} from {(p.Region is null ? p.Origins[0] : $"{p.Region}, {p.Origins[0]}")}";
        var notes = p.Notes.Count switch
        {
            1 => p.Notes[0],
            _ => $"{string.Join(", ", p.Notes.Take(p.Notes.Count - 1))} and {p.Notes[^1]}",
        };
        return $"<p>{what}, roasted {p.Roast}. In the cup: {notes}.</p>";
    }

    private static class Fixture
    {
        private static readonly Roaster Lanternfish = new(1, "lanternfish", "Lanternfish Coffee", "Utrecht", 4.90m);
        private static readonly Roaster Wickerline = new(2, "wickerline", "Wickerline Roasters", "Ghent", 5.50m);
        private static readonly Roaster Ninefold = new(3, "ninefold", "Ninefold Coffee", "Antwerp", 4.50m);
        private static readonly Roaster Tidewell = new(4, "tidewell", "Tidewell Roasting", "Rotterdam", 5.00m);
        private static readonly Roaster Copperhaze = new(5, "copperhaze", "Copperhaze Coffee", "Leuven", 5.00m);
        private static readonly Roaster Fennick = new(6, "fennick", "Fennick Roastery", "Haarlem", 6.00m);

        public static readonly IReadOnlyList<Roaster> Roasters =
            [Lanternfish, Wickerline, Ninefold, Tidewell, Copperhaze, Fennick];

        public static readonly IReadOnlyList<CatalogProduct> Products =
        [
            new(Lanternfish, 1, "Kochere Natural", ["Ethiopia"], "Yirgacheffe", "natural", "light", ["blueberry", "jasmine"], 17.50m, 3, Available: false),
            new(Lanternfish, 2, "Brazil Cerrado", ["Brazil"], "Cerrado", "natural", "medium", ["peanut", "cocoa"], 14.00m, 30, Available: true),
            new(Lanternfish, 3, "House Espresso", ["Brazil", "Colombia"], null, "washed", "dark", ["dark chocolate", "molasses"], 13.00m, 6, Available: true),
            new(Lanternfish, 4, "Decaf Colombia", ["Colombia"], "Tolima", "washed", "medium", ["caramel", "red apple"], 15.00m, 9, Available: true, Decaf: true),

            new(Wickerline, 1, "Huila Washed", ["Colombia"], "Huila", "washed", "medium", ["milk chocolate", "caramel"], 15.50m, 4, Available: true),
            new(Wickerline, 2, "Gesha Lot 7", ["Panama"], "Boquete", "washed", "light", ["bergamot", "jasmine", "peach"], 119.00m, 3, Available: true),
            new(Wickerline, 3, "Tolima Honey", ["Colombia"], "Tolima", "honey", "medium", ["panela", "orange"], 16.00m, 25, Available: true),

            new(Ninefold, 1, "Santos Dark", ["Brazil"], "Santos", "natural", "dark", ["cocoa", "smoke"], 12.00m, 7, Available: true),
            new(Ninefold, 2, "Huehuetenango Washed", ["Guatemala"], "Huehuetenango", "washed", "medium", ["chocolate", "green apple"], 16.00m, 8, Available: true),
            new(Ninefold, 3, "Sumatra Wet-hulled", ["Indonesia"], "Aceh", "wet-hulled", "medium", ["cedar", "herbs"], 15.00m, 12, Available: false),

            new(Tidewell, 1, "Nyeri Washed", ["Kenya"], "Nyeri", "washed", "light", ["grapefruit", "blackcurrant"], 18.00m, 6, Available: true),
            new(Tidewell, 2, "Kirinyaga AA", ["Kenya"], "Kirinyaga", "washed", "light", ["tomato", "blackcurrant"], 21.00m, 40, Available: true),
            new(Tidewell, 3, "Rwanda Natural", ["Rwanda"], "Nyamasheke", "natural", "light", ["raspberry", "hibiscus"], 17.00m, 12, Available: true),

            new(Copperhaze, 1, "Guji Natural", ["Ethiopia"], "Guji", "natural", "light", ["blueberry", "strawberry"], 17.50m, 5, Available: true),
            new(Copperhaze, 2, "Sidamo Natural", ["Ethiopia"], "Sidamo", "natural", "light", ["strawberry", "red wine"], 17.00m, 10, Available: false),
            new(Copperhaze, 3, "Limu Washed", ["Ethiopia"], "Limu", "washed", "light", ["lemon", "black tea"], 16.00m, 22, Available: true),

            new(Fennick, 1, "Burundi Natural", ["Burundi"], "Kayanza", "natural", "light", ["red berries", "cacao nib"], 19.00m, 6, Available: true),
            new(Fennick, 2, "Yunnan Honey", ["China"], "Yunnan", "honey", "medium", ["brown sugar", "plum"], 16.50m, 11, Available: true),
        ];
    }
}
