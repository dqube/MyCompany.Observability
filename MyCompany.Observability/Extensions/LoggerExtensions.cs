using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MyCompany.Observability.Services;

#if NET462
namespace MyCompany.Observability.Extensions
{
#else
namespace MyCompany.Observability.Extensions;
#endif

/// <summary>
/// Logger extension methods that provide object serialization and redaction capabilities
/// </summary>
public static class LoggerExtensions
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Logs an information message with object serialization and redaction using {@Object} syntax
    /// </summary>
    public static void LogInformation<T>(this ILogger logger, string template, T obj) where T : class
    {
        // Only intercept if the template contains {@...} destructuring syntax
        if (logger.IsEnabled(LogLevel.Information) && template.Contains("{@"))
        {
            var serializedObj = SerializeAndRedactObject(logger, obj);
            var processedTemplate = template.Replace("{@", "{");
            // Use Log method directly to avoid recursion
            logger.Log(LogLevel.Information, processedTemplate, serializedObj);
        }
        else
        {
            // Use Log method directly to avoid calling our own extension
            logger.Log(LogLevel.Information, template, obj);
        }
    }

    /// <summary>
    /// Logs a debug message with object serialization and redaction
    /// </summary>
    public static void LogDebug<T>(this ILogger logger, string message, T obj) where T : class
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            var serializedObj = SerializeAndRedactObject(logger, obj);
            logger.Log(LogLevel.Debug, message, serializedObj);
        }
    }

    /// <summary>
    /// Logs a warning message with object serialization and redaction
    /// </summary>
    public static void LogWarning<T>(this ILogger logger, string message, T obj) where T : class
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            var serializedObj = SerializeAndRedactObject(logger, obj);
            logger.Log(LogLevel.Warning, message, serializedObj);
        }
    }

    /// <summary>
    /// Logs an error message with object serialization and redaction
    /// </summary>
    public static void LogError<T>(this ILogger logger, string message, T obj) where T : class
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            var serializedObj = SerializeAndRedactObject(logger, obj);
            logger.Log(LogLevel.Error, message, serializedObj);
        }
    }

    /// <summary>
    /// Logs an error message with exception and object serialization and redaction
    /// </summary>
    public static void LogError<T>(this ILogger logger, Exception exception, string message, T obj) where T : class
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            var serializedObj = SerializeAndRedactObject(logger, obj);
            logger.LogError(exception, message, serializedObj);
        }
    }

    /// <summary>
    /// Logs a critical message with object serialization and redaction
    /// </summary>
    public static void LogCritical<T>(this ILogger logger, string message, T obj) where T : class
    {
        if (logger.IsEnabled(LogLevel.Critical))
        {
            var serializedObj = SerializeAndRedactObject(logger, obj);
            logger.LogCritical(message, serializedObj);
        }
    }

    /// <summary>
    /// Logs a critical message with exception and object serialization and redaction
    /// </summary>
    public static void LogCritical<T>(this ILogger logger, Exception exception, string message, T obj) where T : class
    {
        if (logger.IsEnabled(LogLevel.Critical))
        {
            var serializedObj = SerializeAndRedactObject(logger, obj);
            logger.LogCritical(exception, message, serializedObj);
        }
    }

    private static string SerializeAndRedactObject<T>(ILogger logger, T obj) where T : class
    {
        try
        {
            // Serialize the object to JSON
            var json = JsonSerializer.Serialize(obj, DefaultJsonOptions);
            
            // Try to get the service provider from the logger and use the actual RedactionService
            var serviceProvider = GetServiceProvider(logger);
            if (serviceProvider != null)
            {
                var redactionService = serviceProvider.GetService<IRedactionService>();
                if (redactionService != null)
                {
                    return redactionService.RedactSensitiveData(json, "application/json");
                }
            }
            
            // Fallback to basic redaction if service provider not available
            return ApplyBasicRedaction(json);
        }
        catch (Exception ex)
        {
            return $"[Serialization Error: {ex.Message}]";
        }
    }

    private static IServiceProvider? GetServiceProvider(ILogger logger)
    {
        try
        {
            var loggerType = logger.GetType();
            
            // Method 1: Check if logger has a ServiceProvider property
            var serviceProviderProperty = loggerType.GetProperty("ServiceProvider", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (serviceProviderProperty != null)
            {
                return serviceProviderProperty.GetValue(logger) as IServiceProvider;
            }

            // Method 2: Check if logger has a Services property
            var servicesProperty = loggerType.GetProperty("Services", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (servicesProperty != null)
            {
                return servicesProperty.GetValue(logger) as IServiceProvider;
            }

            // Method 3: Check if logger implements IServiceProvider directly
            if (logger is IServiceProvider directServiceProvider)
            {
                return directServiceProvider;
            }

            // Method 4: Try to get it from private fields
            var serviceProviderField = loggerType.GetField("_serviceProvider", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) ??
                loggerType.GetField("_services", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (serviceProviderField != null)
            {
                return serviceProviderField.GetValue(logger) as IServiceProvider;
            }

            return null;
        }
        catch
        {
            // If reflection fails, return null and use fallback redaction
            return null;
        }
    }

    private static string ApplyBasicRedaction(string json)
    {
        // Apply basic redaction for common sensitive fields as fallback
        var sensitiveFields = new[] { "password", "token", "secret", "key", "apikey", "authorization" };
        
        foreach (var field in sensitiveFields)
        {
            var pattern = $"\"{field}\":\"[^\"]*\"";
            json = System.Text.RegularExpressions.Regex.Replace(json, pattern, $"\"{field}\":\"[REDACTED]\"", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return json;
    }
}

#if NET462
}
#endif