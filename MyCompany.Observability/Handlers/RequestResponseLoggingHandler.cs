#if !NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyCompany.Observability.Configuration;
using MyCompany.Observability.Services;

namespace MyCompany.Observability.Handlers
{
    /// <summary>
    /// HTTP Client handler for logging request/response data in HTTP client scenarios
    /// </summary>
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

            _logger.LogInformation("{@RequestInfo}", requestInfo);
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

            var logLevel = (int)response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information;
            _logger.Log(logLevel, "{@ResponseInfo}", responseInfo);
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
}
#endif