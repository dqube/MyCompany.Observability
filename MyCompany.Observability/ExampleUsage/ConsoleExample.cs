// Example usage for Console Applications
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyCompany.Observability.Console;
using MyCompany.Observability.Configuration;

namespace MyCompany.Observability.Examples
{
    public class ConsoleExample
    {
        public static async Task Main(string[] args)
        {
            // Configure observability for console application
            var serviceProvider = ConsoleObservability.BuildConsoleObservability(
                serviceName: "MyConsoleApp",
                serviceVersion: "1.0.0",
                configureOptions: options =>
                {
                    options.EnableRequestResponseLogging = false; // Not needed for console
                    options.EnableRedaction = true;
                    options.LogLevel = LogLevel.Information;
                    
                    options.Exporter.EnableConsole = true;
                    options.Exporter.EnableOtlp = false;
                }
            );

            var logger = serviceProvider.GetRequiredService<ILogger<ConsoleExample>>();
            
            logger.LogInformation("Console application started");
            
            // Simulate some work
            for (int i = 0; i < 5; i++)
            {
                logger.LogInformation("Processing item {ItemNumber}", i + 1);
                await Task.Delay(1000);
            }
            
            logger.LogInformation("Console application completed");
            
            // Dispose the service provider to flush telemetry
            if (serviceProvider is IDisposable disposable)
                disposable.Dispose();
        }
    }

#if !NET462
    // Example using .NET Generic Host
    public class HostedConsoleExample
    {
        public static async Task Main(string[] args)
        {
            var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .ConfigureConsoleObservabilityHost(
                    serviceName: "MyHostedConsoleApp",
                    serviceVersion: "1.0.0",
                    configureOptions: options =>
                    {
                        options.LogLevel = LogLevel.Debug;
                        options.Exporter.EnableConsole = true;
                    })
                .ConfigureServices(services =>
                {
                    services.AddHostedService<WorkerService>();
                });

            var host = hostBuilder.Build();
            await host.RunAsync();
        }
    }

    public class WorkerService : Microsoft.Extensions.Hosting.BackgroundService
    {
        private readonly ILogger<WorkerService> _logger;

        public WorkerService(ILogger<WorkerService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
#endif
}