using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MyCompany.Observability.Services;
#if NETFRAMEWORK
using System.Web;
#else
using Microsoft.AspNetCore.Http;
#endif

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
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
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
    /// Logs an information message with multiple objects that are automatically serialized and redacted
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="message">The message template</param>
    /// <param name="args">Arguments to serialize and include in the log</param>
    public static void LogInfo(this ILogger logger, string message, params object[] args)
    {
        if (!logger.IsEnabled(LogLevel.Information) || args == null || args.Length == 0)
        {
            logger.Log(LogLevel.Information, message);
            return;
        }

        try
        {
            // Process arguments for serialization and redaction
            var processedArgs = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != null)
                {
                    // Check if this should be serialized (complex object) or used as-is (primitive)
                    if (ShouldSerialize(args[i]))
                    {
                        // Use the same approach as SerializeAndRedactObject
                        processedArgs[i] = SerializeAndRedactObject(logger, args[i]);
                    }
                    else
                    {
                        processedArgs[i] = args[i];
                    }
                }
                else
                {
                    processedArgs[i] = null;
                }
            }

            logger.Log(LogLevel.Information, message, processedArgs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing logged objects");
            // Fallback to original logging without processing
            logger.Log(LogLevel.Information, message, args);
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

    private static bool ShouldSerialize(object obj)
    {
        if (obj == null) return false;
        
        var type = obj.GetType();
        
        // Don't serialize primitive types, strings, dates, etc.
        if (type.IsPrimitive || 
            type == typeof(string) || 
            type == typeof(DateTime) || 
            type == typeof(DateTimeOffset) || 
            type == typeof(TimeSpan) || 
            type == typeof(Guid) || 
            type == typeof(decimal) ||
            type.IsEnum)
        {
            return false;
        }
        
        // Serialize complex objects, arrays, collections, anonymous types, etc.
        return true;
    }

    private static string SerializeAndRedact(object obj, IRedactionService? redactionService)
    {
        try
        {
            var json = JsonSerializer.Serialize(obj, DefaultJsonOptions);
            return redactionService?.RedactSensitiveData(json, "application/json") ?? json;
        }
        catch (Exception ex)
        {
            return $"[Serialization Error: {ex.Message}]";
        }
    }

    private static string SerializeAndRedactObject(ILogger logger, object obj)
    {
        try
        {
            // Serialize the object to JSON
            var json = JsonSerializer.Serialize(obj, DefaultJsonOptions);
            
            // Try to get the RedactionService from the global service provider accessor
            var redactionService = ServiceProviderAccessor.GetRedactionService();
            
            if (redactionService != null)
            {
                return redactionService.RedactSensitiveData(json, "application/json");
            }
            else
            {
                // Fallback to basic redaction if service provider not available
                return ApplyBasicRedaction(json);
            }
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
        var sensitiveFields = new[] { "username", "password", "token", "secret", "key", "apikey", "authorization", "auth", "credentials" };
        
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