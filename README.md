# AspireTelemetryApp

Sample .NET 9 Web API that sends **Traces, Metrics, and Logs** to the
**.NET Aspire Dashboard** running as a Docker container, via OTLP/gRPC.

## Project Structure

```
AspireTelemetryApp/
├── Controllers/
│   ├── WeatherController.cs     # Weather forecast (custom traces + metrics)
│   ├── PostsController.cs       # External HTTP calls (auto-instrumented)
│   └── DiagnosticsController.cs # Health info + test log/error endpoints
├── Models/
│   └── Models.cs
├── Services/
│   ├── AppMetrics.cs            # Custom Meter + ActivitySource
│   ├── WeatherService.cs
│   └── ExternalApiService.cs
├── Program.cs                   # OTel setup
├── Dockerfile
├── docker-compose.yml           # Local dev with Aspire Dashboard
└── .github/workflows/deploy.yml # GitHub Actions → Azure
```

## Run Locally (Docker)

```bash
docker compose up --build
```

| URL | Description |
|-----|-------------|
| http://localhost:8080 | Swagger UI |
| http://localhost:8080/health | Health check |
| http://localhost:18888 | Aspire Dashboard |

## Azure App Service — Required App Settings

Set **either or both** exporters — the app activates only what has a value:

### Application Insights (Azure-native, recommended)

| Name | Value |
|------|-------|
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | `InstrumentationKey=xxx;IngestionEndpoint=https://...` |
| `OTEL_SERVICE_NAME` | `AspireTelemetryApp` |
| `OTEL_SERVICE_VERSION` | `1.0.0` |

> Get the connection string from: **Application Insights → Overview → Connection String**

### Aspire Dashboard / Any OTLP backend (optional)

| Name | Value |
|------|-------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://<docker-host-ip>:18889` |
| `OTEL_EXPORTER_OTLP_HEADERS` | `x-otlp-api-key=your-key` *(optional)* |

## Key Fixes Applied (vs. naive implementation)

| Fix | Why it matters |
|-----|---------------|
| `UseAzureMonitor()` instead of 3 separate exporters | Single call covers Traces + Metrics + Logs; explicit connection string works from any config source |
| `l.SetResourceBuilder(resourceBuilder)` on logging | Logging pipeline is separate from the metrics/traces pipeline — without this, logs arrive in App Insights with no service name |
| `.AddSource(AppMetrics.ActivitySourceName)` in tracing | Custom `Activity` spans are silently dropped by the OTel SDK unless the source is registered |
| `AddMeter(AppMetrics.MeterName)` in metrics | Custom counters and histograms are ignored unless the Meter is registered |

## Deploy to Azure

```bash
# Publish
dotnet publish -c Release -o ./publish

# Deploy with Azure CLI
az webapp deploy \
  --resource-group <rg> \
  --name <webapp-name> \
  --src-path ./publish \
  --type zip
```

Or push to `main` to trigger the GitHub Actions workflow.

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/weather/{city}?days=5` | Weather forecast (custom traces) |
| POST | `/api/weather/multi-city` | Fan-out multi-city forecast |
| GET | `/api/posts/{id}` | External HTTP call (auto-traced) |
| GET | `/api/posts?count=5` | Fetch multiple posts |
| GET | `/api/diagnostics` | App + OTLP config info |
| POST | `/api/diagnostics/test-logs` | Write test log entries |
| POST | `/api/diagnostics/test-error` | Throw a test exception |
| GET | `/health` | Health check |
