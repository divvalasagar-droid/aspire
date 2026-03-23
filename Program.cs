using AspireTelemetryApp.Services;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────────
// Configuration
// ──────────────────────────────────────────────────────────────
var otlpEndpoint      = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var otlpHeaders       = builder.Configuration["OTEL_EXPORTER_OTLP_HEADERS"] ?? string.Empty;
var aiConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
var serviceName       = builder.Configuration["OTEL_SERVICE_NAME"]    ?? "AspireTelemetryApp";
var serviceVersion    = builder.Configuration["OTEL_SERVICE_VERSION"]  ?? "1.0.0";

// Which exporters are active?
var otelExport        = !string.IsNullOrEmpty(otlpEndpoint);
var appInsightsExport = !string.IsNullOrEmpty(aiConnectionString);

// ──────────────────────────────────────────────────────────────
// Shared Resource
// Applied to ALL signals (traces, metrics, logs) so every record
// in both App Insights and the Aspire Dashboard carries the same
// service name, version, and environment attributes.
// ──────────────────────────────────────────────────────────────
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName, serviceVersion: serviceVersion)
    .AddAttributes(new Dictionary<string, object>
    {
        ["deployment.environment"] = builder.Environment.EnvironmentName,
        ["cloud.provider"]         = "azure",
        ["cloud.platform"]         = "azure_app_service",
    });

// ──────────────────────────────────────────────────────────────
// OpenTelemetry — Traces + Metrics
// ──────────────────────────────────────────────────────────────
if (otelExport || appInsightsExport)
{
    var otel = builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r
            .AddService(serviceName, serviceVersion: serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName,
                ["cloud.provider"]         = "azure",
                ["cloud.platform"]         = "azure_app_service",
            }));

    // ── FIX: UseAzureMonitor handles Traces + Metrics + Logs in one call.
    // Passing the connection string explicitly is safer than relying on the
    // env-var auto-detection (which doesn't read appsettings.json).
    if (appInsightsExport)
        otel.UseAzureMonitor(o => o.ConnectionString = aiConnectionString);

    otel.WithTracing(t =>
        {
            t.AddAspNetCoreInstrumentation(o => o.RecordException = true)
             .AddHttpClientInstrumentation()
             .AddEntityFrameworkCoreInstrumentation()
             // FIX: Register custom ActivitySource so spans from AppMetrics
             // are NOT silently dropped by the OTel SDK sampler.
             .AddSource(AppMetrics.ActivitySourceName);

            if (otelExport)
            {
                t.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(otlpEndpoint!);
                    otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    if (!string.IsNullOrEmpty(otlpHeaders))
                        otlp.Headers = otlpHeaders;
                });
            }
        })
        .WithMetrics(m =>
        {
            m.AddAspNetCoreInstrumentation()
             .AddHttpClientInstrumentation()
             .AddRuntimeInstrumentation()
             // Register the custom Meter so AppMetrics counters & histograms are exported
             .AddMeter(AppMetrics.MeterName);

            if (otelExport)
            {
                m.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(otlpEndpoint!);
                    otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    if (!string.IsNullOrEmpty(otlpHeaders))
                        otlp.Headers = otlpHeaders;
                });
            }
        });

    // ── FIX: Logging pipeline must set the ResourceBuilder explicitly.
    // builder.Logging.AddOpenTelemetry() is a separate pipeline from
    // builder.Services.AddOpenTelemetry() and does NOT inherit its resource,
    // so logs would appear in App Insights without service-name attribution.
    // UseAzureMonitor (above) already wires App Insights log export,
    // so we only add OTLP here if that exporter is configured.
    builder.Logging.AddOpenTelemetry(l =>
    {
        l.SetResourceBuilder(resourceBuilder); // FIX: explicit resource
        l.IncludeScopes           = true;
        l.IncludeFormattedMessage = true;

        if (otelExport)
        {
            l.AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new Uri(otlpEndpoint!);
                otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                if (!string.IsNullOrEmpty(otlpHeaders))
                    otlp.Headers = otlpHeaders;
            });
        }
        // Console exporter removed: not available in OpenTelemetry 1.12.0
        // Azure Monitor already exports logs to Application Insights
    });
}

// ──────────────────────────────────────────────────────────────
// Application Services
// ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<AppMetrics>();
builder.Services.AddScoped<WeatherService>();
builder.Services.AddHttpClient<ExternalApiService>(client =>
{
    client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com");
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Aspire Telemetry Demo API",
        Version     = "v1",
        Description = "Sample .NET 9 Web API — OpenTelemetry → App Insights + Aspire Dashboard"
    });
});

// ──────────────────────────────────────────────────────────────
// Health Checks (Azure uses these for slot swaps / autoscale)
// ──────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aspire Telemetry Demo v1");
    c.RoutePrefix = string.Empty;
});

app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health");

app.Logger.LogInformation(
    "🚀 AspireTelemetryApp starting | AppInsights={AI} | OTLP={OTLP}",
    appInsightsExport, otelExport);

app.Run();
