using System;
using System.Collections.Generic;
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

            // Configure the HTTP module - this is just registration, no implementation logic here
            this.UseRequestResponseLogging(_serviceProvider);
        }
    }
}
