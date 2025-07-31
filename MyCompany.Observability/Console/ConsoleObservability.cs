#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
#else
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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

namespace MyCompany.Observability.Console
{
    public static class ConsoleObservability
    {
        public static IServiceCollection ConfigureConsoleObservability(
#if NETFRAMEWORK
            Action<ObservabilityOptions> configureOptions = null)
#else
            Action<ObservabilityOptions>? configureOptions = null)
#endif
        {
            var services = new ServiceCollection();

            var options = new ObservabilityOptions();
            configureOptions?.Invoke(options);

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

            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel((Microsoft.Extensions.Logging.LogLevel)options.LogLevel);
            });

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

            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource = resourceBuilder)
                .WithTracing(tracing =>
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

                    if (options.Tracing.EnableHttpClientInstrumentation)
                        tracing.AddHttpClientInstrumentation();

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
                })
                .WithMetrics(metrics =>
                {
                    // Add custom meter
                    metrics.AddMeter(InstrumentationSources.MeterName);

                    // Add additional custom meters from configuration
                    foreach (var meter in options.Metrics.MeterNames)
                    {
                        metrics.AddMeter(meter);
                    }

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
                })
                .WithLogging(logging =>
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

        public static IServiceProvider BuildConsoleObservability(
#if NETFRAMEWORK
            Action<ObservabilityOptions> configureOptions = null)
#else
            Action<ObservabilityOptions>? configureOptions = null)
#endif
        {
            var services = ConfigureConsoleObservability(configureOptions);
            return services.BuildServiceProvider();
        }

#if NETFRAMEWORK
        public static IServiceProvider BuildConsoleObservabilityFromAppConfig(string sectionName = "observability")
        {
            var options = ConfigurationHelper.LoadFromAppSettings(sectionName);
            return BuildConsoleObservability(opt => 
            {
                opt.ServiceName = options.ServiceName;
                opt.ServiceVersion = options.ServiceVersion;
                opt.ServiceNamespace = options.ServiceNamespace;
                opt.ServiceInstanceId = options.ServiceInstanceId;
                opt.EnableRequestResponseLogging = options.EnableRequestResponseLogging;
                opt.EnableRedaction = options.EnableRedaction;
                opt.LogLevel = options.LogLevel;
                opt.ExportBatchSize = options.ExportBatchSize;
                opt.ExportTimeout = options.ExportTimeout;
                opt.Exporter = options.Exporter;
                opt.Redaction = options.Redaction;
                opt.RequestResponseLogging = options.RequestResponseLogging;
                opt.ServiceAttributes = options.ServiceAttributes;
            });
        }
#else
        public static IServiceProvider BuildConsoleObservabilityFromConfiguration(IConfiguration configuration, string sectionName = "Observability")
        {
            var options = ConfigurationHelper.LoadFromConfiguration(configuration, sectionName);
            return BuildConsoleObservability(opt => 
            {
                opt.ServiceName = options.ServiceName;
                opt.ServiceVersion = options.ServiceVersion;
                opt.ServiceNamespace = options.ServiceNamespace;
                opt.ServiceInstanceId = options.ServiceInstanceId;
                opt.EnableRequestResponseLogging = options.EnableRequestResponseLogging;
                opt.EnableRedaction = options.EnableRedaction;
                opt.LogLevel = options.LogLevel;
                opt.ExportBatchSize = options.ExportBatchSize;
                opt.ExportTimeout = options.ExportTimeout;
                opt.Exporter = options.Exporter;
                opt.Redaction = options.Redaction;
                opt.RequestResponseLogging = options.RequestResponseLogging;
                opt.ServiceAttributes = options.ServiceAttributes;
            });
        }
#endif

#if !NETFRAMEWORK
        public static IHostBuilder ConfigureConsoleObservabilityHost(
            this IHostBuilder hostBuilder,
            Action<ObservabilityOptions>? configureOptions = null)
        {
            return hostBuilder.ConfigureServices((context, services) =>
            {
                var observabilityServices = ConfigureConsoleObservability(configureOptions);
                foreach (var service in observabilityServices)
                {
                    services.Add(service);
                }
            });
        }

        public static IHostBuilder ConfigureConsoleObservabilityHostFromConfiguration(
            this IHostBuilder hostBuilder,
            string sectionName = "Observability")
        {
            return hostBuilder.ConfigureServices((context, services) =>
            {
                var options = ConfigurationHelper.LoadFromConfiguration(context.Configuration, sectionName);
                var observabilityServices = ConfigureConsoleObservability(opt => 
                {
                    opt.ServiceName = options.ServiceName;
                    opt.ServiceVersion = options.ServiceVersion;
                    opt.ServiceNamespace = options.ServiceNamespace;
                    opt.ServiceInstanceId = options.ServiceInstanceId;
                    opt.EnableRequestResponseLogging = options.EnableRequestResponseLogging;
                    opt.EnableRedaction = options.EnableRedaction;
                    opt.LogLevel = options.LogLevel;
                    opt.ExportBatchSize = options.ExportBatchSize;
                    opt.ExportTimeout = options.ExportTimeout;
                    opt.Exporter = options.Exporter;
                    opt.Redaction = options.Redaction;
                    opt.RequestResponseLogging = options.RequestResponseLogging;
                    opt.ServiceAttributes = options.ServiceAttributes;
                });
                
                foreach (var service in observabilityServices)
                {
                    services.Add(service);
                }
            });
        }
#endif
    }
}