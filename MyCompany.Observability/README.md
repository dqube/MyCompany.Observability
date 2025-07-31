# MyCompany.Observability

A cross-platform observability library with OpenTelemetry support for .NET applications, compatible with .NET 9.0, .NET Framework 4.6.2, and .NET Standard 2.0.

## Features

- **Multi-framework support**: .NET 9.0, .NET Framework 4.6.2, .NET Standard 2.0
- **OpenTelemetry integration**: Built-in tracing, metrics, and logging with custom instrumentation
- **Custom tracing**: Create custom activities and spans with service enrichment
- **Custom metrics**: Record counters, histograms, and gauges with automatic service tagging
- **Request/Response logging**: Comprehensive HTTP request and response logging with tracing integration
- **Data redaction**: Automatic redaction of sensitive information using System.Text.Json
- **Modern JSON processing**: Uses System.Text.Json 9.0.0 across all target frameworks
- **Smart exporter selection**: Automatically falls back to console exporter when OTLP endpoint is not configured
- **Configurable exporters**: Console and OTLP exporters with intelligent fallback
- **Web and console support**: Works with both web applications and console applications

## Installation

```bash
dotnet add package MyCompany.Observability
```

## Quick Start

### Web Applications (.NET Core, .NET 6+)

```csharp
using MyCompany.Observability.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add observability services from configuration
builder.Services.AddMyCompanyObservability(builder.Configuration);

// Or override service name/version from configuration
builder.Services.AddMyCompanyObservability(
    builder.Configuration,
    serviceName: "MyWebApi",
    serviceVersion: "1.0.0"
);

var app = builder.Build();

// Add request/response logging middleware
app.UseRequestResponseLogging();

app.Run();
```

### Console Applications

```csharp
using MyCompany.Observability.Console;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Option 1: Use programmatic configuration
var serviceProvider = ConsoleObservability.BuildConsoleObservability(options =>
{
    options.ServiceName = "MyConsoleApp";
    options.ServiceVersion = "1.0.0";
    options.EnableRequestResponseLogging = false; // Not needed for console apps
    options.LogLevel = MyCompany.Observability.Configuration.LogLevel.Information;
});

// Option 2: Use configuration from appsettings.json (.NET Core/5+)
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();
var serviceProvider = ConsoleObservability.BuildConsoleObservabilityFromConfiguration(configuration);

// Option 3: Use app.config (.NET Framework)
// var serviceProvider = ConsoleObservability.BuildConsoleObservabilityFromAppConfig();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started");
```

### .NET Framework Web Applications

```csharp
using MyCompany.Observability.Extensions;
using MyCompany.Observability.Configuration;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Option 1: Load from app.config/web.config
        var options = ConfigurationHelper.LoadFromAppConfig();
        services.AddMyCompanyObservability(opt => {
            opt.ServiceName = options.ServiceName;
            opt.ServiceVersion = options.ServiceVersion;
            opt.EnableRequestResponseLogging = options.EnableRequestResponseLogging;
            opt.EnableRedaction = options.EnableRedaction;
            // ... copy other settings
        });

        // Option 2: Programmatic configuration
        services.AddMyCompanyObservability(config =>
        {
            config.ServiceName = "MyFrameworkApp";
            config.ServiceVersion = "1.0.0";
            config.EnableRequestResponseLogging = true;
            config.EnableRedaction = true;
        });
    }
}
```

## Configuration

Configure the library through `appsettings.json`:

### appsettings.json (.NET Core/5+)

```json
{
  "Observability": {
    "ServiceName": "MyWebApplication",
    "ServiceVersion": "1.0.0",
    "ServiceNamespace": "MyCompany.Services",
    "ServiceInstanceId": "instance-001",
    "EnableRequestResponseLogging": true,
    "EnableRedaction": true,
    "LogLevel": "Information",
    "ExportBatchSize": 100,
    "ExportTimeout": "00:00:30",
    "ServiceAttributes": {
      "environment": "production",
      "team": "backend",
      "component": "api"
    },
    "Exporter": {
      "EnableConsole": true,
      "EnableOtlp": false,
      "OtlpEndpoint": "",
      "Headers": {
        "Authorization": "Bearer token123"
      }
    },
    "Redaction": {
      "SensitiveKeys": ["password", "token", "key", "secret", "authorization"],
      "RedactionText": "[REDACTED]",
      "RedactHeaders": true,
      "RedactQueryParams": true,
      "RedactRequestBody": true,
      "RedactResponseBody": true
    },
    "RequestResponseLogging": {
      "LogRequestHeaders": true,
      "LogResponseHeaders": true,
      "LogRequestBody": true,
      "LogResponseBody": true,
      "MaxBodySize": 4096,
      "ExcludePaths": ["/health", "/metrics"],
      "IncludeContentTypes": ["application/json", "application/xml", "text/plain"]
    },
    "Tracing": {
      "EnableCustomInstrumentation": true,
      "EnableHttpClientInstrumentation": true,
      "EnableHttpServerInstrumentation": true,
      "EnableSqlClientInstrumentation": false,
      "RecordException": true,
      "ActivitySources": ["MyApp.CustomSource"],
      "MaxTagCount": 128,
      "MaxEventCount": 128
    },
    "Metrics": {
      "EnableCustomMetrics": true,
      "EnableHttpClientMetrics": true,
      "EnableHttpServerMetrics": true,
      "EnableRuntimeMetrics": false,
      "MeterNames": ["MyApp.CustomMeter"],
      "MaxMetricPointsPerMetric": 2000,
      "MetricExportInterval": "00:01:00",
      "MetricExportTimeout": "00:00:30"
    }
  }
}
```

### app.config/.web.config (.NET Framework)

```xml
<configuration>
  <appSettings>
    <!-- Service Information -->
    <add key="observability:ServiceName" value="MyFrameworkApp" />
    <add key="observability:ServiceVersion" value="1.0.0" />
    <add key="observability:ServiceNamespace" value="MyCompany.Services" />
    <add key="observability:ServiceInstanceId" value="instance-001" />
    
    <!-- Basic Settings -->
    <add key="observability:EnableRequestResponseLogging" value="true" />
    <add key="observability:EnableRedaction" value="true" />
    <add key="observability:LogLevel" value="Information" />
    <add key="observability:ExportBatchSize" value="100" />
    <add key="observability:ExportTimeout" value="00:00:30" />
    
    <!-- Exporter Settings -->
    <add key="observability:Exporter:EnableConsole" value="true" />
    <add key="observability:Exporter:EnableOtlp" value="false" />
    <add key="observability:Exporter:OtlpEndpoint" value="http://localhost:4317" />
    
    <!-- Redaction Settings -->
    <add key="observability:Redaction:SensitiveKeys" value="password,token,key,secret,authorization" />
    <add key="observability:Redaction:RedactionText" value="[REDACTED]" />
    <add key="observability:Redaction:RedactHeaders" value="true" />
    
    <!-- Request/Response Logging -->
    <add key="observability:RequestResponseLogging:LogRequestHeaders" value="true" />
    <add key="observability:RequestResponseLogging:LogResponseHeaders" value="true" />
    <add key="observability:RequestResponseLogging:MaxBodySize" value="4096" />
    <add key="observability:RequestResponseLogging:ExcludePaths" value="/health,/metrics" />
    
    <!-- Service Attributes -->
    <add key="observability:ServiceAttributes:environment" value="production" />
    <add key="observability:ServiceAttributes:team" value="backend" />
  </appSettings>
</configuration>
```

## Tracing and Metrics Usage

### Custom Tracing

```csharp
using MyCompany.Observability.Services;
using System.Diagnostics;

public class MyService
{
    private readonly ITracingService _tracingService;
    private readonly ILogger<MyService> _logger;

    public MyService(ITracingService tracingService, ILogger<MyService> logger)
    {
        _tracingService = tracingService;
        _logger = logger;
    }

    public async Task<string> ProcessDataAsync(string data)
    {
        // Create a custom activity for the operation
        using var activity = _tracingService.StartActivity("ProcessData", ActivityKind.Internal);
        
        // Add custom tags
        _tracingService.AddTag(activity, "data.length", data.Length);
        _tracingService.AddTag(activity, "operation.type", "data_processing");
        
        try
        {
            // Add events during processing
            _tracingService.AddEvent(activity, "processing.started", new Dictionary<string, object?>
            {
                ["timestamp"] = DateTimeOffset.UtcNow,
                ["input.size"] = data.Length
            });

            // Simulate processing
            await Task.Delay(100);
            
            var result = $"Processed: {data}";
            
            // Record successful completion
            _tracingService.SetStatus(activity, ActivityStatusCode.Ok);
            _tracingService.AddTag(activity, "result.length", result.Length);
            
            return result;
        }
        catch (Exception ex)
        {
            // Record exceptions in traces
            _tracingService.RecordException(activity, ex);
            throw;
        }
    }

    // Using extension methods for simpler tracing
    public async Task<int> CalculateAsync(int value)
    {
        return await _tracingService.TraceAsync("Calculate", async (activity) =>
        {
            _tracingService.AddTag(activity, "input.value", value);
            await Task.Delay(50);
            var result = value * 2;
            _tracingService.AddTag(activity, "output.value", result);
            return result;
        });
    }
}
```

### Custom Metrics

```csharp
using MyCompany.Observability.Services;

public class MetricsExample
{
    private readonly IMetricsService _metricsService;

    public MetricsExample(IMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    public async Task ProcessOrderAsync(Order order)
    {
        // Increment a counter
        _metricsService.IncrementCounter("orders_processed_total", 1, new[]
        {
            new KeyValuePair<string, object?>("order.type", order.Type),
            new KeyValuePair<string, object?>("customer.tier", order.CustomerTier)
        });

        // Measure operation time using extension method
        using (_metricsService.MeasureTime("order_processing_duration", new[]
        {
            new KeyValuePair<string, object?>("order.type", order.Type)
        }))
        {
            await ProcessOrderInternalAsync(order);
        }

        // Record custom histogram values
        _metricsService.RecordHistogram("order_value_usd", order.TotalValue, new[]
        {
            new KeyValuePair<string, object?>("currency", "USD"),
            new KeyValuePair<string, object?>("customer.region", order.CustomerRegion)
        });

        // Set gauge values
        var activeOrders = GetActiveOrderCount();
        _metricsService.SetGauge("active_orders", activeOrders);

        // Record errors when they occur
        if (order.HasErrors)
        {
            _metricsService.RecordErrorCount("ValidationError", "ProcessOrder");
        }
    }

    private async Task ProcessOrderInternalAsync(Order order)
    {
        // Simulate processing time
        await Task.Delay(Random.Shared.Next(50, 200));
    }

    private int GetActiveOrderCount() => Random.Shared.Next(10, 100);
}

public class Order
{
    public string Type { get; set; } = "";
    public string CustomerTier { get; set; } = "";
    public string CustomerRegion { get; set; } = "";
    public decimal TotalValue { get; set; }
    public bool HasErrors { get; set; }
}
```

### Combined Tracing and Metrics

```csharp
public class BusinessService
{
    private readonly ITracingService _tracingService;
    private readonly IMetricsService _metricsService;
    private readonly ILogger<BusinessService> _logger;

    public BusinessService(
        ITracingService tracingService, 
        IMetricsService metricsService, 
        ILogger<BusinessService> logger)
    {
        _tracingService = tracingService;
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task<ApiResponse> HandleRequestAsync(ApiRequest request)
    {
        // Start tracing the request
        using var activity = _tracingService.StartActivity($"API {request.Method} {request.Endpoint}", ActivityKind.Server);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Add request context to trace
            _tracingService.AddTag(activity, "http.method", request.Method);
            _tracingService.AddTag(activity, "http.endpoint", request.Endpoint);
            _tracingService.AddTag(activity, "request.id", request.Id);

            // Process the request
            var response = await ProcessRequestAsync(request);
            
            // Record successful metrics
            stopwatch.Stop();
            _metricsService.RecordRequestDuration(stopwatch.Elapsed.TotalMilliseconds, 
                request.Method, request.Endpoint, response.StatusCode);
            _metricsService.IncrementRequestCount(request.Method, request.Endpoint, response.StatusCode);

            // Update trace with response info
            _tracingService.AddTag(activity, "http.status_code", response.StatusCode);
            _tracingService.SetStatus(activity, ActivityStatusCode.Ok);

            return response;
        }
        catch (Exception ex)
        {
            // Record error metrics and traces
            stopwatch.Stop();
            _metricsService.RecordRequestDuration(stopwatch.Elapsed.TotalMilliseconds, 
                request.Method, request.Endpoint, 500);
            _metricsService.IncrementRequestCount(request.Method, request.Endpoint, 500);
            _metricsService.RecordErrorCount(ex.GetType().Name, "HandleRequest");

            _tracingService.RecordException(activity, ex);
            _logger.LogError(ex, "Failed to handle request {RequestId}", request.Id);

            throw;
        }
    }

    private async Task<ApiResponse> ProcessRequestAsync(ApiRequest request)
    {
        await Task.Delay(Random.Shared.Next(10, 100));
        return new ApiResponse { StatusCode = 200, Data = "Success" };
    }
}

public class ApiRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Method { get; set; } = "";
    public string Endpoint { get; set; } = "";
}

public class ApiResponse
{
    public int StatusCode { get; set; }
    public string Data { get; set; } = "";
}
```

## Advanced Usage

### Custom Configuration

```csharp
services.AddMyCompanyObservability(options =>
{
    // Service identification
    options.ServiceName = "MyCustomService";
    options.ServiceVersion = "2.0.0";
    options.ServiceNamespace = "MyCompany.Services";
    options.ServiceInstanceId = Environment.MachineName;
    
    // Basic settings
    options.EnableRequestResponseLogging = true;
    options.EnableRedaction = true;
    options.LogLevel = MyCompany.Observability.Configuration.LogLevel.Debug;
    
    // Configure OTLP exporter
    options.Exporter.EnableOtlp = true;
    options.Exporter.OtlpEndpoint = "https://your-otlp-endpoint.com";
    options.Exporter.Headers.Add("Authorization", "Bearer your-token");
    
    // Configure redaction
    options.Redaction.SensitiveKeys.AddRange(new[] { "customSecret", "apiKey" });
    options.Redaction.RedactionText = "***HIDDEN***";
    
    // Configure request/response logging
    options.RequestResponseLogging.MaxBodySize = 8192;
    options.RequestResponseLogging.ExcludePaths.Add("/internal/health");
    
    // Add custom service attributes
    options.ServiceAttributes.Add("datacenter", "us-east-1");
    options.ServiceAttributes.Add("version.build", "12345");
    
    // Configure tracing options
    options.Tracing.EnableCustomInstrumentation = true;
    options.Tracing.EnableHttpClientInstrumentation = true;
    options.Tracing.EnableHttpServerInstrumentation = true;
    options.Tracing.RecordException = true;
    options.Tracing.MaxTagCount = 128;
    options.Tracing.MaxEventCount = 128;
    options.Tracing.ActivitySources.Add("MyApp.CustomSource");
    
    // Configure metrics options
    options.Metrics.EnableCustomMetrics = true;
    options.Metrics.EnableHttpClientMetrics = true;
    options.Metrics.EnableHttpServerMetrics = true;
    options.Metrics.MaxMetricPointsPerMetric = 2000;
    options.Metrics.MetricExportInterval = TimeSpan.FromMinutes(1);
    options.Metrics.MeterNames.Add("MyApp.CustomMeter");
});
```

### Exporter Configuration

The library provides intelligent exporter selection based on your configuration:

**Automatic Fallback Behavior:**
- If `EnableOtlp` is `true` but `OtlpEndpoint` is null or empty, the library automatically uses the console exporter
- If both `EnableConsole` and `EnableOtlp` are configured with a valid endpoint, both exporters will be used
- This ensures telemetry data is always exported somewhere, preventing data loss due to misconfiguration

**Configuration Examples:**

```csharp
// Console exporter only
services.AddMyCompanyObservability(options =>
{
    options.Exporter.EnableConsole = true;
    options.Exporter.EnableOtlp = false;
});

// OTLP exporter with fallback to console if endpoint is missing
services.AddMyCompanyObservability(options =>
{
    options.Exporter.EnableConsole = false;
    options.Exporter.EnableOtlp = true;
    options.Exporter.OtlpEndpoint = ""; // Empty endpoint will fallback to console
});

// Both exporters (dual export)
services.AddMyCompanyObservability(options =>
{
    options.Exporter.EnableConsole = true;
    options.Exporter.EnableOtlp = true;
    options.Exporter.OtlpEndpoint = "https://your-collector.com";
});
```

### Manual Redaction Service

```csharp
services.AddSingleton<IRedactionService>(provider => 
    new RedactionService(new RedactionOptions
    {
        SensitiveKeys = new List<string> { "password", "token" },
        RedactionText = "[SECURE]"
    }));

// Use the service
var redactionService = serviceProvider.GetRequiredService<IRedactionService>();
var redactedJson = redactionService.RedactSensitiveData(jsonContent, "application/json");
var redactedHeaders = redactionService.RedactHeaders(headers);
```

## Framework Compatibility

| Feature | .NET 9.0 | .NET Standard 2.0 | .NET Framework 4.6.2 |
|---------|----------|-------------------|----------------------|
| OpenTelemetry | ✅ | ✅ | ✅ |
| Custom Tracing | ✅ | ✅ | ✅ |
| Custom Metrics | ✅ | ✅ | ✅ |
| Request/Response Logging | ✅ | ✅ (Limited) | ✅ |
| Data Redaction | ✅ | ✅ | ✅ |
| System.Text.Json | ✅ | ✅ | ✅ |
| Console Logging | ✅ | ✅ | ✅ |
| OTLP Export | ✅ | ✅ | ✅ |

**Notes**: 
- .NET Standard 2.0 has limited request buffering capabilities compared to newer frameworks
- System.Text.Json 9.0.0 is used consistently across all target frameworks for optimal performance and security
- **Smart Exporter Fallback**: If OTLP is enabled but endpoint is null/empty, console exporter is automatically used as fallback

## License

This project is licensed under the MIT License.