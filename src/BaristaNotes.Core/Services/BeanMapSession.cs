using Mapsui;
using Mapsui.Layers;
using Mapsui.Manipulations;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services;

public sealed record BeanMapState(double CenterX, double CenterY, double Resolution, double Rotation,
    bool InitialFitDone, IReadOnlyList<string> SelectedCountries, IReadOnlyList<string>? SelectedLocations = null);

public sealed class BeanMapSession : IDisposable
{
    public const string UserAgent = "BaristaNotes-Native-Maps/0.1";
    public const string LoadFailure = "Map tiles could not load. The bean list is still available.";

    private readonly ILogger _logger;
    private readonly IOriginGeocoder? _geocoder;
    private readonly Action<Action> _dispatch;
    private readonly object _gate = new();
    private readonly TileLayer _tiles;
    private readonly MemoryLayer _origins = new("Saved bean origins") { Style = null };
    private bool _initialFitDone;
    private string[] _selectedLocations = [];
    private string[] _pendingCountries = [];
    private readonly Dictionary<string, OriginLocation> _resolvedLocations = new(StringComparer.Ordinal);
    private BeanDto[] _beans = [];
    private CancellationTokenSource? _lookupCancellation;
    private int _lookupVersion;
    private string? _geocodeError;
    private BeanOriginMapData? _data;
    private bool _disposed;
    private string? _error;

    public Map Map { get; } = new();
    public string? Error
    {
        get
        {
            lock (_gate)
            {
                var error = string.Join(" ", new[] { _error, _geocodeError }.OfType<string>());
                return error.Length > 0 ? error : null;
            }
        }
    }
    public event EventHandler? Changed;
    public event EventHandler? SelectionChanged;
    public event EventHandler? OriginsChanged;
    public BeanOriginMapData? OriginData => _data;
    public bool HasSelection => _data != null && _selectedLocations.Length > 0;
    public bool HasPendingSelection => _data == null && (_selectedLocations.Length > 0 || _pendingCountries.Length > 0);
    public bool HasGeocodeFailure => _geocodeError != null && _data?.Lookups.Count > 0;
    public IReadOnlyList<BeanDto> SelectedBeans => _data?.BeansForPlaces(_selectedLocations) ?? [];
    public string SelectionTitle
    {
        get
        {
            var places = _data?.Places.Where(place => _selectedLocations.Contains(place.Id, StringComparer.Ordinal)).ToArray() ?? [];
            return BeanOriginClusters.GetTitle(places);
        }
    }
    public string SelectionLabel => HasSelection ? $"{SelectionTitle} ({SelectedBeans.Count})" : "";
    public string OriginStatus => _data == null ? "World overview - loading saved origins."
        : _data.Countries.Count == 0
            ? $"World overview - no mapped origins. {_data.UnmappedBeans.Count} unmapped beans."
            : $"{_data.MappedBeans.Count} mapped beans; {_data.UnmappedBeans.Count} unmapped. " +
                "Approximate country locations; resolved cities/localities/regions where available. " +
                string.Join("; ", _data.Places.Select(place => $"{place.Label}: {place.Beans.Count}").Distinct());
    public string FormerFooterStatus => _data == null ? "World overview - loading saved origins."
        : _data.Countries.Count == 0
            ? $"World overview - no mapped origins. {_data.UnmappedBeans.Count} unmapped beans."
            : $"{_data.MappedBeans.Count} mapped beans; {_data.UnmappedBeans.Count} unmapped. Approximate country locations.";

    public BeanMapSession(ILogger logger, BeanMapState? state = null, IOriginGeocoder? geocoder = null,
        Action<Action>? dispatch = null)
    {
        _logger = logger;
        _geocoder = geocoder;
        var context = SynchronizationContext.Current;
        _dispatch = dispatch ?? (action =>
        {
            if (context != null) context.Post(_ => action(), null);
            else action();
        });
        _ = BeanOriginMapData.CountryCatalogue;
        _tiles = OpenStreetMap.CreateTileLayer(userAgent: UserAgent);
        if (geocoder != null) _tiles.Attribution.Text += " | Search: Nominatim";
        _tiles.Attribution.BackColor = Mapsui.Styles.Color.White;
        _tiles.Attribution.Opacity = 1;
        _tiles.DataChanged += OnTileDataChanged;
        _origins.Attribution.Enabled = false;
        Map.Layers.Add(_tiles);
        Map.Layers.Add(_origins);
        Map.Tapped += OnTapped;
        Map.PointerPressed += OnPointerPressed;
        Map.Navigator.ViewportChanged += OnViewportChanged;
        if (state != null)
        {
            _initialFitDone = state.InitialFitDone;
            _selectedLocations = state.SelectedLocations?.ToArray() ?? [];
            if (state.SelectedLocations == null) _pendingCountries = state.SelectedCountries.ToArray();
            Map.Navigator.CenterOnAndZoomTo(new MPoint(state.CenterX, state.CenterY), state.Resolution, duration: 0);
            Map.Navigator.RotateTo(state.Rotation, duration: 0);
        }
        _logger.LogDebug("Native bean map session created");
    }

    public void SetBeans(IEnumerable<BeanDto> savedNonDeletedBeans)
    {
        if (_disposed) return;
        _beans = savedNonDeletedBeans.DistinctBy(bean => bean.Id).ToArray();
        CancelOriginResolution();
        _geocodeError = null;
        _data = new BeanOriginMapData(_beans, _resolvedLocations);
        if (_pendingCountries.Length > 0)
        {
            _selectedLocations = _data.Places.Where(place => _pendingCountries.Contains(place.Country.Name, StringComparer.Ordinal))
                .Select(place => place.Id).ToArray();
            _pendingCountries = [];
        }
        _selectedLocations = _selectedLocations.Where(id => _data.Places.Any(place => place.Id == id)).ToArray();
        RebuildPins();
        if (!_initialFitDone)
        {
            _initialFitDone = true;
            Map.Navigator.ZoomToBox(InitialBounds(_data), duration: 0);
        }
        _logger.LogDebug("Native bean map origins refreshed: mapped={Mapped}, unmapped={Unmapped}, places={Places}",
            _data.MappedBeans.Count, _data.UnmappedBeans.Count, _data.Places.Count);
        StartOriginResolution(retryFailures: false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildPins()
    {
        if (_disposed || _data == null) return;
        _origins.Features = BeanOriginClusters.Create(_data.Places, Map.Navigator.Viewport).Select(cluster =>
        {
            var feature = new PointFeature(cluster.Position);
            feature["Locations"] = cluster.Places.Select(place => place.Id).ToArray();
            feature["Country"] = string.Join(", ", cluster.Places.Select(place => place.Country.Name).Distinct().Order(StringComparer.Ordinal));
            feature.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse, SymbolScale = .65,
                Fill = new Brush(new Color(134, 84, 63)), Outline = new Pen(Color.White, 2)
            });
            feature.Styles.Add(new LabelStyle
            {
                Text = $"{cluster.Title} ({cluster.BeanCount})",
                Offset = new Offset(0, 24), Font = new Font { Size = 12 },
                ForeColor = Color.Black, BackColor = new Brush(Color.White)
            });
            return feature;
        }).ToArray();
        _origins.DataHasChanged();
    }

    private void OnViewportChanged(object? sender, ViewportChangedEventArgs args)
    {
        if (args.PreviousViewport.Resolution != args.Viewport.Resolution) RebuildPins();
    }

    public void RetryOrigins()
    {
        if (_disposed || !HasGeocodeFailure) return;
        CancelOriginResolution();
        _geocodeError = null;
        StartOriginResolution(retryFailures: true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void CancelOriginResolution()
    {
        _lookupVersion++;
        _lookupCancellation?.Cancel();
        _lookupCancellation?.Dispose();
        _lookupCancellation = null;
    }

    private void StartOriginResolution(bool retryFailures)
    {
        if (_data?.HasAmbiguousBlend == true)
        {
            _geocodeError = "A blend's detailed places could not be paired with countries. Approximate country locations remain; separate each place/country pair with a semicolon.";
            _logger.LogWarning("Native origin blend has ambiguous place/country associations");
        }
        if (_geocoder == null || _data == null || _data.Lookups.Count == 0) return;
        _lookupCancellation = new CancellationTokenSource();
        var token = _lookupCancellation.Token;
        var version = _lookupVersion;
        var lookups = _data.Lookups.ToArray();
        _ = Task.Run(() => ResolveOriginsAsync(lookups, retryFailures, version, token), token);
    }

    private async Task ResolveOriginsAsync(OriginLookup[] lookups, bool retryFailures, int version, CancellationToken token)
    {
        try
        {
            foreach (var lookup in lookups)
            {
                var result = await _geocoder!.ResolveAsync(lookup, retryFailures, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                _dispatch(() =>
                {
                    if (_disposed || token.IsCancellationRequested || version != _lookupVersion) return;
                    if (result.Location != null) _resolvedLocations[lookup.Id] = result.Location;
                    else _resolvedLocations.Remove(lookup.Id);
                    if (result.Error != null)
                        _geocodeError = string.Join(" ", new[] { _geocodeError, result.Error }.OfType<string>().Distinct());
                    _data = new BeanOriginMapData(_beans, _resolvedLocations);
                    RebuildPins();
                    Changed?.Invoke(this, EventArgs.Empty);
                    OriginsChanged?.Invoke(this, EventArgs.Empty);
                });
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _logger.LogDebug("Native detailed origin resolution cancelled");
        }
        catch (Exception error)
        {
            _logger.LogError(error, "Native detailed origin resolution failed");
            _dispatch(() =>
            {
                if (_disposed || token.IsCancellationRequested || version != _lookupVersion) return;
                _geocodeError = "Detailed origins could not load. Approximate country locations remain; use Retry origins.";
                Changed?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    internal static MRect InitialBounds(BeanOriginMapData data)
    {
        if (data.Countries.Count == 0)
            return new MRect(-20037508, -15000000, 20037508, 15000000);
        var points = data.Places.Select(place =>
            SphericalMercator.FromLonLat(place.Location.Longitude, place.Location.Latitude)).ToArray();
        var minX = points.Min(point => point.x);
        var maxX = points.Max(point => point.x);
        var minY = points.Min(point => point.y);
        var maxY = points.Max(point => point.y);
        // A minimum country-scale box also protects one point or nearby countries from street-level fitting.
        var padX = Math.Max(750000, (maxX - minX) * .18);
        var padY = Math.Max(750000, (maxY - minY) * .18);
        return new MRect(minX - padX, minY - padY, maxX + padX, maxY + padY);
    }

    public BeanMapState? CaptureState()
    {
        var viewport = Map.Navigator.Viewport;
        return viewport.Resolution > 0 && viewport.Width > 0 && viewport.Height > 0
            ? new BeanMapState(viewport.CenterX, viewport.CenterY, viewport.Resolution, viewport.Rotation,
                _initialFitDone, _data?.Places.Where(place => _selectedLocations.Contains(place.Id, StringComparer.Ordinal))
                    .Select(place => place.Country.Name).Distinct().Order(StringComparer.Ordinal).ToArray() ?? _pendingCountries,
                _data == null && _pendingCountries.Length > 0 ? null : _selectedLocations.ToArray()) : null;
    }

    public void SelectCountries(IEnumerable<string> countries)
    {
        if (_disposed || _data == null) return;
        var names = countries.ToHashSet(StringComparer.Ordinal);
        SelectLocations(_data.Places.Where(place => names.Contains(place.Country.Name)).Select(place => place.Id));
    }

    public void SelectLocations(IEnumerable<string> locations)
    {
        if (_disposed || _data == null) return;
        _selectedLocations = locations.Distinct(StringComparer.Ordinal)
            .Where(id => _data.Places.Any(place => place.Id == id)).Order(StringComparer.Ordinal).ToArray();
        Changed?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // An in-flight origin read must not replace an overview the user has started exploring.
    private void OnPointerPressed(object? sender, MapEventArgs args) => _initialFitDone = true;

    private void OnTapped(object? sender, MapEventArgs args)
    {
        if (_disposed) return;
        if (args.GestureType == GestureType.DoubleTap)
        {
            args.Handled = true;
            Map.Navigator.ZoomIn(args.ScreenPosition, duration: 200);
            return;
        }
        if (args.GestureType != GestureType.SingleTap) return;
        var locations = args.GetMapInfo([_origins]).MapInfoRecords
            .Select(record => record.Feature["Locations"]).OfType<string[]>().SelectMany(ids => ids)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (locations.Length == 0) return;
        args.Handled = true;
        SelectLocations(locations.Length == _selectedLocations.Length &&
            locations.All(id => _selectedLocations.Contains(id, StringComparer.Ordinal)) ? [] : locations);
    }

    private void OnTileDataChanged(object sender, Mapsui.Fetcher.DataChangedEventArgs args)
    {
        if (args.Error is { } error)
            ReportLoadFailure(error);
    }

    internal void ReportLoadFailure(Exception error)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _logger.LogError(error, "Native bean map tile request failed");
            if (_error != null) return;
            _error = LoadFailure;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            CancelOriginResolution();
            Changed = null;
            SelectionChanged = null;
            OriginsChanged = null;
            _tiles.DataChanged -= OnTileDataChanged;
            Map.Tapped -= OnTapped;
            Map.PointerPressed -= OnPointerPressed;
            Map.Navigator.ViewportChanged -= OnViewportChanged;
        }
        Map.Dispose();
    }
}
