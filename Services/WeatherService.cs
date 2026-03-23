using System.Diagnostics;
using AspireTelemetryApp.Models;

namespace AspireTelemetryApp.Services;

public class WeatherService
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild",
        "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    private readonly AppMetrics _metrics;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(AppMetrics metrics, ILogger<WeatherService> logger)
    {
        _metrics = metrics;
        _logger  = logger;
    }

    public async Task<IEnumerable<WeatherForecast>> GetForecastAsync(int days, string city)
    {
        var sw = Stopwatch.StartNew();

        // ── Custom Span (child of the HTTP request span) ──────────
        using var activity = AppMetrics.ActivitySource.StartActivity("WeatherService.GetForecast");
        activity?.SetTag("weather.city", city);
        activity?.SetTag("weather.days", days);

        _logger.LogInformation("Generating {Days}-day forecast for {City}", days, city);

        // Simulate async work (e.g., DB or external call)
        await Task.Delay(Random.Shared.Next(20, 120));

        // Simulate occasional errors for demo purposes
        if (Random.Shared.Next(0, 10) == 0)
        {
            _metrics.ErrorCounter.Add(1, new KeyValuePair<string, object?>("error.type", "SimulatedError"));
            activity?.SetStatus(ActivityStatusCode.Error, "Simulated random error");
            _logger.LogWarning("Simulated error occurred for city {City}", city);
            throw new InvalidOperationException($"Simulated weather service error for {city}");
        }

        var forecasts = Enumerable.Range(1, days).Select(index =>
            new WeatherForecast(
                Date:        DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC: Random.Shared.Next(-20, 55),
                Summary:     Summaries[Random.Shared.Next(Summaries.Length)],
                City:        city
            )).ToList();

        sw.Stop();

        // ── Record custom metrics ──────────────────────────────────
        _metrics.WeatherRequestCounter.Add(1,
            new KeyValuePair<string, object?>("city", city));

        _metrics.WeatherProcessingDuration.Record(sw.ElapsedMilliseconds,
            new KeyValuePair<string, object?>("city", city),
            new KeyValuePair<string, object?>("days", days));

        activity?.SetTag("weather.result.count", forecasts.Count);
        activity?.SetStatus(ActivityStatusCode.Ok);

        _logger.LogInformation(
            "Generated {Count} forecasts for {City} in {ElapsedMs}ms",
            forecasts.Count, city, sw.ElapsedMilliseconds);

        return forecasts;
    }
}
