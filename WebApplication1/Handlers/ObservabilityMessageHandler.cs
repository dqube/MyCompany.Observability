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
        private readonly ITracingService _tracingService;
        private readonly IMetricsService _metricsService;

        public ObservabilityMessageHandler()
        {
            _logger = SimpleObservabilityConfig.GetLogger<ObservabilityMessageHandler>();
            _tracingService = SimpleObservabilityConfig.GetTracingService();
            _metricsService = SimpleObservabilityConfig.GetMetricsService();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var method = request.Method.Method;
            var path = request.RequestUri?.AbsolutePath ?? "/";

            // Start tracing the HTTP request - use try/finally instead of using var for C# 7.3
            var activity = _tracingService?.StartActivity($"HTTP {method} {path}", ActivityKind.Server);

            try
            {
                // Add request tags to the activity
                _tracingService?.AddTag(activity, "http.method", method);
                _tracingService?.AddTag(activity, "http.url", request.RequestUri?.ToString());
                _tracingService?.AddTag(activity, "http.scheme", request.RequestUri?.Scheme);
                _tracingService?.AddTag(activity, "http.host", request.RequestUri?.Host);
                _tracingService?.AddTag(activity, "http.path", path);
                _tracingService?.AddTag(activity, "request.id", requestId);

                // Add user agent if present
                if (request.Headers.UserAgent != null)
                {
                    _tracingService?.AddTag(activity, "http.user_agent", request.Headers.UserAgent.ToString());
                }

                _logger?.LogInformation("HTTP {Method} {Path} - Request ID: {RequestId}", method, path, requestId);

                // Record request event
                _tracingService?.AddEvent(activity, "http.request.start", new System.Collections.Generic.Dictionary<string, object>
                {
                    ["timestamp"] = DateTimeOffset.UtcNow,
                    ["request.id"] = requestId,
                    ["request.size"] = request.Content?.Headers?.ContentLength ?? 0
                });

                // Execute the request
                var response = await base.SendAsync(request, cancellationToken);
                
                stopwatch.Stop();
                var statusCode = (int)response.StatusCode;

                // Add response information to tracing
                _tracingService?.AddTag(activity, "http.status_code", statusCode);
                _tracingService?.AddTag(activity, "http.response_size", response.Content?.Headers?.ContentLength ?? 0);

                // Set activity status based on response
                if (statusCode >= 400)
                {
                    _tracingService?.SetStatus(activity, ActivityStatusCode.Error, $"HTTP {statusCode}");
                }
                else
                {
                    _tracingService?.SetStatus(activity, ActivityStatusCode.Ok);
                }

                // Record metrics
                _metricsService?.RecordRequestDuration(stopwatch.Elapsed.TotalMilliseconds, method, path, statusCode);
                _metricsService?.IncrementRequestCount(method, path, statusCode);

                // Record response event
                _tracingService?.AddEvent(activity, "http.response.end", new System.Collections.Generic.Dictionary<string, object>
                {
                    ["timestamp"] = DateTimeOffset.UtcNow,
                    ["response.status_code"] = statusCode,
                    ["response.duration_ms"] = stopwatch.Elapsed.TotalMilliseconds
                });

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
                
                // Record exception in tracing and metrics
                _tracingService?.RecordException(activity, ex);
                _metricsService?.RecordRequestDuration(stopwatch.Elapsed.TotalMilliseconds, method, path, 500);
                _metricsService?.IncrementRequestCount(method, path, 500);
                _metricsService?.RecordErrorCount(ex.GetType().Name, "http_request");

                _logger?.LogError(ex, "HTTP {0} {1} - Exception after {2}ms - Request ID: {3}", 
                    method, path, stopwatch.Elapsed.TotalMilliseconds, requestId);

                throw;
            }
            finally
            {
                // Dispose activity in finally block for C# 7.3 compatibility
                activity?.Dispose();
            }
        }
    }
}