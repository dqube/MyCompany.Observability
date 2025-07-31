#if NETFRAMEWORK
using System;
using MyCompany.Observability.Configuration;
using MyCompany.Observability.Services;

namespace MyCompany.Observability.Framework
{
    /// <summary>
    /// Generic observability configuration manager for .NET Framework applications.
    /// Provides a simplified API for initializing and accessing observability services without Microsoft.Extensions.DependencyInjection complexity.
    /// </summary>
    public static class FrameworkObservabilityConfig
    {
        private static ObservabilityOptions? _options;
        private static IRedactionService? _redactionService;
        private static ITracingService? _tracingService;
        private static IMetricsService? _metricsService;
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Initialize observability services with configuration loaded from app.config/web.config
        /// </summary>
        public static void Initialize()
        {
            lock (_lockObject)
            {
                if (_isInitialized)
                    return;

                try
                {
                    // Load configuration from app.config/web.config
                    _options = ConfigurationHelper.LoadFromAppConfig();

                    // Create core services
                    _redactionService = new RedactionService(_options.Redaction);

                    // Initialize tracing and metrics services
                    _tracingService = new TracingService(Instrumentation.InstrumentationSources.ActivitySource, _options);
                    _metricsService = new MetricsService(Instrumentation.InstrumentationSources.Meter, _options);

                    _isInitialized = true;

                    System.Diagnostics.Debug.WriteLine($"Framework observability initialized for service: {_options.ServiceName}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize framework observability: {ex}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Initialize observability services with custom configuration
        /// </summary>
        /// <param name="configureOptions">Action to configure observability options</param>
        public static void Initialize(Action<ObservabilityOptions> configureOptions)
        {
            lock (_lockObject)
            {
                if (_isInitialized)
                    return;

                try
                {
                    _options = new ObservabilityOptions();
                    configureOptions(_options);

                    // Create core services
                    _redactionService = new RedactionService(_options.Redaction);

                    // Initialize tracing and metrics services
                    _tracingService = new TracingService(Instrumentation.InstrumentationSources.ActivitySource, _options);
                    _metricsService = new MetricsService(Instrumentation.InstrumentationSources.Meter, _options);

                    _isInitialized = true;

                    System.Diagnostics.Debug.WriteLine($"Framework observability initialized for service: {_options.ServiceName}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize framework observability: {ex}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Get the current observability options
        /// </summary>
        public static ObservabilityOptions GetOptions()
        {
            EnsureInitialized();
            return _options!;
        }

        /// <summary>
        /// Get the redaction service instance
        /// </summary>
        public static IRedactionService GetRedactionService()
        {
            EnsureInitialized();
            return _redactionService!;
        }

        /// <summary>
        /// Get the tracing service instance
        /// </summary>
        public static ITracingService GetTracingService()
        {
            EnsureInitialized();
            return _tracingService!;
        }

        /// <summary>
        /// Get the metrics service instance
        /// </summary>
        public static IMetricsService GetMetricsService()
        {
            EnsureInitialized();
            return _metricsService!;
        }

      

        /// <summary>
        /// Check if the observability services have been initialized
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// Clean up observability resources
        /// </summary>
        public static void Cleanup()
        {
            lock (_lockObject)
            {
                try
                {
                    // Dispose of services if they implement IDisposable
                    if (_tracingService is IDisposable tracingDisposable)
                        tracingDisposable.Dispose();

                    if (_metricsService is IDisposable metricsDisposable)
                        metricsDisposable.Dispose();

                    if (_redactionService is IDisposable redactionDisposable)
                        redactionDisposable.Dispose();

                    _isInitialized = false;
                    _options = null;
                    _redactionService = null;
                    _tracingService = null;
                    _metricsService = null;

                    System.Diagnostics.Debug.WriteLine("Framework observability cleanup completed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during framework observability cleanup: {ex}");
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Framework observability has not been initialized. Call Initialize() first.");
            }
        }
    }
}
#endif