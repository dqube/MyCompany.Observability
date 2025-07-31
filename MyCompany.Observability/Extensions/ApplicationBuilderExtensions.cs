#if !NETFRAMEWORK
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MyCompany.Observability.Middleware;

namespace MyCompany.Observability.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));

            return app.UseMiddleware<RequestResponseLoggingMiddleware>();
        }
    }
}
#endif