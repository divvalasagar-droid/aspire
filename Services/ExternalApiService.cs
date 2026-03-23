using System.Diagnostics;
using System.Text.Json;
using AspireTelemetryApp.Models;

namespace AspireTelemetryApp.Services;

/// <summary>
/// Demonstrates outbound HTTP calls that are auto-instrumented by
/// OpenTelemetry.Instrumentation.Http — you'll see them as child spans
/// in the Aspire Dashboard trace view.
/// </summary>
public class ExternalApiService
{
    private readonly HttpClient _httpClient;
    private readonly AppMetrics _metrics;
    private readonly ILogger<ExternalApiService> _logger;

    public ExternalApiService(
        HttpClient httpClient,
        AppMetrics metrics,
        ILogger<ExternalApiService> logger)
    {
        _httpClient = httpClient;
        _metrics    = metrics;
        _logger     = logger;
    }

    public async Task<ExternalPost?> GetPostAsync(int id)
    {
        using var activity = AppMetrics.ActivitySource.StartActivity("ExternalApiService.GetPost");
        activity?.SetTag("external.api.post_id", id);

        _logger.LogInformation("Fetching external post {PostId}", id);

        try
        {
            // HttpClient is auto-instrumented → creates a child span automatically
            var response = await _httpClient.GetAsync($"/posts/{id}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var post    = JsonSerializer.Deserialize<ExternalPost>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _metrics.ExternalApiCallCounter.Add(1,
                new KeyValuePair<string, object?>("status", "success"),
                new KeyValuePair<string, object?>("endpoint", "/posts"));

            activity?.SetTag("external.api.status", "success");
            return post;
        }
        catch (Exception ex)
        {
            _metrics.ExternalApiCallCounter.Add(1,
                new KeyValuePair<string, object?>("status", "error"),
                new KeyValuePair<string, object?>("endpoint", "/posts"));

            _metrics.ErrorCounter.Add(1,
                new KeyValuePair<string, object?>("error.type", ex.GetType().Name));

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to fetch post {PostId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<ExternalPost>> GetPostsAsync(int count = 5)
    {
        using var activity = AppMetrics.ActivitySource.StartActivity("ExternalApiService.GetPosts");
        activity?.SetTag("external.api.requested_count", count);

        _logger.LogInformation("Fetching {Count} external posts", count);

        var response = await _httpClient.GetAsync("/posts");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var posts   = JsonSerializer.Deserialize<List<ExternalPost>>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? [];

        var result = posts.Take(count).ToList();

        _metrics.ExternalApiCallCounter.Add(1,
            new KeyValuePair<string, object?>("status", "success"),
            new KeyValuePair<string, object?>("endpoint", "/posts/list"));

        activity?.SetTag("external.api.returned_count", result.Count);
        return result;
    }
}
