using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyCompany.Observability.Console;
using MyCompany.Observability.Services;
using System.Diagnostics;

var serviceProvider = ConsoleObservability.BuildConsoleObservability(options =>
{
    options.ServiceName = "TestApp";
    options.ServiceVersion = "1.0.0";
    options.EnableRequestResponseLogging = false; // Not needed for console app
    options.Exporter.EnableConsole = true;
    options.Exporter.EnableOtlp = false;
});

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
var tracingService = serviceProvider.GetRequiredService<ITracingService>();
var metricsService = serviceProvider.GetRequiredService<IMetricsService>();

logger.LogInformation("Starting test application");

// Test tracing
using (tracingService.StartActivity("TestOperation", ActivityKind.Internal))
{
    logger.LogInformation("Inside traced operation");
    
    // Test metrics
    metricsService.IncrementCounter("test_counter", 1, new[]
    {
        new KeyValuePair<string, object?>("test_tag", "test_value")
    });
    
    metricsService.RecordHistogram("operation_duration", 125.5, new[]
    {
        new KeyValuePair<string, object?>("operation", "test")
    });
    
    // Test error recording
    try
    {
        throw new InvalidOperationException("Test exception");
    }
    catch (Exception ex)
    {
        using var errorActivity = tracingService.StartActivity("ErrorHandling", ActivityKind.Internal);
        tracingService.RecordException(errorActivity, ex);
        metricsService.RecordErrorCount("InvalidOperationException", "TestOperation");
        logger.LogError(ex, "Handled test exception");
    }
}

// Test extension methods
await tracingService.TraceAsync("AsyncOperation", async (activity) =>
{
    await Task.Delay(100);
    logger.LogInformation("Async operation completed");
    metricsService.RecordHistogram("async_operation_duration", 100);
    return "Success";
});

// Test time measurement
using (metricsService.MeasureTime("timed_operation"))
{
    await Task.Delay(50);
    logger.LogInformation("Timed operation completed");
}

// Test custom instrumentation
using var customActivity = tracingService.StartActivity("CustomInstrumentation", ActivityKind.Consumer);
tracingService.AddTag(customActivity, "custom.tag", "custom_value");
tracingService.AddEvent(customActivity, "custom.event", new Dictionary<string, object?>
{
    ["event.data"] = "some data",
    ["event.timestamp"] = DateTimeOffset.UtcNow
});

logger.LogInformation("Test application completed successfully");

await Task.Delay(1000); // Give time for telemetry to be exported

// Cleanup
if (serviceProvider is IDisposable disposable)
    disposable.Dispose();