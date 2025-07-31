using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using WebApplication1.App_Start;
using WebApplication1.Infrastructure;

namespace WebApplication1
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            try
            {
                // Initialize simplified observability
                SimpleObservabilityConfig.Initialize();

                // Get logger to log application start
                var logger = SimpleObservabilityConfig.GetLogger<WebApiApplication>();
                logger?.LogInformation("WebApplication1 is starting up...");

                AreaRegistration.RegisterAllAreas();
                GlobalConfiguration.Configure(WebApiConfig.Register);
                FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
                RouteConfig.RegisterRoutes(RouteTable.Routes);
                BundleConfig.RegisterBundles(BundleTable.Bundles);

                logger?.LogInformation("WebApplication1 startup completed successfully");
            }
            catch (Exception ex)
            {
                // Log the error if possible
                try
                {
                    var logger = SimpleObservabilityConfig.GetLogger<WebApiApplication>();
                    logger?.LogError(ex, "Failed to start WebApplication1");
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to start WebApplication1: {ex}");
                }
                throw;
            }
        }

        protected void Application_End()
        {
            try
            {
                var logger = SimpleObservabilityConfig.GetLogger<WebApiApplication>();
                logger?.LogInformation("WebApplication1 is shutting down...");

                // Cleanup observability services
                SimpleObservabilityConfig.Cleanup();
            }
            catch (Exception ex)
            {
                // Log the error if possible
                System.Diagnostics.Debug.WriteLine($"Error during application shutdown: {ex}");
            }
        }

        protected void Application_Error()
        {
            var exception = Server.GetLastError();
            if (exception != null)
            {
                try
                {
                    var logger = SimpleObservabilityConfig.GetLogger<WebApiApplication>();
                    logger?.LogError(exception, "Unhandled application error occurred");
                }
                catch
                {
                    // If logging fails, we can't do much more
                    System.Diagnostics.Debug.WriteLine($"Unhandled error: {exception}");
                }
            }
        }
    }
}
