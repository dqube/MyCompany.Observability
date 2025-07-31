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
            
            // Add logging services for .NET Framework
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            });
            
            // Add observability services
            services.AddObservability(options =>
            {
                options.ServiceName = "WebApplication1";
                options.ServiceVersion = "1.0.0";
                options.EnableRequestResponseLogging = true;
                
                // Configure request/response logging
                options.RequestResponseLogging.LogRequestBody = true;
                options.RequestResponseLogging.LogResponseBody = true;
                options.RequestResponseLogging.LogRequestHeaders = true;
                options.RequestResponseLogging.LogResponseHeaders = true;
                options.RequestResponseLogging.IncludeContentTypes.Add("application/json");
                options.RequestResponseLogging.IncludeContentTypes.Add("application/xml");
                options.RequestResponseLogging.IncludeContentTypes.Add("text/plain");
            });

            // Add request response logging registration
            services.AddRequestResponseLogging();

            _serviceProvider = services.BuildServiceProvider();

            // Configure the HTTP module - this is just registration, no implementation logic here
            this.UseRequestResponseLogging(_serviceProvider);
        }
    }
}
