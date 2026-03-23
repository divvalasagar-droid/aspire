using Microsoft.AspNetCore.Mvc;

namespace AspireTelemetryApp.Controllers;

/// <summary>
/// Simple diagnostics endpoint — useful for checking the deployment on Azure.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DiagnosticsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        IConfiguration config,
        IWebHostEnvironment env,
        ILogger<DiagnosticsController> logger)
    {
        _config = config;
        _env    = env;
        _logger = logger;
    }

    /// <summary>Returns runtime info and OTLP configuration (safe values only)</summary>
    [HttpGet]
    public IActionResult GetDiagnostics()
    {
        _logger.LogInformation("Diagnostics requested");

        var otlpEndpoint = _config["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "not set";
        var serviceName  = _config["OTEL_SERVICE_NAME"]            ?? "not set";

        return Ok(new
        {
            Status          = "healthy",
            Timestamp       = DateTime.UtcNow,
            Environment     = _env.EnvironmentName,
            DotNetVersion   = Environment.Version.ToString(),
            MachineName     = Environment.MachineName,
            Telemetry = new
            {
                OtlpEndpoint = otlpEndpoint,
                ServiceName  = serviceName,
                OtlpHeaders  = string.IsNullOrEmpty(_config["OTEL_EXPORTER_OTLP_HEADERS"])
                               ? "not set"
                               : "configured (value hidden)",
            }
        });
    }

    /// <summary>Generate a test log entry at each severity level</summary>
    [HttpPost("test-logs")]
    public IActionResult TestLogs()
    {
        _logger.LogTrace("TRACE: This is a trace message");
        _logger.LogDebug("DEBUG: This is a debug message");
        _logger.LogInformation("INFO: Test log entry generated at {Time}", DateTime.UtcNow);
        _logger.LogWarning("WARN: This is a warning message");
        _logger.LogError("ERROR: This is a simulated error message");

        return Ok(new { Message = "Log entries written — check Aspire Dashboard → Logs" });
    }

    /// <summary>Trigger an unhandled exception to see error traces in Aspire Dashboard</summary>
    [HttpPost("test-error")]
    public IActionResult TestError()
    {
        _logger.LogWarning("About to throw a test exception");
        throw new InvalidOperationException("This is a deliberate test exception for telemetry demo");
    }
}
