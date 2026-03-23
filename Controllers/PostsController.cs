using AspireTelemetryApp.Models;
using AspireTelemetryApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace AspireTelemetryApp.Controllers;

/// <summary>
/// Demonstrates outbound HTTP calls tracked in Aspire Dashboard traces.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PostsController : ControllerBase
{
    private readonly ExternalApiService _externalApi;
    private readonly ILogger<PostsController> _logger;

    public PostsController(ExternalApiService externalApi, ILogger<PostsController> logger)
    {
        _externalApi = externalApi;
        _logger      = logger;
    }

    /// <summary>Fetch a single post from an external API (shows distributed trace)</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ExternalPost>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GetPost([FromRoute] int id)
    {
        try
        {
            var post = await _externalApi.GetPostAsync(id);
            return Ok(new ApiResponse<ExternalPost>(true, post));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get post {Id}", id);
            return StatusCode(500, new ApiResponse<object>(false, null, ex.Message));
        }
    }

    /// <summary>Fetch multiple posts (shows bulk outbound HTTP traces)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ExternalPost>>), 200)]
    public async Task<IActionResult> GetPosts([FromQuery] int count = 5)
    {
        var posts = await _externalApi.GetPostsAsync(count);
        return Ok(new ApiResponse<IEnumerable<ExternalPost>>(true, posts));
    }
}
