using System;
using System.Web.Http;
using MyCompany.Observability.Configuration;
using MyCompany.Observability.Services;
using WebApplication1.Infrastructure;

namespace WebApplication1.App_Start
{
    public static class SimpleObservabilityConfig
    {
        private static ObservabilityOptions _options;
        private static IRedactionService _redactionService;
        private static ITracingService _tracingService;
        private static IMetricsService _metricsService;

        public static void Initialize()
        {
            try
            {
                // Load configuration from app.config
                _options = ConfigurationHelper.LoadFromAppConfig();

                // Create redaction service
                _redactionService = new RedactionService(_options.Redaction);

                // For .NET Framework, we'll use basic services without Microsoft.Extensions complexity
                // This provides basic observability without the dependency issues

                System.Diagnostics.Debug.WriteLine($"Observability initialized for service: {_options.ServiceName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize observability: {ex}");
                throw;
            }
        }

        public static ObservabilityOptions GetOptions()
        {
            return _options ?? throw new InvalidOperationException("Observability not initialized");
        }

        public static IRedactionService GetRedactionService()
        {
            return _redactionService ?? throw new InvalidOperationException("Observability not initialized");
        }

        public static ITracingService GetTracingService()
        {
            return _tracingService;
        }

        public static IMetricsService GetMetricsService()
        {
            return _metricsService;
        }

        public static ISimpleLogger GetLogger<T>()
        {
            return SimpleLoggerFactory.CreateLogger<T>();
        }

        public static ISimpleLogger GetLogger(string categoryName)
        {
            return SimpleLoggerFactory.CreateLogger(categoryName);
        }

        public static void Cleanup()
        {
            try
            {
                // Basic cleanup - no complex disposals needed
                System.Diagnostics.Debug.WriteLine("Observability cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex}");
            }
        }
    }
}