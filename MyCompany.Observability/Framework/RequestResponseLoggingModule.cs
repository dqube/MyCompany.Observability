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
        private static ILoggerFactory? _loggerFactory;
        private static ILogger? _logger;
        private static ObservabilityOptions? _options;
        private static IRedactionService? _redactionService;
        private static ITracingService? _tracingService;
        private static IMetricsService? _metricsService;

        public static void Configure(
            ILoggerFactory loggerFactory,
            ObservabilityOptions options,
            IRedactionService redactionService,
            ITracingService? tracingService = null,
            IMetricsService? metricsService = null)
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
            context.BeginRequest += OnBeginRequest;
            context.EndRequest += OnEndRequest;
            context.PostRequestHandlerExecute += OnPostRequestHandlerExecute;
            context.Error += OnError;
            context.PreSendRequestContent += OnPreSendRequestContent;

            _logger?.LogInformation("RequestResponseLoggingModule initialized. Options enabled: {LoggingEnabled}, TracingService available: {TracingAvailable}",
                _options?.EnableRequestResponseLogging,
                _tracingService != null);
        }

        private void OnBeginRequest(object sender, EventArgs e)
        {
            var application = sender as HttpApplication;
            if (application == null)
            {
                _logger?.LogWarning("HttpApplication is null in OnBeginRequest");
                return;
            }

            var context = application.Context;
            if (context == null)
            {
                _logger?.LogWarning("HttpApplication.Context is null in OnBeginRequest");
                return;
            }

            var requestId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;
            var method = context.Request.HttpMethod;
            var url = context.Request.Url;
            var path = url?.AbsolutePath ?? "/";

            _logger?.LogInformation("OnBeginRequest called for {Method} {Path}", method, path);

            if (_options?.RequestResponseLogging?.LogResponseBody == true)
            {
                _logger?.LogInformation("Installing ResponseCapturingFilter for {Method} {Path}", method, path);

                var originalFilter = context.Response.Filter;
                var capturingFilter = new ResponseFilterStream(originalFilter, context);
                context.Response.Filter = capturingFilter;
                context.Items["ResponseFilter"] = capturingFilter;

                _logger?.LogDebug("ResponseCapturingFilter installed successfully. Original filter: {OriginalFilter}",
                    originalFilter?.GetType().Name ?? "null");
            }
            else
            {
                _logger?.LogInformation("Response body logging disabled or not configured for {Method} {Path}", method, path);
            }

            var scheme = url?.Scheme ?? "http";
            var host = url?.Host;
            var port = url?.Port;
            var query = url?.Query;

            context.Items["RequestId"] = requestId;
            context.Items["StartTime"] = startTime;
            context.Items["Method"] = method;
            context.Items["Path"] = path;
            context.Items["Url"] = url?.ToString();

            var parentContext = ExtractTraceContext(context.Request);

            Activity? activity = null;
            if (_tracingService != null)
            {
                _logger?.LogInformation("Creating activity for {Method} {Path} with TracingService", method, path);

                if (parentContext.HasValue)
                {
                    _logger?.LogInformation("Creating activity with parent context: {ParentId}", parentContext.Value.TraceId);
                    activity = _tracingService.StartActivity($"HTTP {method}", ActivityKind.Server, parentContext.Value);
                }
                else
                {
                    _logger?.LogInformation("Creating activity without parent context");
                    activity = _tracingService.StartActivity($"HTTP {method}", ActivityKind.Server);
                }

                if (activity != null)
                {
                    _logger?.LogInformation("Activity created successfully with ID: {ActivityId}", activity.Id);
                }
                else
                {
                    _logger?.LogWarning("Activity creation failed - returned null");
                }

                if (activity != null)
                {
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

                    var userAgent = context.Request.Headers["User-Agent"];
                    if (!string.IsNullOrEmpty(userAgent))
                        _tracingService.AddTag(activity, "user_agent.original", userAgent);

                    var clientAddress = GetClientAddress(context.Request);
                    if (!string.IsNullOrEmpty(clientAddress))
                        _tracingService.AddTag(activity, "client.address", clientAddress);

                    var httpVersion = GetHttpVersion(context.Request);
                    if (!string.IsNullOrEmpty(httpVersion))
                        _tracingService.AddTag(activity, "network.protocol.version", httpVersion);

                    _tracingService.AddTag(activity, "http.request_id", requestId);

                    Activity.Current = activity;
                    _logger?.LogInformation("Set Activity.Current to: {ActivityId}", activity.Id);
                    context.Items["Activity"] = activity;
                }
            }
            else
            {
                _logger?.LogWarning("TracingService is null - cannot create activity for {Method} {Path}", method, path);
            }

            _metricsService?.IncrementActiveRequests();

            if (_options != null && _options.EnableRequestResponseLogging && !ShouldSkipLogging(path))
            {
                Task.Run(() => LogRequestAsync(context.Request, requestId));
            }
        }

        private void OnEndRequest(object sender, EventArgs e)
        {
            var application = sender as HttpApplication;
            if (application == null) return;

            var context = application.Context;
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

            if (activity != null && _tracingService != null)
            {
                try
                {
                    _tracingService.AddTag(activity, "http.response.status_code", statusCode);

                    if (!string.IsNullOrEmpty(contentLength) && long.TryParse(contentLength, out var length))
                        _tracingService.AddTag(activity, "http.response.body.size", length);

                    var routeData = context.Items["MS_HttpRouteData"] ?? context.Items["__route__"];
                    if (routeData != null)
                    {
                        var routeTemplate = ExtractRouteTemplate(routeData);
                        if (!string.IsNullOrEmpty(routeTemplate))
                            _tracingService.AddTag(activity, "http.route", routeTemplate);
                    }

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
                    _tracingService.RecordException(activity, ex);
                }
                finally
                {
                    if (Activity.Current == activity)
                        Activity.Current = null;
                    activity.Dispose();
                }
            }

            _metricsService?.RecordRequestDuration(duration.TotalMilliseconds, method, GetRouteForMetrics(path), statusCode);
            _metricsService?.IncrementRequestCount(method, GetRouteForMetrics(path), statusCode);
            _metricsService?.DecrementActiveRequests();

            if (_options != null && _options.EnableRequestResponseLogging && !ShouldSkipLogging(path))
            {
                Task.Run(() => LogResponseAsync(context.Response, requestId, duration, context));
            }
        }

        private void OnPreSendRequestContent(object sender, EventArgs e)
        {
            var application = sender as HttpApplication;
            if (application == null) return;

            var context = application.Context;
            if (context == null) return;

            if (_options?.RequestResponseLogging?.LogResponseBody == true)
            {
                try
                {
                    var response = context.Response;
                    if (ShouldLogBody(response.ContentType))
                    {
                        _logger?.LogDebug("PreSendRequestContent: Attempting to capture response body for content type {ContentType}", response.ContentType);

                        var responseText = TryGetResponseFromBuffer(context);
                        if (!string.IsNullOrEmpty(responseText))
                        {
                            context.Items["CapturedResponseBody"] = responseText;
                            _logger?.LogDebug("PreSendRequestContent: Successfully captured {Length} chars from response buffer", responseText.Length);
                        }
                        else
                        {
                            _logger?.LogDebug("PreSendRequestContent: Could not capture response from buffer");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in PreSendRequestContent");
                    context.Items["CapturedResponseBody"] = $"[PreSendRequestContent error: {ex.Message}]";
                }
            }
        }

        private string? TryGetResponseFromBuffer(HttpContext context)
        {
            try
            {
                // Try to get the captured response from the ResponseFilterStream
                var responseFilter = context.Items["ResponseFilter"] as ResponseFilterStream;
                if (responseFilter != null)
                {
                    // Force flush to ensure all data is captured
                    context.Response.Flush();
                    // Try to get the captured content from the filter's internal buffer
                    var captured = context.Items["CapturedResponseBody"] as string;
                    if (!string.IsNullOrEmpty(captured))
                        return captured;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "TryGetResponseFromBuffer error");
                return null;
            }
        }

        private void OnError(object sender, EventArgs e)
        {
            var application = sender as HttpApplication;
            if (application == null) return;

            var context = application.Context;
            if (context == null) return;

            var exception = context.Server.GetLastError();
            if (exception == null)
                return;

            var activity = context.Items["Activity"] as Activity;
            if (activity != null && _tracingService != null)
            {
                _tracingService.RecordException(activity, exception);
                _tracingService.AddTag(activity, "error.type", exception.GetType().Name);
            }

            _metricsService?.RecordErrorCount(exception.GetType().Name, "http.request");

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

        // Replace LogRequestAsync and LogResponseAsync with JSON logging and redaction

        private async Task LogRequestAsync(HttpRequest request, string requestId)
        {
            try
            {
                var requestInfo = new
                {
                    RequestId = requestId,
                    Method = request.HttpMethod,
                    Path = request.Url?.AbsolutePath,
                    QueryString = _redactionService?.RedactQueryString(request.Url?.Query ?? string.Empty),
                    Headers = _options?.RequestResponseLogging?.LogRequestHeaders == true
                        ? _redactionService?.RedactHeaders(GetHeaders(request.Headers))
                        : null,
                    Body = await GetRequestBodyAsync(request)
                };

                var json = System.Text.Json.JsonSerializer.Serialize(requestInfo, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                _logger?.LogInformation("Request: {RequestJson}", json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error logging HTTP request for RequestId: {RequestId}", requestId);
            }
        }

        private async Task LogResponseAsync(HttpResponse response, string requestId, TimeSpan duration, HttpContext? context = null)
        {
            try
            {
                var responseInfo = new
                {
                    RequestId = requestId,
                    StatusCode = response.StatusCode,
                    Duration = duration.TotalMilliseconds,
                    Headers = _options?.RequestResponseLogging?.LogResponseHeaders == true
                        ? _redactionService?.RedactHeaders(GetHeaders(response.Headers))
                        : null,
                    Body = await GetResponseBodyAsync(response, context)
                };

                var json = System.Text.Json.JsonSerializer.Serialize(responseInfo, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                var logLevel = response.StatusCode >= 400 ? Microsoft.Extensions.Logging.LogLevel.Warning : Microsoft.Extensions.Logging.LogLevel.Information;
                _logger?.Log(logLevel, "Response: {ResponseJson}", json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error logging HTTP response for RequestId: {RequestId}", requestId);
            }
        }

        private async Task<string?> GetRequestBodyAsync(HttpRequest request)
        {
            if (_options?.RequestResponseLogging?.LogRequestBody != true ||
                !ShouldLogBody(request.ContentType))
                return null;

            try
            {
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

                        return _redactionService?.RedactSensitiveData(body, request.ContentType ?? "application/json");
                    }
                }

                return "[Body not available - stream not seekable]";
            }
            catch (Exception)
            {
                return "[Body not available - error reading stream]";
            }
        }

        private async Task<string?> GetResponseBodyAsync(HttpResponse response, HttpContext? context = null)
        {
            if (_options?.RequestResponseLogging?.LogResponseBody != true)
            {
                _logger?.LogDebug("Response body logging disabled");
                return null;
            }

            try
            {
                context ??= HttpContext.Current;

                if (context == null)
                {
                    _logger?.LogDebug("HttpContext is null");
                    return "[No HttpContext]";
                }

                var capturedBody = context.Items["CapturedResponseBody"] as string;
                var capturedContentType = context.Items["ResponseContentType"] as string ?? response.ContentType;

                _logger?.LogDebug("Web API action filter captured body: {HasContent}, ContentType: {ContentType}",
                    !string.IsNullOrEmpty(capturedBody), capturedContentType);

                if (!string.IsNullOrEmpty(capturedBody))
                {
                    if (!ShouldLogBody(capturedContentType))
                    {
                        _logger?.LogDebug("Content type {ContentType} not in allowed list", capturedContentType);
                        return null;
                    }

                    _logger?.LogDebug("Using Web API action filter captured response body of length {Length}", capturedBody.Length);
                    return _redactionService?.RedactSensitiveData(capturedBody, capturedContentType ?? "application/json");
                }

                _logger?.LogDebug("No Web API action filter capture, checking response filter");

                if (!ShouldLogBody(response.ContentType))
                {
                    _logger?.LogDebug("Response content type {ContentType} not in allowed list", response.ContentType);
                    return null;
                }

                var responseFilter = context.Items["ResponseFilter"] as ResponseFilterStream;
                if (responseFilter != null)
                {
                    var filterCapturedBody = context.Items["CapturedResponseBody"] as string;
                    if (!string.IsNullOrEmpty(filterCapturedBody))
                    {
                        _logger?.LogDebug("Using response filter captured content of length {Length}", filterCapturedBody.Length);
                        return _redactionService?.RedactSensitiveData(filterCapturedBody, response.ContentType ?? "application/json");
                    }
                }

                var itemsInfo = new System.Text.StringBuilder();
                itemsInfo.AppendLine("HttpContext.Items contents:");
                foreach (var key in context.Items.Keys)
                {
                    var value = context.Items[key];
                    var valueStr = value?.ToString();
                    var preview = string.IsNullOrEmpty(valueStr) ? "null" : valueStr!.Substring(0, Math.Min(50, valueStr.Length));
                    itemsInfo.AppendLine($"  {key}: {value?.GetType().Name} = {preview}...");
                }
                _logger?.LogDebug(itemsInfo.ToString());

                return "[Response body not captured - neither action filter nor response filter captured content]";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting response body");
                return $"[Response body error: {ex.Message}]";
            }
        }

        private bool ShouldLogBody(string? contentType)
        {
            if (string.IsNullOrEmpty(contentType) || _options?.RequestResponseLogging?.IncludeContentTypes == null)
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
                    result[key] = headers[key] ?? string.Empty;
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
            var headers = new[] { "X-Forwarded-For", "X-Real-IP", "CF-Connecting-IP", "X-Client-IP" };

            foreach (var header in headers)
            {
                var value = request.Headers[header];
                if (!string.IsNullOrEmpty(value))
                {
                    return value.Split(',')[0].Trim();
                }
            }

            return request.UserHostAddress ?? string.Empty;
        }

        private string GetHttpVersion(HttpRequest request)
        {
            var serverVars = request.ServerVariables;
            var protocol = serverVars["SERVER_PROTOCOL"];

            if (string.IsNullOrEmpty(protocol))
                return "1.1";

            return protocol.Replace("HTTP/", "");
        }

        private string? ExtractRouteTemplate(object routeData)
        {
            try
            {
                if (routeData.GetType().Name == "HttpRouteData")
                {
                    var route = routeData.GetType().GetProperty("Route")?.GetValue(routeData);
                    var routeTemplate = route?.GetType().GetProperty("RouteTemplate")?.GetValue(route) as string;
                    return routeTemplate;
                }

                var routeDataDict = routeData as System.Collections.IDictionary;
                if (routeDataDict != null && routeDataDict.Contains("__route__"))
                {
                    return routeDataDict["__route__"] as string;
                }
            }
            catch
            {
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
            if (string.IsNullOrEmpty(path))
                return "/";

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
        }

        private void OnPostRequestHandlerExecute(object sender, EventArgs e)
        {
        }
    }

    /// <summary>
    /// HTTP Response filter to capture response body for logging in .NET Framework
    /// </summary>
    public class ResponseFilterStream : Stream
    {
        private readonly Stream _originalFilter;
        private readonly HttpContext _context;
        private readonly MemoryStream _captureStream;

        public ResponseFilterStream(Stream originalFilter, HttpContext context)
        {
            _originalFilter = originalFilter;
            _context = context;
            _captureStream = new MemoryStream();

            System.Diagnostics.Debug.WriteLine($"[ResponseCapturingFilter] Created for {context.Request.HttpMethod} {context.Request.Url?.AbsolutePath}");
        }

        public override bool CanRead => _originalFilter.CanRead;
        public override bool CanSeek => _originalFilter.CanSeek;
        public override bool CanWrite => _originalFilter.CanWrite;
        public override long Length => _originalFilter.Length;

        public override long Position
        {
            get => _originalFilter.Position;
            set => _originalFilter.Position = value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _originalFilter.Write(buffer, offset, count);

            if (_captureStream.Length < 32768)
            {
                var bytesToCapture = Math.Min(count, (int)(32768 - _captureStream.Length));
                _captureStream.Write(buffer, offset, bytesToCapture);

                var contentPreview = Encoding.UTF8.GetString(buffer, offset, Math.Min(bytesToCapture, 100));
                System.Diagnostics.Debug.WriteLine($"[ResponseFilter] Wrote {bytesToCapture} bytes: {contentPreview}...");

                StoreCurrentCapture();
            }
        }

        public override void Flush()
        {
            StoreCurrentCapture();
            _originalFilter.Flush();
        }

        private void StoreCurrentCapture()
        {
            if (_captureStream.Length > 0)
            {
                try
                {
                    var currentPosition = _captureStream.Position;
                    _captureStream.Position = 0;

                    using (var reader = new StreamReader(_captureStream, Encoding.UTF8, false, 1024, true))
                    {
                        var capturedContent = reader.ReadToEnd();
                        if (!string.IsNullOrEmpty(capturedContent))
                        {
                            _context.Items["CapturedResponseBody"] = capturedContent;
                        }
                    }

                    _captureStream.Position = currentPosition;
                }
                catch (Exception ex)
                {
                    _context.Items["CapturedResponseBody"] = $"[Capture error: {ex.Message}]";
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _originalFilter.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _originalFilter.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _originalFilter.SetLength(value);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    _captureStream?.Dispose();
                    _originalFilter?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
#endif