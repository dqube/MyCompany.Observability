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
        public static ObservabilityOptions LoadFromAppConfig(string sectionName = "observability")
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
            if (Enum.TryParse<LogLevel>(ConfigurationManager.AppSettings[$"{sectionName}:LogLevel"], out LogLevel logLevel))
                options.LogLevel = logLevel;
            
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
            
            var redactionText = ConfigurationManager.AppSettings[$"{sectionName}:Redaction:RedactionText"];
            if (!string.IsNullOrEmpty(redactionText))
                options.Redaction.RedactionText = redactionText;
            
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
                options.RequestResponseLogging.IncludeContentTypes = includeContentTypes.Split(',', ';')
                    .Select(ct => ct.Trim())
                    .Where(ct => !string.IsNullOrEmpty(ct))
                    .ToList();
            }
            
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