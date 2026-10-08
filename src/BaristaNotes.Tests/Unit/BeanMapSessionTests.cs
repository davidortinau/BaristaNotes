using System.Net.Http.Headers;
using BaristaNotes.Core.Services;
using Mapsui.Widgets.ButtonWidgets;
using Mapsui.Layers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Tests.Unit;

public sealed class BeanMapSessionTests
{
    [Fact]
    public void CreatesDefaultTileLayerAndEmptyOriginLayerWithoutBeanData()
    {
        using var session = new BeanMapSession(NullLogger.Instance);

        Assert.Equal(2, session.Map.Layers.Count);
        var layer = session.Map.Layers.First();
        Assert.Empty(Assert.IsType<MemoryLayer>(session.Map.Layers.Last()).Features);
        Assert.Null(session.Error);
        Assert.Equal("BaristaNotes-Native-Maps", ProductInfoHeaderValue.Parse(BeanMapSession.UserAgent).Product!.Name);
        Assert.Same(layer.Attribution,
            Assert.Single(session.Map.GetWidgetsOfMapAndLayers().OfType<HyperlinkWidget>()));
        Assert.True(layer.Attribution.Enabled);
        Assert.Equal("https://www.openstreetmap.org/copyright", layer.Attribution.Url);
        Assert.Contains("OpenStreetMap contributors", layer.Attribution.Text);
        Assert.Equal<Mapsui.Styles.Color?>(Mapsui.Styles.Color.White, layer.Attribution.BackColor);
        Assert.Equal(Mapsui.Styles.Color.Black, layer.Attribution.TextColor);
        Assert.Equal(1d, layer.Attribution.Opacity);
        Assert.True(layer.Attribution.TextSize >= 12);
    }

    [Fact]
    public void DataChangeWithoutErrorDoesNotReportFailure()
    {
        using var session = new BeanMapSession(NullLogger.Instance);
        var notifications = 0;
        session.Changed += (_, _) => notifications++;

        session.Map.Layers.First().DataHasChanged();

        Assert.Null(session.Error);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void TileFailureIsExplicitAndRemainsVisibleAfterFurtherFailures()
    {
        using var session = new BeanMapSession(NullLogger.Instance);
        var notifications = 0;
        session.Changed += (_, _) => notifications++;

        session.ReportLoadFailure(new HttpRequestException("Controlled tile failure"));
        session.ReportLoadFailure(new HttpRequestException("Another controlled tile failure"));
        session.Map.Layers.First().DataHasChanged();

        Assert.Equal(BeanMapSession.LoadFailure, session.Error);
        Assert.Equal(1, notifications);
        Assert.Equal(2, session.Map.Layers.Count);
    }

    [Fact]
    public void LateTileFailureCannotNotifyDisposedPage()
    {
        var session = new BeanMapSession(NullLogger.Instance);
        var notifications = 0;
        session.Changed += (_, _) => notifications++;

        session.Dispose();
        session.ReportLoadFailure(new HttpRequestException("Controlled late failure"));
        session.Dispose();

        Assert.Null(session.Error);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void TileFailureLogsExceptionAndStopsAfterDisposal()
    {
        var logger = new RecordingLogger();
        var session = new BeanMapSession(logger);
        var layer = session.Map.Layers.First();
        var error = new HttpRequestException("Controlled tile failure");

        session.ReportLoadFailure(error);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && ReferenceEquals(error, entry.Exception));

        session.Dispose();
        var count = logger.Entries.Count;
        layer.DataHasChanged();
        session.ReportLoadFailure(error);

        Assert.Equal(count, logger.Entries.Count);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
