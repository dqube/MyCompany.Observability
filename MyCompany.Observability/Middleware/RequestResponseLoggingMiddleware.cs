#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
#else
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#endif
using MyCompany.Observability.Configuration;
using MyCompany.Observability.Services;

namespace MyCompany.Observability.Middleware
{
#if !NETFRAMEWORK
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
        private readonly ObservabilityOptions _options;
        private readonly IRedactionService _redactionService;
        private readonly ITracingService? _tracingService;
        private readonly IMetricsService? _metricsService;

        public RequestResponseLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestResponseLoggingMiddleware> logger,
            IOptions<ObservabilityOptions> options,
            IRedactionService redactionService,
            ITracingService? tracingService = null,
            IMetricsService? metricsService = null)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
            _tracingService = tracingService;
            _metricsService = metricsService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_options.EnableRequestResponseLogging || ShouldSkipLogging(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var requestId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;
            var method = context.Request.Method;
            var path = context.Request.Path.Value ?? "/";

            // Start tracing
            using var activity = _tracingService?.StartActivity($"HTTP {method} {path}", ActivityKind.Server);
            _tracingService?.AddTag(activity, "http.method", method);
            _tracingService?.AddTag(activity, "http.route", path);
            _tracingService?.AddTag(activity, "http.request_id", requestId);

            await LogRequestAsync(context.Request, requestId);

            var originalResponseBodyStream = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            try
            {
                await _next(context);
                
                var statusCode = context.Response.StatusCode;
                _tracingService?.AddTag(activity, "http.status_code", statusCode);
                
                if (statusCode >= 400)
                {
                    _tracingService?.SetStatus(activity, ActivityStatusCode.Error, $"HTTP {statusCode}");
                }
                else
                {
                    _tracingService?.SetStatus(activity, ActivityStatusCode.Ok);
                }
            }
            catch (Exception ex)
            {
                _tracingService?.RecordException(activity, ex);
                throw;
            }
            finally
            {
                var duration = DateTime.UtcNow - startTime;
                var statusCode = context.Response.StatusCode;
                
                await LogResponseAsync(context.Response, requestId, duration);

                // Record metrics
                _metricsService?.RecordRequestDuration(duration.TotalMilliseconds, method, path, statusCode);
                _metricsService?.IncrementRequestCount(method, path, statusCode);

                responseBodyStream.Seek(0, SeekOrigin.Begin);
                await responseBodyStream.CopyToAsync(originalResponseBodyStream);
            }
        }

        private bool ShouldSkipLogging(PathString path)
        {
            return _options.RequestResponseLogging.ExcludePaths
                .Any(excludePath => path.StartsWithSegments(excludePath, StringComparison.OrdinalIgnoreCase));
        }

        private async Task LogRequestAsync(HttpRequest request, string requestId)
        {
            var requestInfo = new
            {
                RequestId = requestId,
                Method = request.Method,
                Path = request.Path.Value,
                QueryString = _redactionService.RedactQueryString(request.QueryString.Value),
                Headers = _options.RequestResponseLogging.LogRequestHeaders 
                    ? _redactionService.RedactHeaders(GetHeaders(request.Headers))
                    : null,
                Body = await GetRequestBodyAsync(request)
            };

            _logger.LogInformation("HTTP Request: {@RequestInfo}", requestInfo);
        }

        private async Task LogResponseAsync(HttpResponse response, string requestId, TimeSpan duration)
        {
            var responseInfo = new
            {
                RequestId = requestId,
                StatusCode = response.StatusCode,
                Duration = duration.TotalMilliseconds,
                Headers = _options.RequestResponseLogging.LogResponseHeaders 
                    ? _redactionService.RedactHeaders(GetHeaders(response.Headers))
                    : null,
                Body = await GetResponseBodyAsync(response)
            };

            var logLevel = response.StatusCode >= 400 ? Microsoft.Extensions.Logging.LogLevel.Warning : Microsoft.Extensions.Logging.LogLevel.Information;
            _logger.Log(logLevel, "HTTP Response: {@ResponseInfo}", responseInfo);
        }

        private async Task<string> GetRequestBodyAsync(HttpRequest request)
        {
            if (!_options.RequestResponseLogging.LogRequestBody || 
                !ShouldLogBody(request.ContentType))
                return null;

#if NETSTANDARD2_0
            // EnableBuffering not available in .NET Standard 2.0
            if (!request.Body.CanSeek)
                return "[Body not available - buffering not supported]";
#else
            request.EnableBuffering();
#endif
            var buffer = new byte[_options.RequestResponseLogging.MaxBodySize];
            var bytesRead = await request.Body.ReadAsync(buffer, 0, buffer.Length);
            request.Body.Position = 0;

            if (bytesRead == 0)
                return null;

            var body = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            return _redactionService.RedactSensitiveData(body, request.ContentType);
        }

        private async Task<string> GetResponseBodyAsync(HttpResponse response)
        {
            if (!_options.RequestResponseLogging.LogResponseBody || 
                !ShouldLogBody(response.ContentType))
                return null;

            response.Body.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[Math.Min(_options.RequestResponseLogging.MaxBodySize, response.Body.Length)];
            var bytesRead = await response.Body.ReadAsync(buffer, 0, buffer.Length);
            response.Body.Seek(0, SeekOrigin.Begin);

            if (bytesRead == 0)
                return null;

            var body = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            return _redactionService.RedactSensitiveData(body, response.ContentType);
        }

        private bool ShouldLogBody(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            return _options.RequestResponseLogging.IncludeContentTypes
                .Any(ct => contentType.StartsWith(ct, StringComparison.OrdinalIgnoreCase));
        }

        private Dictionary<string, string> GetHeaders(IHeaderDictionary headers)
        {
            return headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value.ToArray()));
        }
    }
#endif

#if NETFRAMEWORK
    public class RequestResponseLoggingHandler : System.Net.Http.DelegatingHandler
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly ObservabilityOptions _options;
        private readonly IRedactionService _redactionService;

        public RequestResponseLoggingHandler(
            ILoggerFactory loggerFactory,
            ObservabilityOptions options,
            IRedactionService redactionService)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = _loggerFactory.CreateLogger<RequestResponseLoggingHandler>();
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
        }

        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, 
            System.Threading.CancellationToken cancellationToken)
        {
            if (!_options.EnableRequestResponseLogging)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            var requestId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            await LogHttpRequestAsync(request, requestId);

            var response = await base.SendAsync(request, cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            await LogHttpResponseAsync(response, requestId, duration);

            return response;
        }

        private async Task LogHttpRequestAsync(System.Net.Http.HttpRequestMessage request, string requestId)
        {
            var requestInfo = new
            {
                RequestId = requestId,
                Method = request.Method.Method,
                Uri = request.RequestUri?.ToString(),
                Headers = _options.RequestResponseLogging.LogRequestHeaders 
                    ? _redactionService.RedactHeaders(GetHttpHeaders(request.Headers))
                    : null,
                Body = await GetHttpRequestBodyAsync(request)
            };

            _logger.LogInformation("HTTP Request: {@RequestInfo}", requestInfo);
        }

        private async Task LogHttpResponseAsync(System.Net.Http.HttpResponseMessage response, string requestId, TimeSpan duration)
        {
            var responseInfo = new
            {
                RequestId = requestId,
                StatusCode = (int)response.StatusCode,
                Duration = duration.TotalMilliseconds,
                Headers = _options.RequestResponseLogging.LogResponseHeaders 
                    ? _redactionService.RedactHeaders(GetHttpHeaders(response.Headers))
                    : null,
                Body = await GetHttpResponseBodyAsync(response)
            };

            var logLevel = (int)response.StatusCode >= 400 ? Microsoft.Extensions.Logging.LogLevel.Warning : Microsoft.Extensions.Logging.LogLevel.Information;
            _logger.Log(logLevel, "HTTP Response: {@ResponseInfo}", responseInfo);
        }

        private async Task<string> GetHttpRequestBodyAsync(System.Net.Http.HttpRequestMessage request)
        {
            if (!_options.RequestResponseLogging.LogRequestBody || request.Content == null)
                return null;

            var contentType = request.Content.Headers?.ContentType?.MediaType;
            if (!ShouldLogBody(contentType))
                return null;

            var body = await request.Content.ReadAsStringAsync();
            return _redactionService.RedactSensitiveData(body, contentType);
        }

        private async Task<string> GetHttpResponseBodyAsync(System.Net.Http.HttpResponseMessage response)
        {
            if (!_options.RequestResponseLogging.LogResponseBody || response.Content == null)
                return null;

            var contentType = response.Content.Headers?.ContentType?.MediaType;
            if (!ShouldLogBody(contentType))
                return null;

            var body = await response.Content.ReadAsStringAsync();
            return _redactionService.RedactSensitiveData(body, contentType);
        }

        private bool ShouldLogBody(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            return _options.RequestResponseLogging.IncludeContentTypes
                .Any(ct => contentType.StartsWith(ct, StringComparison.OrdinalIgnoreCase));
        }

        private Dictionary<string, string> GetHttpHeaders(System.Net.Http.Headers.HttpHeaders headers)
        {
            return headers?.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)) ?? new Dictionary<string, string>();
        }
    }
#endif
}