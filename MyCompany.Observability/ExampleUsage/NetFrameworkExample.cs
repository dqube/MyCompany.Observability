// Example usage for .NET Framework Web Applications
#if NET462
using System;
using System.Web;
using System.Web.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyCompany.Observability.Extensions;
using MyCompany.Observability.Middleware;
using MyCompany.Observability.Configuration;

namespace MyCompany.Observability.Examples
{
    public class NetFrameworkExample
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            // Configure observability for .NET Framework
            services.AddMyCompanyObservability(options =>
            {
                options.EnableRequestResponseLogging = true;
                options.EnableRedaction = true;
                options.LogLevel = LogLevel.Information;
                
                options.Exporter.EnableConsole = true;
                options.Exporter.EnableOtlp = false;
                
                options.Redaction.SensitiveKeys.AddRange(new[] { "password", "token", "authorization" });
                options.Redaction.RedactionText = "[HIDDEN]";
                
                options.RequestResponseLogging.LogRequestHeaders = true;
                options.RequestResponseLogging.LogResponseHeaders = true;
                options.RequestResponseLogging.MaxBodySize = 2048;
            }, "MyFrameworkWebApi", "1.0.0");

            // Register the logging handler for HTTP clients
            var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
            var observabilityOptions = services.BuildServiceProvider().GetRequiredService<ObservabilityOptions>();
            var redactionService = services.BuildServiceProvider().GetRequiredService<Services.IRedactionService>();
            
            services.AddRequestResponseLogging(loggerFactory, observabilityOptions, redactionService);
        }

        // Example Web API Controller
        public class SampleController : ApiController
        {
            private readonly ILogger<SampleController> _logger;

            public SampleController()
            {
                // In a real application, use dependency injection
                var serviceProvider = GlobalConfiguration.Configuration.DependencyResolver.GetService(typeof(IServiceProvider)) as IServiceProvider;
                _logger = serviceProvider?.GetService<ILogger<SampleController>>();
            }

            [HttpGet]
            public IHttpActionResult Get()
            {
                _logger?.LogInformation("GET request received");
                
                return Ok(new { 
                    Message = "Hello from .NET Framework!", 
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.0"
                });
            }

            [HttpPost]
            public IHttpActionResult Post([FromBody] object data)
            {
                _logger?.LogInformation("POST request received with data");
                
                return Ok(new { 
                    Status = "Success", 
                    ReceivedAt = DateTime.UtcNow 
                });
            }
        }

        // Example HTTP Module for request/response logging
        public class ObservabilityHttpModule : IHttpModule
        {
            private static IServiceProvider _serviceProvider;

            public static void Initialize(IServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
            }

            public void Init(HttpApplication context)
            {
                context.BeginRequest += (sender, e) =>
                {
                    var logger = _serviceProvider?.GetService<ILogger<ObservabilityHttpModule>>();
                    logger?.LogInformation("HTTP Request started: {Method} {Url}", 
                        HttpContext.Current.Request.HttpMethod, 
                        HttpContext.Current.Request.Url?.ToString());
                };

                context.EndRequest += (sender, e) =>
                {
                    var logger = _serviceProvider?.GetService<ILogger<ObservabilityHttpModule>>();
                    logger?.LogInformation("HTTP Request completed: {StatusCode}", 
                        HttpContext.Current.Response.StatusCode);
                };
            }

            public void Dispose()
            {
            }
        }
    }
}
#endif