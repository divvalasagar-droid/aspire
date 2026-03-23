using AspireTelemetryApp.Models;
using AspireTelemetryApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace AspireTelemetryApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WeatherController : ControllerBase
{
    private readonly WeatherService _weatherService;
    private readonly AppMetrics _metrics;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(
        WeatherService weatherService,
        AppMetrics metrics,
        ILogger<WeatherController> logger)
    {
        _weatherService = weatherService;
        _metrics        = metrics;
        _logger         = logger;
    }

    /// <summary>Get weather forecast for a city</summary>
    /// <param name="city">City name</param>
    /// <param name="days">Number of days (1–14)</param>
    [HttpGet("{city}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WeatherForecast>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GetForecast(
        [FromRoute] string city,
        [FromQuery] int days = 5)
    {
        if (days is < 1 or > 14)
            return BadRequest(new ApiResponse<object>(false, null, "days must be between 1 and 14"));

        _metrics.IncrementActiveRequests();
        try
        {
            var forecasts = await _weatherService.GetForecastAsync(days, city);
            return Ok(new ApiResponse<IEnumerable<WeatherForecast>>(true, forecasts));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weather forecast failed for {City}", city);
            return StatusCode(500, new ApiResponse<object>(false, null, ex.Message));
        }
        finally
        {
            _metrics.DecrementActiveRequests();
        }
    }

    /// <summary>Get forecasts for multiple cities in parallel (demonstrates fan-out traces)</summary>
    [HttpPost("multi-city")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, IEnumerable<WeatherForecast>>>), 200)]
    public async Task<IActionResult> GetMultiCityForecast(
        [FromBody] string[] cities,
        [FromQuery] int days = 3)
    {
        if (cities.Length == 0)
            return BadRequest(new ApiResponse<object>(false, null, "Provide at least one city"));

        _logger.LogInformation("Multi-city forecast requested for {Cities}", string.Join(", ", cities));

        var tasks = cities.Select(async city =>
        {
            try
            {
                var forecasts = await _weatherService.GetForecastAsync(days, city);
                return (city, forecasts, error: (string?)null);
            }
            catch (Exception ex)
            {
                return (city, forecasts: Enumerable.Empty<WeatherForecast>(), error: ex.Message);
            }
        });

        var results = await Task.WhenAll(tasks);
        var dict    = results.ToDictionary(r => r.city, r => r.forecasts);

        return Ok(new ApiResponse<Dictionary<string, IEnumerable<WeatherForecast>>>(true, dict));
    }
}
