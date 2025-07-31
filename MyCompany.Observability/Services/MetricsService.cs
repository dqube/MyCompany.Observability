using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using MyCompany.Observability.Configuration;

namespace MyCompany.Observability.Services
{
    public interface IMetricsService
    {
        void IncrementCounter(string name, double value = 1, params KeyValuePair<string, object?>[] tags);
        void RecordHistogram(string name, double value, params KeyValuePair<string, object?>[] tags);
        void SetGauge(string name, double value, params KeyValuePair<string, object?>[] tags);
        void IncrementUpDownCounter(string name, double value = 1, params KeyValuePair<string, object?>[] tags);
        
        // Convenience methods for common metrics
        void RecordRequestDuration(double durationMs, string method, string endpoint, int statusCode);
        void IncrementRequestCount(string method, string endpoint, int statusCode);
        void RecordErrorCount(string errorType, string operation);
        void SetActiveConnections(int count);
        void RecordDatabaseQueryDuration(double durationMs, string operation, string table);
        
        // HTTP Server metrics for .NET Framework
        void IncrementActiveRequests();
        void DecrementActiveRequests();
    }

    public class MetricsService : IMetricsService
    {
        private readonly Meter _meter;
        private readonly ObservabilityOptions _options;
        
        // Common metric instruments
        private readonly Counter<double> _requestCounter;
        private readonly Histogram<double> _requestDuration;
        private readonly Counter<double> _errorCounter;
        private readonly UpDownCounter<int> _activeConnections;
        private readonly UpDownCounter<int> _activeRequests;
        private readonly Histogram<double> _databaseQueryDuration;
        
        // Custom metric instruments cache
        private readonly Dictionary<string, Counter<double>> _counters = new();
        private readonly Dictionary<string, Histogram<double>> _histograms = new();
        private readonly Dictionary<string, UpDownCounter<double>> _upDownCounters = new();
        private readonly Dictionary<string, ObservableGauge<double>> _gauges = new();
        private readonly Dictionary<string, Func<double>> _gaugeCallbacks = new();

        public MetricsService(Meter meter, ObservabilityOptions options)
        {
            _meter = meter ?? throw new ArgumentNullException(nameof(meter));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            // Initialize common metrics
            _requestCounter = _meter.CreateCounter<double>(
                "http_requests_total",
                description: "Total number of HTTP requests");

            _requestDuration = _meter.CreateHistogram<double>(
                "http_request_duration_ms",
                unit: "ms",
                description: "Duration of HTTP requests in milliseconds");

            _errorCounter = _meter.CreateCounter<double>(
                "errors_total",
                description: "Total number of errors");

            _activeConnections = _meter.CreateUpDownCounter<int>(
                "active_connections",
                description: "Number of active connections");

            _activeRequests = _meter.CreateUpDownCounter<int>(
                "http_server_active_requests",
                description: "Number of active HTTP requests");

            _databaseQueryDuration = _meter.CreateHistogram<double>(
                "database_query_duration_ms",
                unit: "ms",
                description: "Duration of database queries in milliseconds");
        }

        public void IncrementCounter(string name, double value = 1, params KeyValuePair<string, object?>[] tags)
        {
            if (string.IsNullOrEmpty(name)) return;

            if (!_counters.TryGetValue(name, out var counter))
            {
                counter = _meter.CreateCounter<double>(name);
                _counters[name] = counter;
            }

            var enrichedTags = EnrichTags(tags);
            counter.Add(value, enrichedTags);
        }

        public void RecordHistogram(string name, double value, params KeyValuePair<string, object?>[] tags)
        {
            if (string.IsNullOrEmpty(name)) return;

            if (!_histograms.TryGetValue(name, out var histogram))
            {
                histogram = _meter.CreateHistogram<double>(name);
                _histograms[name] = histogram;
            }

            var enrichedTags = EnrichTags(tags);
            histogram.Record(value, enrichedTags);
        }

        public void SetGauge(string name, double value, params KeyValuePair<string, object?>[] tags)
        {
            if (string.IsNullOrEmpty(name)) return;

            // Store the value for the gauge callback
            var key = $"{name}_{string.Join("_", Array.ConvertAll(tags, t => $"{t.Key}={t.Value}"))}";
            _gaugeCallbacks[key] = () => value;

            if (!_gauges.ContainsKey(name))
            {
                var gauge = _meter.CreateObservableGauge<double>(name, () => 
                {
                    var measurements = new List<Measurement<double>>();
                    foreach (var kvp in _gaugeCallbacks)
                    {
                        if (kvp.Key.StartsWith(name))
                        {
                            measurements.Add(new Measurement<double>(kvp.Value(), EnrichTags(tags)));
                        }
                    }
                    return measurements;
                });
                _gauges[name] = gauge;
            }
        }

        public void IncrementUpDownCounter(string name, double value = 1, params KeyValuePair<string, object?>[] tags)
        {
            if (string.IsNullOrEmpty(name)) return;

            if (!_upDownCounters.TryGetValue(name, out var counter))
            {
                counter = _meter.CreateUpDownCounter<double>(name);
                _upDownCounters[name] = counter;
            }

            var enrichedTags = EnrichTags(tags);
            counter.Add(value, enrichedTags);
        }

        public void RecordRequestDuration(double durationMs, string method, string endpoint, int statusCode)
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new("http.method", method),
                new("http.route", endpoint),
                new("http.status_code", statusCode),
                new("http.status_class", GetStatusClass(statusCode))
            };

            _requestDuration.Record(durationMs, EnrichTags(tags));
        }

        public void IncrementRequestCount(string method, string endpoint, int statusCode)
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new("http.method", method),
                new("http.route", endpoint),
                new("http.status_code", statusCode),
                new("http.status_class", GetStatusClass(statusCode))
            };

            _requestCounter.Add(1, EnrichTags(tags));
        }

        public void RecordErrorCount(string errorType, string operation)
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new("error.type", errorType),
                new("operation", operation)
            };

            _errorCounter.Add(1, EnrichTags(tags));
        }

        public void SetActiveConnections(int count)
        {
            _activeConnections.Add(count - GetCurrentActiveConnections());
        }

        public void RecordDatabaseQueryDuration(double durationMs, string operation, string table)
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new("db.operation", operation),
                new("db.table", table)
            };

            _databaseQueryDuration.Record(durationMs, EnrichTags(tags));
        }

        public void IncrementActiveRequests()
        {
            _activeRequests.Add(1);
        }

        public void DecrementActiveRequests()
        {
            _activeRequests.Add(-1);
        }

        private KeyValuePair<string, object?>[] EnrichTags(KeyValuePair<string, object?>[] originalTags)
        {
            var enrichedTags = new List<KeyValuePair<string, object?>>(originalTags)
            {
                new("service.name", _options.ServiceName),
                new("service.version", _options.ServiceVersion)
            };

            if (!string.IsNullOrEmpty(_options.ServiceNamespace))
                enrichedTags.Add(new("service.namespace", _options.ServiceNamespace));

            if (!string.IsNullOrEmpty(_options.ServiceInstanceId))
                enrichedTags.Add(new("service.instance.id", _options.ServiceInstanceId));

            // Add custom service attributes
            foreach (var attribute in _options.ServiceAttributes)
            {
                enrichedTags.Add(new($"service.{attribute.Key}", attribute.Value));
            }

            return enrichedTags.ToArray();
        }

        private static string GetStatusClass(int statusCode)
        {
            return statusCode switch
            {
                >= 100 and < 200 => "1xx",
                >= 200 and < 300 => "2xx",
                >= 300 and < 400 => "3xx",
                >= 400 and < 500 => "4xx",
                >= 500 => "5xx",
                _ => "unknown"
            };
        }

        private int GetCurrentActiveConnections()
        {
            // This is a simplified implementation - in a real scenario,
            // you'd track this state more accurately
            return 0;
        }
    }

    public static class MetricsServiceExtensions
    {
        public static IDisposable MeasureTime(this IMetricsService metricsService, string metricName, params KeyValuePair<string, object?>[] tags)
        {
            return new TimeMeasurement(metricsService, metricName, tags);
        }

        public static IDisposable MeasureRequestTime(this IMetricsService metricsService, string method, string endpoint)
        {
            return new RequestTimeMeasurement(metricsService, method, endpoint);
        }
    }

    internal class TimeMeasurement : IDisposable
    {
        private readonly IMetricsService _metricsService;
        private readonly string _metricName;
        private readonly KeyValuePair<string, object?>[] _tags;
        private readonly DateTime _startTime;

        public TimeMeasurement(IMetricsService metricsService, string metricName, KeyValuePair<string, object?>[] tags)
        {
            _metricsService = metricsService;
            _metricName = metricName;
            _tags = tags;
            _startTime = DateTime.UtcNow;
        }

        public void Dispose()
        {
            var duration = (DateTime.UtcNow - _startTime).TotalMilliseconds;
            _metricsService.RecordHistogram(_metricName, duration, _tags);
        }
    }

    internal class RequestTimeMeasurement : IDisposable
    {
        private readonly IMetricsService _metricsService;
        private readonly string _method;
        private readonly string _endpoint;
        private readonly DateTime _startTime;

        public RequestTimeMeasurement(IMetricsService metricsService, string method, string endpoint)
        {
            _metricsService = metricsService;
            _method = method;
            _endpoint = endpoint;
            _startTime = DateTime.UtcNow;
        }

        public void Dispose()
        {
            var duration = (DateTime.UtcNow - _startTime).TotalMilliseconds;
            // Note: Status code would need to be set externally or captured from context
            _metricsService.RecordRequestDuration(duration, _method, _endpoint, 200);
        }
    }
}