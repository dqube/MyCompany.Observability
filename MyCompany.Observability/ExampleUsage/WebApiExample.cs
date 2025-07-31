// Example usage for ASP.NET Core Web API
#if !NET462
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyCompany.Observability.Extensions;

namespace MyCompany.Observability.Examples
{
    public class WebApiExample
    {
        public static void ConfigureServices(WebApplicationBuilder builder)
        {
            // Add observability with configuration from appsettings.json
            builder.Services.AddMyCompanyObservability(
                builder.Configuration,
                serviceName: "MyWebApi",
                serviceVersion: "1.0.0"
            );

            // Or configure programmatically
            builder.Services.AddMyCompanyObservability(options =>
            {
                options.EnableRequestResponseLogging = true;
                options.EnableRedaction = true;
                options.LogLevel = Configuration.LogLevel.Information;
                
                options.Exporter.EnableConsole = true;
                options.Exporter.EnableOtlp = true;
                options.Exporter.OtlpEndpoint = "http://localhost:4317";
                
                options.Redaction.SensitiveKeys.AddRange(new[] { "api-key", "x-secret" });
                
                options.RequestResponseLogging.MaxBodySize = 4096;
                options.RequestResponseLogging.ExcludePaths.Add("/swagger");
            }, "MyWebApi", "1.0.0");

            builder.Services.AddControllers();
        }

        public static void Configure(WebApplication app)
        {
            // Add request/response logging middleware
            app.UseRequestResponseLogging();
            
            app.UseRouting();
            app.MapControllers();
        }
    }
}
#endif