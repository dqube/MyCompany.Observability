#if NETFRAMEWORK
using System;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyCompany.Observability.Configuration;
using MyCompany.Observability.Services;

namespace MyCompany.Observability.Extensions
{
    public static class HttpApplicationExtensions
    {
        private static ILoggerFactory _loggerFactory;

        public static void UseRequestResponseLogging(
            this HttpApplication app,
            IServiceProvider serviceProvider)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

            // Try to get logger factory from service provider first
            ILoggerFactory loggerFactory;
            try
            {
                loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                // Successfully obtained ILoggerFactory from service provider
            }
            catch (InvalidOperationException)
            {
                // Create a fallback logger factory if not available from DI
                if (_loggerFactory == null)
                {
                    _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => 
                    {
                        builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
                    });
                }
                loggerFactory = _loggerFactory;
                // ILoggerFactory not available from DI, using fallback factory
            }

            // Set the logger factory for controllers to use
            Framework.LoggerFactoryProvider.SetLoggerFactory(loggerFactory);

            var options = serviceProvider.GetRequiredService<ObservabilityOptions>();
            var redactionService = serviceProvider.GetRequiredService<IRedactionService>();
            
            // Optional services
            var tracingService = serviceProvider.GetService<ITracingService>();
            var metricsService = serviceProvider.GetService<IMetricsService>();

            ServiceCollectionExtensions.ConfigureRequestResponseLogging(
                loggerFactory, 
                options, 
                redactionService, 
                tracingService, 
                metricsService);
        }
    }
}
#endif