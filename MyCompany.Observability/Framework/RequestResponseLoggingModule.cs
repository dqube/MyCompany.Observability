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
using MyCompany.Observability.Configuration;
using MyCompany.Observability.Services;

namespace MyCompany.Observability.Framework
{
    public class RequestResponseLoggingModule : IHttpModule
    {
        private static ILoggerFactory _loggerFactory;
        private static ILogger _logger;
        private static ObservabilityOptions _options;
        private static IRedactionService _redactionService;
        private static ITracingService _tracingService;
        private static IMetricsService _metricsService;

        public static void Configure(
            ILoggerFactory loggerFactory,
            ObservabilityOptions options,
            IRedactionService redactionService,
            ITracingService tracingService = null,
            IMetricsService metricsService = null)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = _loggerFactory.CreateLogger<RequestResponseLoggingModule>();
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
            _tracingService = tracingService;
            _metricsService = metricsService;
        }

        public void Init(HttpApplication context)
        {
            if (_options?.EnableRequestResponseLogging == true)
            {
                context.BeginRequest += OnBeginRequest;
                context.EndRequest += OnEndRequest;
                context.Error += OnError;
            }
        }

        private void OnBeginRequest(object sender, EventArgs e)
        {
            var context = HttpContext.Current;
            if (context == null)
                return;

            var requestId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;
            var method = context.Request.HttpMethod;
            var url = context.Request.Url;
            var path = url?.AbsolutePath ?? "/";
            var scheme = url?.Scheme ?? "http";
            var host = url?.Host;
            var port = url?.Port;
            var query = url?.Query;

            // Store request data in context for use in EndRequest
            context.Items["RequestId"] = requestId;
            context.Items["StartTime"] = startTime;
            context.Items["Method"] = method;
            context.Items["Path"] = path;
            context.Items["Url"] = url?.ToString();

            // Parse distributed tracing headers for parent context
            var parentContext = ExtractTraceContext(context.Request);
            
            // Start tracing with proper HTTP semantic conventions
            Activity? activity = null;
            if (_tracingService != null)
            {
                // Create activity with parent context if available
                if (parentContext.HasValue)
                {
                    activity = _tracingService.StartActivity($"HTTP {method}", ActivityKind.Server, parentContext.Value);
                }
                else
                {
                    activity = _tracingService.StartActivity($"HTTP {method}", ActivityKind.Server);
                }

                if (activity != null)
                {
                    // HTTP Semantic Conventions (current stable attributes)
                    _tracingService.AddTag(activity, "http.request.method", method);
                    _tracingService.AddTag(activity, "url.scheme", scheme);
                    _tracingService.AddTag(activity, "url.path", path);
                    
                    if (!string.IsNullOrEmpty(query))
                        _tracingService.AddTag(activity, "url.query", query);
                    
                    if (!string.IsNullOrEmpty(host))
                    {
                        _tracingService.AddTag(activity, "server.address", host);
                        if (port.HasValue && !IsDefaultPort(scheme, port.Value))
                            _tracingService.AddTag(activity, "server.port", port.Value);
                    }

                    // Client information
                    var userAgent = context.Request.Headers["User-Agent"];
                    if (!string.IsNullOrEmpty(userAgent))
                        _tracingService.AddTag(activity, "user_agent.original", userAgent);

                    var clientAddress = GetClientAddress(context.Request);
                    if (!string.IsNullOrEmpty(clientAddress))
                        _tracingService.AddTag(activity, "client.address", clientAddress);

                    // HTTP version
                    var httpVersion = GetHttpVersion(context.Request);
                    if (!string.IsNullOrEmpty(httpVersion))
                        _tracingService.AddTag(activity, "network.protocol.version", httpVersion);

                    // Custom request ID for correlation
                    _tracingService.AddTag(activity, "http.request_id", requestId);
                    
                    // Set as current activity for .NET Framework
                    Activity.Current = activity;
                    context.Items["Activity"] = activity;
                }
            }

            // Record metrics - active requests counter
            _metricsService?.IncrementActiveRequests();

            // Log request asynchronously to avoid blocking
            if (_options.EnableRequestResponseLogging && !ShouldSkipLogging(path))
            {
                Task.Run(() => LogRequestAsync(context.Request, requestId));
            }
        }

        private void OnEndRequest(object sender, EventArgs e)
        {
            var context = HttpContext.Current;
            if (context == null || !context.Items.Contains("RequestId"))
                return;

            var requestId = (string)context.Items["RequestId"];
            var startTime = (DateTime)context.Items["StartTime"];
            var method = (string)context.Items["Method"];
            var path = (string)context.Items["Path"];
            var url = (string)context.Items["Url"];
            var activity = context.Items["Activity"] as Activity;

            var duration = DateTime.UtcNow - startTime;
            var statusCode = context.Response.StatusCode;
            var contentLength = context.Response.Headers["Content-Length"];

            // Complete tracing with HTTP semantic conventions
            if (activity != null)
            {
                try
                {
                    // Response attributes
                    _tracingService?.AddTag(activity, "http.response.status_code", statusCode);
                    
                    if (!string.IsNullOrEmpty(contentLength) && long.TryParse(contentLength, out var length))
                        _tracingService?.AddTag(activity, "http.response.body.size", length);

                    // Route information (if available from routing)
                    var routeData = context.Items["MS_HttpRouteData"] ?? context.Items["__route__"];
                    if (routeData != null)
                    {
                        var routeTemplate = ExtractRouteTemplate(routeData);
                        if (!string.IsNullOrEmpty(routeTemplate))
                            _tracingService.AddTag(activity, "http.route", routeTemplate);
                    }

                    // Set activity status based on HTTP status code
                    if (statusCode >= 400)
                    {
                        var errorType = GetErrorType(statusCode);
                        _tracingService.AddTag(activity, "error.type", errorType);
                        _tracingService.SetStatus(activity, ActivityStatusCode.Error, $"HTTP {statusCode}");
                    }
                    else
                    {
                        _tracingService.SetStatus(activity, ActivityStatusCode.Ok);
                    }
                }
                catch (Exception ex)
                {
                    // Record exception in activity if something goes wrong
                    _tracingService?.RecordException(activity, ex);
                }
                finally
                {
                    // Clean up Activity.Current and dispose
                    if (Activity.Current == activity)
                        Activity.Current = null;
                    activity.Dispose();
                }
            }

            // Record metrics with proper labels
            _metricsService?.RecordRequestDuration(duration.TotalMilliseconds, method, GetRouteForMetrics(path), statusCode);
            _metricsService?.IncrementRequestCount(method, GetRouteForMetrics(path), statusCode);
            _metricsService?.DecrementActiveRequests();

            // Log response asynchronously to avoid blocking
            if (_options.EnableRequestResponseLogging && !ShouldSkipLogging(path))
            {
                Task.Run(() => LogResponseAsync(context.Response, requestId, duration));
            }
        }

        private void OnError(object sender, EventArgs e)
        {
            var context = HttpContext.Current;
            if (context == null)
                return;

            var exception = context.Server.GetLastError();
            if (exception == null)
                return;

            var activity = context.Items["Activity"] as Activity;
            if (activity != null && _tracingService != null)
            {
                // Record the exception in the current activity
                _tracingService.RecordException(activity, exception);
                
                // Add error type attribute
                _tracingService.AddTag(activity, "error.type", exception.GetType().Name);
            }

            // Record error metrics
            _metricsService?.RecordErrorCount(exception.GetType().Name, "http.request");

            // Log the exception
            _logger?.LogError(exception, "Unhandled exception in HTTP request. RequestId: {RequestId}", 
                context.Items["RequestId"]);
        }

        private bool ShouldSkipLogging(string path)
        {
            if (string.IsNullOrEmpty(path) || _options?.RequestResponseLogging?.ExcludePaths == null)
                return false;

            return _options.RequestResponseLogging.ExcludePaths
                .Any(excludePath => path.StartsWith(excludePath, StringComparison.OrdinalIgnoreCase));
        }

        private async Task LogRequestAsync(HttpRequest request, string requestId)
        {
            try
            {
                var requestInfo = new
                {
                    RequestId = requestId,
                    Method = request.HttpMethod,
                    Path = request.Url?.AbsolutePath,
                    QueryString = _redactionService.RedactQueryString(request.Url?.Query),
                    Headers = _options.RequestResponseLogging.LogRequestHeaders 
                        ? _redactionService.RedactHeaders(GetHeaders(request.Headers))
                        : null,
                    Body = await GetRequestBodyAsync(request)
                };

                _logger?.LogInformation("HTTP Request: {@RequestInfo}", requestInfo);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error logging HTTP request for RequestId: {RequestId}", requestId);
            }
        }

        private async Task LogResponseAsync(HttpResponse response, string requestId, TimeSpan duration)
        {
            try
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
                _logger?.Log(logLevel, "HTTP Response: {@ResponseInfo}", responseInfo);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error logging HTTP response for RequestId: {RequestId}", requestId);
            }
        }

        private async Task<string> GetRequestBodyAsync(HttpRequest request)
        {
            if (!_options.RequestResponseLogging.LogRequestBody || 
                !ShouldLogBody(request.ContentType))
                return null;

            try
            {
                // For .NET Framework, we need to read the input stream carefully
                if (request.InputStream.CanSeek)
                {
                    var originalPosition = request.InputStream.Position;
                    request.InputStream.Position = 0;
                    
                    using (var reader = new StreamReader(request.InputStream, Encoding.UTF8, false, 1024, true))
                    {
                        var body = await reader.ReadToEndAsync();
                        request.InputStream.Position = originalPosition;
                        
                        if (string.IsNullOrEmpty(body))
                            return null;
                            
                        return _redactionService.RedactSensitiveData(body, request.ContentType);
                    }
                }
                
                return "[Body not available - stream not seekable]";
            }
            catch (Exception)
            {
                return "[Body not available - error reading stream]";
            }
        }

        private async Task<string> GetResponseBodyAsync(HttpResponse response)
        {
            if (!_options.RequestResponseLogging.LogResponseBody || 
                !ShouldLogBody(response.ContentType))
                return null;

            // Note: Reading response body in .NET Framework HttpModule is complex
            // as the response stream is not typically accessible at this point.
            // This is a limitation of the HttpModule approach compared to ASP.NET Core middleware.
            return "[Response body logging not supported in .NET Framework HttpModule]";
        }

        private bool ShouldLogBody(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            return _options.RequestResponseLogging.IncludeContentTypes
                .Any(ct => contentType.StartsWith(ct, StringComparison.OrdinalIgnoreCase));
        }

        private Dictionary<string, string> GetHeaders(System.Collections.Specialized.NameValueCollection headers)
        {
            var result = new Dictionary<string, string>();
            foreach (string key in headers.Keys)
            {
                if (key != null)
                {
                    result[key] = headers[key];
                }
            }
            return result;
        }

        private ActivityContext? ExtractTraceContext(HttpRequest request)
        {
            try
            {
                var traceparent = request.Headers["traceparent"];
                if (string.IsNullOrEmpty(traceparent))
                    return null;

                // Parse W3C traceparent header: version-traceid-spanid-flags
                var parts = traceparent.Split('-');
                if (parts.Length != 4)
                    return null;

                var version = parts[0];
                var traceId = parts[1];
                var spanId = parts[2];
                var flags = parts[3];

                if (version != "00" || traceId.Length != 32 || spanId.Length != 16)
                    return null;

                var traceIdBytes = new byte[16];
                var spanIdBytes = new byte[8];

                for (int i = 0; i < 16; i++)
                {
                    traceIdBytes[i] = Convert.ToByte(traceId.Substring(i * 2, 2), 16);
                }

                for (int i = 0; i < 8; i++)
                {
                    spanIdBytes[i] = Convert.ToByte(spanId.Substring(i * 2, 2), 16);
                }

                var activityTraceId = ActivityTraceId.CreateFromBytes(traceIdBytes);
                var activitySpanId = ActivitySpanId.CreateFromBytes(spanIdBytes);
                var traceFlags = Convert.ToByte(flags, 16);

                var tracestate = request.Headers["tracestate"];
                return new ActivityContext(activityTraceId, activitySpanId, (ActivityTraceFlags)traceFlags, tracestate);
            }
            catch
            {
                return null;
            }
        }

        private bool IsDefaultPort(string scheme, int port)
        {
            return (scheme == "http" && port == 80) || (scheme == "https" && port == 443);
        }

        private string GetClientAddress(HttpRequest request)
        {
            // Try various headers that might contain the real client IP
            var headers = new[] { "X-Forwarded-For", "X-Real-IP", "CF-Connecting-IP", "X-Client-IP" };
            
            foreach (var header in headers)
            {
                var value = request.Headers[header];
                if (!string.IsNullOrEmpty(value))
                {
                    // X-Forwarded-For can contain multiple IPs, take the first one
                    return value.Split(',')[0].Trim();
                }
            }

            return request.UserHostAddress;
        }

        private string GetHttpVersion(HttpRequest request)
        {
            var serverVars = request.ServerVariables;
            var protocol = serverVars["SERVER_PROTOCOL"];
            
            if (string.IsNullOrEmpty(protocol))
                return "1.1";

            // Convert "HTTP/1.1" to "1.1"
            return protocol.Replace("HTTP/", "");
        }

        private string ExtractRouteTemplate(object routeData)
        {
            try
            {
                // For Web API routing
                if (routeData.GetType().Name == "HttpRouteData")
                {
                    var route = routeData.GetType().GetProperty("Route")?.GetValue(routeData);
                    var routeTemplate = route?.GetType().GetProperty("RouteTemplate")?.GetValue(route) as string;
                    return routeTemplate;
                }

                // For MVC routing
                var routeDataDict = routeData as System.Collections.IDictionary;
                if (routeDataDict != null && routeDataDict.Contains("__route__"))
                {
                    return routeDataDict["__route__"] as string;
                }
            }
            catch
            {
                // Ignore errors in route extraction
            }

            return null;
        }

        private string GetErrorType(int statusCode)
        {
            return statusCode switch
            {
                400 => "bad_request",
                401 => "unauthorized", 
                403 => "forbidden",
                404 => "not_found",
                405 => "method_not_allowed",
                408 => "request_timeout",
                409 => "conflict",
                422 => "unprocessable_entity",
                429 => "too_many_requests",
                500 => "internal_server_error",
                501 => "not_implemented",
                502 => "bad_gateway",
                503 => "service_unavailable",
                504 => "gateway_timeout",
                _ => statusCode.ToString()
            };
        }

        private string GetRouteForMetrics(string path)
        {
            // For metrics, we want to avoid high cardinality
            // This is a simple implementation - in production you'd want more sophisticated route templating
            if (string.IsNullOrEmpty(path))
                return "/";

            // Remove numeric segments to create route templates
            var segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (int.TryParse(segments[i], out _) || Guid.TryParse(segments[i], out _))
                {
                    segments[i] = "{id}";
                }
            }

            return string.Join("/", segments);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
#endif