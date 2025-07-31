using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MyCompany.Observability.Services;
using WebApplication1.App_Start;
using WebApplication1.Infrastructure;

namespace WebApplication1.Handlers
{
    public class ObservabilityMessageHandler : DelegatingHandler
    {
        private readonly ISimpleLogger _logger;

        public ObservabilityMessageHandler()
        {
            _logger = SimpleObservabilityConfig.GetLogger<ObservabilityMessageHandler>();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var method = request.Method.Method;
            var path = request.RequestUri?.AbsolutePath ?? "/";

            try
            {
                _logger?.LogInformation("HTTP {Method} {Path} - Request ID: {RequestId}", method, path, requestId);

                var response = await base.SendAsync(request, cancellationToken);

                stopwatch.Stop();
                var statusCode = (int)response.StatusCode;

                if (statusCode >= 400)
                {
                    _logger?.LogWarning("HTTP {0} {1} - {2} - {3}ms - Request ID: {4}",
                        method, path, statusCode, stopwatch.Elapsed.TotalMilliseconds, requestId);
                }
                else
                {
                    _logger?.LogInformation("HTTP {0} {1} - {2} - {3}ms - Request ID: {4}",
                        method, path, statusCode, stopwatch.Elapsed.TotalMilliseconds, requestId);
                }

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger?.LogError(ex, "HTTP {0} {1} - Exception after {2}ms - Request ID: {3}",
                    method, path, stopwatch.Elapsed.TotalMilliseconds, requestId);

                throw;
            }
        }
    }
}