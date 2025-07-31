using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using WebApplication1.Handlers;

namespace WebApplication1
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services
            
            // Add observability message handler for request/response logging and tracing
            config.MessageHandlers.Add(new ObservabilityMessageHandler());

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
