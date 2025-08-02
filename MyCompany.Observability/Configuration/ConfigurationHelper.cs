#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
#else
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
#endif

namespace MyCompany.Observability.Configuration
{
    public static class ConfigurationHelper
    {
#if NETFRAMEWORK
        public static ObservabilityOptions LoadFromAppSettings(string sectionName = "Observability")
        {
            var options = new ObservabilityOptions();
            
            // Load basic service information
            options.ServiceName = ConfigurationManager.AppSettings[$"{sectionName}:ServiceName"] ?? options.ServiceName;
            options.ServiceVersion = ConfigurationManager.AppSettings[$"{sectionName}:ServiceVersion"] ?? options.ServiceVersion;
            options.ServiceNamespace = ConfigurationManager.AppSettings[$"{sectionName}:ServiceNamespace"];
            options.ServiceInstanceId = ConfigurationManager.AppSettings[$"{sectionName}:ServiceInstanceId"];
            
            // Load boolean settings
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:EnableRequestResponseLogging"], out bool enableLogging))
                options.EnableRequestResponseLogging = enableLogging;
                
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:EnableRedaction"], out bool enableRedaction))
                options.EnableRedaction = enableRedaction;
            
            // Load log level
            if (Enum.TryParse<LogSeverity>(ConfigurationManager.AppSettings[$"{sectionName}:LogLevel"], out LogSeverity logLevel))
                options.LogLevel = logLevel;
            
            // Load logging configuration
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:Logging:EnableConsoleLogging"], out bool enableConsoleLogging))
                options.Logging.EnableConsoleLogging = enableConsoleLogging;
                
            if (Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(ConfigurationManager.AppSettings[$"{sectionName}:Logging:MinimumLevel"], out Microsoft.Extensions.Logging.LogLevel minimumLevel))
                options.Logging.MinimumLevel = minimumLevel;
            
            // Load export settings
            if (int.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:ExportBatchSize"], out int batchSize))
                options.ExportBatchSize = batchSize;
                
            if (TimeSpan.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:ExportTimeout"], out TimeSpan timeout))
                options.ExportTimeout = timeout;
            
            // Load exporter options
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:Exporter:EnableConsole"], out bool enableConsole))
                options.Exporter.EnableConsole = enableConsole;
                
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:Exporter:EnableOtlp"], out bool enableOtlp))
                options.Exporter.EnableOtlp = enableOtlp;
                
            var otlpEndpoint = ConfigurationManager.AppSettings[$"{sectionName}:Exporter:OtlpEndpoint"];
            if (!string.IsNullOrEmpty(otlpEndpoint))
                options.Exporter.OtlpEndpoint = otlpEndpoint;
            
            // Load redaction options
            var sensitiveKeys = ConfigurationManager.AppSettings[$"{sectionName}:Redaction:SensitiveKeys"];
            if (!string.IsNullOrEmpty(sensitiveKeys))
            {
                options.Redaction.SensitiveKeys = sensitiveKeys.Split(',', ';')
                    .Select(k => k.Trim())
                    .Where(k => !string.IsNullOrEmpty(k))
                    .ToList();
            }
            else
            {
                // Fallback to basic defaults if not configured
                options.Redaction.SensitiveKeys = new List<string> { "password", "token", "secret", "key" };
            }
            
            var redactionText = ConfigurationManager.AppSettings[$"{sectionName}:Redaction:RedactionText"];
            if (!string.IsNullOrEmpty(redactionText))
                options.Redaction.RedactionText = redactionText;
                
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:Redaction:RedactHeaders"], out bool redactHeaders))
                options.Redaction.RedactHeaders = redactHeaders;
                
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:Redaction:RedactQueryParams"], out bool redactQueryParams))
                options.Redaction.RedactQueryParams = redactQueryParams;
                
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:Redaction:RedactRequestBody"], out bool redactRequestBody))
                options.Redaction.RedactRequestBody = redactRequestBody;
                
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:Redaction:RedactResponseBody"], out bool redactResponseBody))
                options.Redaction.RedactResponseBody = redactResponseBody;
            
            // Load request/response logging options
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:RequestResponseLogging:LogRequestHeaders"], out bool logReqHeaders))
                options.RequestResponseLogging.LogRequestHeaders = logReqHeaders;
                
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:RequestResponseLogging:LogResponseHeaders"], out bool logRespHeaders))
                options.RequestResponseLogging.LogResponseHeaders = logRespHeaders;
                
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:RequestResponseLogging:LogRequestBody"], out bool logReqBody))
                options.RequestResponseLogging.LogRequestBody = logReqBody;
                
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:RequestResponseLogging:LogResponseBody"], out bool logRespBody))
                options.RequestResponseLogging.LogResponseBody = logRespBody;
                
            if (int.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:RequestResponseLogging:MaxBodySize"], out int maxBodySize))
                options.RequestResponseLogging.MaxBodySize = maxBodySize;
            
            // Load exclude paths
            var excludePaths = ConfigurationManager.AppSettings[$"{sectionName}:RequestResponseLogging:ExcludePaths"];
            if (!string.IsNullOrEmpty(excludePaths))
            {
                options.RequestResponseLogging.ExcludePaths = excludePaths.Split(',', ';')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();
            }
            
            // Load include content types
            var includeContentTypes = ConfigurationManager.AppSettings[$"{sectionName}:RequestResponseLogging:IncludeContentTypes"];
            if (!string.IsNullOrEmpty(includeContentTypes))
            {
                options.RequestResponseLogging.IncludeContentTypes.Clear();
                options.RequestResponseLogging.IncludeContentTypes.AddRange(
                    includeContentTypes.Split(',', ';')
                        .Select(ct => ct.Trim())
                        .Where(ct => !string.IsNullOrEmpty(ct))
                );
            }
            
            // Load tracing configuration
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:Tracing:EnableHttpServerInstrumentation"], out bool enableHttpServerTracing))
                options.Tracing.EnableHttpServerInstrumentation = enableHttpServerTracing;
                
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:Tracing:EnableHttpClientInstrumentation"], out bool enableHttpClientTracing))
                options.Tracing.EnableHttpClientInstrumentation = enableHttpClientTracing;
            
            // Load metrics configuration
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:Metrics:EnableHttpServerMetrics"], out bool enableHttpServerMetrics))
                options.Metrics.EnableHttpServerMetrics = enableHttpServerMetrics;
                
            if (bool.TryParse(ConfigurationManager.AppSettings[$"{sectionName}:Metrics:EnableHttpClientMetrics"], out bool enableHttpClientMetrics))
                options.Metrics.EnableHttpClientMetrics = enableHttpClientMetrics;
            
            // Load service attributes
            var attributeKeys = ConfigurationManager.AppSettings.AllKeys
                .Where(key => key.StartsWith($"{sectionName}:ServiceAttributes:", StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            foreach (var key in attributeKeys)
            {
                var attributeName = key.Substring($"{sectionName}:ServiceAttributes:".Length);
                var attributeValue = ConfigurationManager.AppSettings[key];
                if (!string.IsNullOrEmpty(attributeValue))
                {
                    options.ServiceAttributes[attributeName] = attributeValue;
                }
            }
            
            return options;
        }
#else
        public static ObservabilityOptions LoadFromConfiguration(IConfiguration configuration, string sectionName = "Observability")
        {
            var options = new ObservabilityOptions();
            configuration.GetSection(sectionName).Bind(options);
            return options;
        }
#endif

        public static ObservabilityOptions CreateDefault(string serviceName = "MyApplication", string serviceVersion = "1.0.0")
        {
            return new ObservabilityOptions
            {
                ServiceName = serviceName,
                ServiceVersion = serviceVersion
            };
        }
    }
}