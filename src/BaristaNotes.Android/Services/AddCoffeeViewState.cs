using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.AndroidApp.Services;

internal enum AddCoffeeMode { Browse, Type, Scanning }
internal sealed class AddCoffeeViewState
{
    private BeanLabelExtraction? _pending;
    private DateTime _date = DateTime.Today;
    public AddCoffeeMode Mode { get; private set; } = AddCoffeeMode.Type;
    public AddCoffeeDraft Draft { get; private set; } = new();
    public void Initialize(bool hasRecent, BeanLabelExtraction? extraction)
    {
        _pending = extraction;
        Mode = extraction is not null || !hasRecent ? AddCoffeeMode.Type : AddCoffeeMode.Browse;
    }

    public void Browse() => Mode = AddCoffeeMode.Browse;
    public void Type(BeanLabelExtraction? extraction = null)
    {
        _pending = extraction;
        Mode = AddCoffeeMode.Type;
    }
    public void Scanning() => Mode = AddCoffeeMode.Scanning;
    public void BeginTypeRender(DateTime today)
    {
        var extraction = _pending;
        _pending = null;
        if (extraction?.RoastDate is DateTime date && date <= today.Date) _date = date.Date;
        Draft = new()
        {
            Name = extraction?.Name ?? "", Roaster = extraction?.Roaster ?? "",
            Origin = extraction?.Origin ?? "", Notes = extraction?.Notes ?? "", RoastDate = _date
        };
    }
    public void SetDate(DateTime date) { _date = date; Draft.RoastDate = date; }
    public AddCoffeeDraft Snapshot() => new()
    {
        Name = Draft.Name, Roaster = Draft.Roaster, Origin = Draft.Origin,
        Notes = Draft.Notes, RoastDate = Draft.RoastDate
    };
    public static IReadOnlyList<string> Suggestions(IReadOnlyList<string> pool, string? filter) =>
        (string.IsNullOrWhiteSpace(filter) ? pool : pool.Where(value =>
            value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase))).Take(3).ToArray();
}
