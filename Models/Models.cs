namespace AspireTelemetryApp.Models;

public record WeatherForecast(
    DateOnly Date,
    int TemperatureC,
    string? Summary,
    string City)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public class ExternalPost
{
    public int    UserId { get; set; }
    public int    Id     { get; set; }
    public string Title  { get; set; } = string.Empty;
    public string Body   { get; set; } = string.Empty;
}

public record ApiResponse<T>(bool Success, T? Data, string? Error = null);
