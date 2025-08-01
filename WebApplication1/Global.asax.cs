using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyCompany.Observability.Extensions;
using MyCompany.Observability.Configuration;

namespace WebApplication1
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        private static IServiceProvider _serviceProvider;

        public static IServiceProvider ServiceProvider => _serviceProvider;

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Configure observability - registration only
            ConfigureObservability();
        }

        private void ConfigureObservability()
        {
            var services = new ServiceCollection();
            
            // Load configuration from web.config appSettings
            var configOptions = ConfigurationHelper.LoadFromAppSettings();
            
            // Add observability services with configuration from web.config
            services.AddObservability(options =>
            {
                // Copy all configuration from web.config
                options.ServiceName = configOptions.ServiceName;
                options.ServiceVersion = configOptions.ServiceVersion;
                options.EnableRequestResponseLogging = configOptions.EnableRequestResponseLogging;
                
                // Copy logging configuration
                options.Logging = configOptions.Logging;
                
                // Copy request/response logging configuration
                options.RequestResponseLogging = configOptions.RequestResponseLogging;
                
                // Copy exporter configuration
                options.Exporter = configOptions.Exporter;
                
                // Copy tracing configuration
                options.Tracing = configOptions.Tracing;
                
                // Copy metrics configuration
                options.Metrics = configOptions.Metrics;
            });

            // Add request response logging registration
            services.AddRequestResponseLogging();

            _serviceProvider = services.BuildServiceProvider();

            // Get the logger factory and create a test logger to verify logging is working
            var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<WebApiApplication>();
            
            // Test log message to verify logging is working
            logger.LogInformation("WebApplication1 observability initialized successfully - ServiceName: {ServiceName}, ServiceVersion: {ServiceVersion}", 
                configOptions.ServiceName, configOptions.ServiceVersion);
            
            // Also write to Debug output for immediate visibility
            Debug.WriteLine($"[WebApplication1] Observability initialized - Service: {configOptions.ServiceName} v{configOptions.ServiceVersion}");
            Debug.WriteLine($"[WebApplication1] Console logging enabled: {configOptions.Logging?.EnableConsoleLogging}");
            Debug.WriteLine($"[WebApplication1] Request/Response logging enabled: {configOptions.EnableRequestResponseLogging}");

            // Configure the HTTP module - this is just registration, no implementation logic here
            this.UseRequestResponseLogging(_serviceProvider);
        }
    }
}
