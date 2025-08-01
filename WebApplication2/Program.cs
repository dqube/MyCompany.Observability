using MyCompany.Observability.Extensions;
using Scalar.AspNetCore;

namespace WebApplication2
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();

            // Add observability services
            builder.Services.AddObservability(builder.Configuration, "WebApplication2", "1.0.0");

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseHttpsRedirection();

            // Add request/response logging middleware
            app.UseRequestResponseLogging();

            app.UseAuthorization();

            var summaries = new[]
            {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

            app.MapGet("/weatherforecast", (HttpContext httpContext, ILogger<Program> logger) =>
            {
                var userDetails = new { username = "DemoUser", role = "User", password="test" };
                logger.LogInformation("User details: {@UserDetails}", userDetails);
                logger.LogInformation("Getting weather forecast for request {RequestId}", httpContext.TraceIdentifier);
                
                var forecast = Enumerable.Range(1, 5).Select(index =>
                    new WeatherForecast
                    {
                        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        TemperatureC = Random.Shared.Next(-20, 55),
                        Summary = summaries[Random.Shared.Next(summaries.Length)]
                    })
                    .ToArray();
                
                logger.LogInformation("Weather forecast generated with {Count} items for request {RequestId}", 
                    forecast.Length, httpContext.TraceIdentifier);
                return forecast;
            })
            .WithName("GetWeatherForecast");

            app.MapGet("/health", (ILogger<Program> logger) =>
            {
                logger.LogInformation("Health check requested");
                return Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
            })
            .WithName("HealthCheck");
            app.MapPost("/userdetails", (UserDetails userDetails, ILogger<Program> logger) =>
            {
                // Use the {@UserDetails} syntax to trigger serialization and redaction
                logger.LogInformation("Received user details: {@UserDetails}", userDetails);
                return Results.Ok(userDetails);
            });
                app.Run();
        }
    }
    public class UserDetails
    {
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; // Sensitive data, should be redacted
    }
}
