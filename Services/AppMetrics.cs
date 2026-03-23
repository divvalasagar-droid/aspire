using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AspireTelemetryApp.Services;

/// <summary>
/// Central place for all custom OpenTelemetry Activity Sources and Meters.
/// Register as a singleton so the same Meter instance is used everywhere.
/// </summary>
public sealed class AppMetrics : IDisposable
{
    public const string ActivitySourceName = "AspireTelemetryApp";
    public const string MeterName          = "AspireTelemetryApp";

    // Activity source for custom traces/spans
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    // Meter for custom metrics
    private readonly Meter _meter;

    // ── Custom Counters ──────────────────────────────────────────
    public Counter<long>   WeatherRequestCounter  { get; }
    public Counter<long>   ExternalApiCallCounter { get; }
    public Counter<long>   ErrorCounter           { get; }

    // ── Custom Histograms ────────────────────────────────────────
    public Histogram<double> WeatherProcessingDuration { get; }

    // ── Custom Gauge (ObservableGauge) ───────────────────────────
    private int _activeRequests;
    public void IncrementActiveRequests()  => Interlocked.Increment(ref _activeRequests);
    public void DecrementActiveRequests()  => Interlocked.Decrement(ref _activeRequests);

    public AppMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        WeatherRequestCounter = _meter.CreateCounter<long>(
            "weather.requests.total",
            unit: "{requests}",
            description: "Total number of weather forecast requests");

        ExternalApiCallCounter = _meter.CreateCounter<long>(
            "external.api.calls.total",
            unit: "{calls}",
            description: "Total external API calls made");

        ErrorCounter = _meter.CreateCounter<long>(
            "app.errors.total",
            unit: "{errors}",
            description: "Total application errors");

        WeatherProcessingDuration = _meter.CreateHistogram<double>(
            "weather.processing.duration",
            unit: "ms",
            description: "Time to process a weather forecast request");

        _meter.CreateObservableGauge(
            "app.active_requests",
            () => _activeRequests,
            unit: "{requests}",
            description: "Number of currently active requests");
    }

    public void Dispose() => _meter.Dispose();
}
