#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
#else
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
#endif
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using MyCompany.Observability.Configuration;
using MyCompany.Observability.Services;
using MyCompany.Observability.Instrumentation;
#if NETFRAMEWORK
using MyCompany.Observability.Framework;
#endif

namespace MyCompany.Observability.Extensions
{
    public static class ServiceCollectionExtensions
    {
#if !NETFRAMEWORK
        public static IServiceCollection AddObservability(
            this IServiceCollection services,
            IConfiguration configuration,
            string? serviceName = null,
            string? serviceVersion = null)
        {
            return services.AddObservability(config =>
            {
                configuration.GetSection("Observability").Bind(config);
                
                // Override with provided values if specified
                if (!string.IsNullOrEmpty(serviceName))
                    config.ServiceName = serviceName;
                if (!string.IsNullOrEmpty(serviceVersion))
                    config.ServiceVersion = serviceVersion;
            });
        }
#endif

        public static IServiceCollection AddObservability(
            this IServiceCollection services,
            Action<ObservabilityOptions> configureOptions)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configureOptions == null) throw new ArgumentNullException(nameof(configureOptions));

            var options = new ObservabilityOptions();
            configureOptions(options);

            // Add logging services - this ensures ILoggerFactory is available
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(options.Logging?.MinimumLevel ?? Microsoft.Extensions.Logging.LogLevel.Information);
                
                // Apply category-specific log levels
                if (options.Logging?.CategoryLevels != null)
                {
                    foreach (var categoryLevel in options.Logging.CategoryLevels)
                    {
                        builder.AddFilter(categoryLevel.Key, categoryLevel.Value);
                    }
                }
                
                // Add console logging if enabled in options
                if (options.Logging?.EnableConsoleLogging == true)
                {
#if NETFRAMEWORK
                    // For .NET Framework, add Debug provider for Visual Studio Output window with JSON formatting
                    builder.AddDebug();
                    // Also add EventLog provider for Windows Event Log (optional)
                    // builder.AddEventLog();
#else
                    builder.AddConsole(console =>
                    {
                        console.FormatterName = "json";
                    });
                    builder.AddJsonConsole();
#endif
                }
                
                // Configure structured logging to always format as JSON
                builder.Configure(options => 
                {
                    // Ensure structured logging outputs JSON format
                    options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId | ActivityTrackingOptions.ParentId;
                });
            });

            services.AddSingleton(options);
            services.AddSingleton<IRedactionService>(provider => new RedactionService(options.Redaction));

            // Register custom instrumentation sources
            services.AddSingleton<ActivitySource>(_ => InstrumentationSources.ActivitySource);
            services.AddSingleton<Meter>(_ => InstrumentationSources.Meter);

            // Register custom services
            services.AddSingleton<ITracingService>(provider => 
                new TracingService(provider.GetRequiredService<ActivitySource>(), options));
            services.AddSingleton<IMetricsService>(provider => 
                new MetricsService(provider.GetRequiredService<Meter>(), options));

#if !NETFRAMEWORK
            services.Configure<ObservabilityOptions>(configureOptions);
#endif

            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(serviceName: options.ServiceName, serviceVersion: options.ServiceVersion, serviceNamespace: options.ServiceNamespace, serviceInstanceId: options.ServiceInstanceId)
                .AddTelemetrySdk();

            // Add custom service attributes
            foreach (var attribute in options.ServiceAttributes)
            {
                resourceBuilder.AddAttributes(new[] { new KeyValuePair<string, object>(attribute.Key, attribute.Value) });
            }

            // Determine which exporters to use based on configuration
            var useConsoleExporter = options.Exporter.EnableConsole || 
                                    (options.Exporter.EnableOtlp && string.IsNullOrWhiteSpace(options.Exporter.OtlpEndpoint));
            var useOtlpExporter = options.Exporter.EnableOtlp && !string.IsNullOrWhiteSpace(options.Exporter.OtlpEndpoint);

            var openTelemetryBuilder = services.AddOpenTelemetry()
                .ConfigureResource(resource => resource = resourceBuilder);

            // Conditionally add tracing based on configuration
            if (options.Tracing.EnableCustomInstrumentation)
            {
                openTelemetryBuilder.WithTracing(tracing =>
                {
                    // Add custom activity source
                    tracing.AddSource(InstrumentationSources.ActivitySourceName);

                    // Add additional custom activity sources from configuration
                    foreach (var source in options.Tracing.ActivitySources)
                    {
                        tracing.AddSource(source);
                    }

                    // Configure tracing options
                    tracing.SetSampler(new AlwaysOnSampler());

                    // Add instrumentation based on configuration
                    if (options.Tracing.EnableHttpServerInstrumentation)
                        tracing.AddAspNetCoreInstrumentation();
                    
                    if (options.Tracing.EnableHttpClientInstrumentation)
                        tracing.AddHttpClientInstrumentation();

                    // SQL Client instrumentation can be added when the appropriate package is referenced
                    // if (options.Tracing.EnableSqlClientInstrumentation)
                    //     tracing.AddSqlClientInstrumentation();

                    if (useConsoleExporter)
                        tracing.AddConsoleExporter();

                    if (useOtlpExporter)
                        tracing.AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(options.Exporter.OtlpEndpoint);
                            foreach (var header in options.Exporter.Headers)
                            {
                                otlpOptions.Headers += $"{header.Key}={header.Value},";
                            }
                        });
                });
            }

            // Conditionally add metrics based on configuration
            if (options.Metrics.EnableCustomMetrics)
            {
                openTelemetryBuilder.WithMetrics(metrics =>
                {
                    // Add custom meter
                    metrics.AddMeter(InstrumentationSources.MeterName);

                    // Add additional custom meters from configuration
                    foreach (var meter in options.Metrics.MeterNames)
                    {
                        metrics.AddMeter(meter);
                    }

                    // Add instrumentation based on configuration
                    if (options.Metrics.EnableHttpServerMetrics)
                        metrics.AddAspNetCoreInstrumentation();
                    
                    if (options.Metrics.EnableHttpClientMetrics)
                        metrics.AddHttpClientInstrumentation();

                    // Runtime instrumentation can be added when the appropriate package is referenced
                    // if (options.Metrics.EnableRuntimeMetrics)
                    //     metrics.AddRuntimeInstrumentation();

                    if (useConsoleExporter)
                        metrics.AddConsoleExporter();

                    if (useOtlpExporter)
                        metrics.AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(options.Exporter.OtlpEndpoint);
                            foreach (var header in options.Exporter.Headers)
                            {
                                otlpOptions.Headers += $"{header.Key}={header.Value},";
                            }
                        });
                });
            }

            // Always add logging (it's typically needed for observability)
            openTelemetryBuilder.WithLogging(logging =>
            {
                if (useConsoleExporter)
                    logging.AddConsoleExporter();

                if (useOtlpExporter)
                    logging.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(options.Exporter.OtlpEndpoint);
                        foreach (var header in options.Exporter.Headers)
                        {
                            otlpOptions.Headers += $"{header.Key}={header.Value},";
                        }
                    });
            });

            return services;
        }

#if !NETFRAMEWORK
        public static IServiceCollection AddRequestResponseLogging(this IServiceCollection services)
        {
            return services;
        }
#endif

#if NETFRAMEWORK
        public static IServiceCollection AddRequestResponseLogging(this IServiceCollection services)
        {
            // For .NET Framework, HTTP module registration is handled in web.config
            // This method ensures the required services are available for the module
            return services;
        }

        public static void ConfigureRequestResponseLogging(
            ILoggerFactory loggerFactory,
            ObservabilityOptions options,
            IRedactionService redactionService,
            ITracingService tracingService = null,
            IMetricsService metricsService = null)
        {
            Framework.RequestResponseLoggingModule.Configure(
                loggerFactory, 
                options, 
                redactionService, 
                tracingService, 
                metricsService);
        }
#endif
    }
}