#if NETFRAMEWORK
using System;
using System.Collections.Generic;
#else
using System;
using System.Collections.Generic;
#endif

namespace MyCompany.Observability.Configuration
{
    public class ObservabilityOptions
    {
        public string ServiceName { get; set; } = "MyApplication";
        public string ServiceVersion { get; set; } = "1.0.0";
#if NETFRAMEWORK
        public string? ServiceNamespace { get; set; }
        public string? ServiceInstanceId { get; set; }
#else
        public string? ServiceNamespace { get; set; }
        public string? ServiceInstanceId { get; set; }
#endif
        public bool EnableRequestResponseLogging { get; set; } = true;
        public bool EnableRedaction { get; set; } = true;
        public LogSeverity LogLevel { get; set; } = LogSeverity.Information;
        public int ExportBatchSize { get; set; } = 100;
        public TimeSpan ExportTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public ExporterOptions Exporter { get; set; } = new ExporterOptions();
        public RedactionOptions Redaction { get; set; } = new RedactionOptions();
        public RequestResponseLoggingOptions RequestResponseLogging { get; set; } = new RequestResponseLoggingOptions();
        public TracingOptions Tracing { get; set; } = new TracingOptions();
        public MetricsOptions Metrics { get; set; } = new MetricsOptions();
        public LoggingOptions Logging { get; set; } = new LoggingOptions();
        public Dictionary<string, string> ServiceAttributes { get; set; } = new Dictionary<string, string>();
    }

    public class ExporterOptions
    {
        public bool EnableConsole { get; set; } = true;
        public bool EnableOtlp { get; set; } = false;
        public string OtlpEndpoint { get; set; } = "http://localhost:4317";
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    }

    public class RedactionOptions
    {
        public List<string> SensitiveKeys { get; set; } = new List<string>
        {
            "password", "token", "key", "secret", "authorization", "api-key", "x-api-key"
        };
        public string RedactionText { get; set; } = "[REDACTED]";
        public bool RedactHeaders { get; set; } = true;
        public bool RedactQueryParams { get; set; } = true;
        public bool RedactRequestBody { get; set; } = true;
        public bool RedactResponseBody { get; set; } = true;
    }

    public class RequestResponseLoggingOptions
    {
        public bool LogRequestHeaders { get; set; } = true;
        public bool LogResponseHeaders { get; set; } = true;
        public bool LogRequestBody { get; set; } = true;
        public bool LogResponseBody { get; set; } = true;
        public int MaxBodySize { get; set; } = 4096;
        public List<string> ExcludePaths { get; set; } = new List<string> { "/health", "/metrics" };
        public List<string> IncludeContentTypes { get; set; } = new List<string> 
        { 
            "application/json", 
            "application/xml", 
            "text/plain", 
            "text/xml" 
        };
    }

    public class TracingOptions
    {
        public bool EnableCustomInstrumentation { get; set; } = true;
        public bool EnableHttpClientInstrumentation { get; set; } = true;
        public bool EnableHttpServerInstrumentation { get; set; } = true;
        public bool EnableSqlClientInstrumentation { get; set; } = true;
        public bool RecordException { get; set; } = true;
        public List<string> ActivitySources { get; set; } = new List<string>();
        public int MaxTagValueLength { get; set; } = 1024;
        public int MaxEventCount { get; set; } = 128;
        public int MaxLinkCount { get; set; } = 128;
        public int MaxTagCount { get; set; } = 128;
    }

    public class MetricsOptions
    {
        public bool EnableCustomMetrics { get; set; } = true;
        public bool EnableHttpClientMetrics { get; set; } = true;
        public bool EnableHttpServerMetrics { get; set; } = true;
        public bool EnableRuntimeMetrics { get; set; } = true;
        public List<string> MeterNames { get; set; } = new List<string>();
        public int MaxMetricPointsPerMetric { get; set; } = 2000;
        public TimeSpan MetricExportInterval { get; set; } = TimeSpan.FromMinutes(1);
        public TimeSpan MetricExportTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    public class LoggingOptions
    {
        public bool EnableConsoleLogging { get; set; } = true;
        public Microsoft.Extensions.Logging.LogLevel MinimumLevel { get; set; } = Microsoft.Extensions.Logging.LogLevel.Information;
        public Dictionary<string, Microsoft.Extensions.Logging.LogLevel> CategoryLevels { get; set; } = new Dictionary<string, Microsoft.Extensions.Logging.LogLevel>();
    }

    public enum LogSeverity
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5,
        None = 6
    }
}