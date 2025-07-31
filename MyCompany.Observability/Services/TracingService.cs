using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using MyCompany.Observability.Configuration;

namespace MyCompany.Observability.Services
{
    public interface ITracingService
    {
        Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal);
        Activity? StartActivity(string name, ActivityKind kind, ActivityContext parentContext);
        Activity? StartActivity(string name, ActivityKind kind, string parentId);
        void AddTag(Activity? activity, string key, object? value);
        void AddEvent(Activity? activity, string name, Dictionary<string, object?>? attributes = null);
        void SetStatus(Activity? activity, ActivityStatusCode status, string? description = null);
        void RecordException(Activity? activity, Exception exception);
    }

    public class TracingService : ITracingService
    {
        private readonly ActivitySource _activitySource;
        private readonly ObservabilityOptions _options;

        public TracingService(ActivitySource activitySource, ObservabilityOptions options)
        {
            _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        {
            var activity = _activitySource.StartActivity(name, kind);
            EnrichActivity(activity);
            return activity;
        }

        public Activity? StartActivity(string name, ActivityKind kind, ActivityContext parentContext)
        {
            var activity = _activitySource.StartActivity(name, kind, parentContext);
            EnrichActivity(activity);
            return activity;
        }

        public Activity? StartActivity(string name, ActivityKind kind, string parentId)
        {
            var activity = _activitySource.StartActivity(name, kind, parentId);
            EnrichActivity(activity);
            return activity;
        }

        public void AddTag(Activity? activity, string key, object? value)
        {
            if (activity != null && !string.IsNullOrEmpty(key) && value != null)
            {
                activity.SetTag(key, value.ToString());
            }
        }

        public void AddEvent(Activity? activity, string name, Dictionary<string, object?>? attributes = null)
        {
            if (activity != null && !string.IsNullOrEmpty(name))
            {
                if (attributes == null || attributes.Count == 0)
                {
                    activity.AddEvent(new ActivityEvent(name));
                }
                else
                {
                    var activityTags = new List<KeyValuePair<string, object?>>();
                    foreach (var attr in attributes)
                    {
                        if (!string.IsNullOrEmpty(attr.Key))
                        {
                            activityTags.Add(new KeyValuePair<string, object?>(attr.Key, attr.Value));
                        }
                    }
                    activity.AddEvent(new ActivityEvent(name, DateTimeOffset.UtcNow, new ActivityTagsCollection(activityTags)));
                }
            }
        }

        public void SetStatus(Activity? activity, ActivityStatusCode status, string? description = null)
        {
            if (activity != null)
            {
                activity.SetStatus(status, description);
            }
        }

        public void RecordException(Activity? activity, Exception exception)
        {
            if (activity != null && exception != null)
            {
                var attributes = new Dictionary<string, object?>
                {
                    ["exception.type"] = exception.GetType().FullName,
                    ["exception.message"] = exception.Message,
                    ["exception.stacktrace"] = exception.StackTrace
                };

                AddEvent(activity, "exception", attributes);
                SetStatus(activity, ActivityStatusCode.Error, exception.Message);
            }
        }

        private void EnrichActivity(Activity? activity)
        {
            if (activity == null) return;

            // Add service information
            activity.SetTag("service.name", _options.ServiceName);
            activity.SetTag("service.version", _options.ServiceVersion);
            
            if (!string.IsNullOrEmpty(_options.ServiceNamespace))
                activity.SetTag("service.namespace", _options.ServiceNamespace);
                
            if (!string.IsNullOrEmpty(_options.ServiceInstanceId))
                activity.SetTag("service.instance.id", _options.ServiceInstanceId);

            // Add custom service attributes
            foreach (var attribute in _options.ServiceAttributes)
            {
                activity.SetTag($"service.{attribute.Key}", attribute.Value);
            }
        }
    }

    public static class TracingServiceExtensions
    {
        public static async Task<T> TraceAsync<T>(this ITracingService tracingService, string operationName, 
            Func<Activity?, Task<T>> operation, ActivityKind kind = ActivityKind.Internal)
        {
            using var activity = tracingService.StartActivity(operationName, kind);
            try
            {
                var result = await operation(activity);
                tracingService.SetStatus(activity, ActivityStatusCode.Ok);
                return result;
            }
            catch (Exception ex)
            {
                tracingService.RecordException(activity, ex);
                throw;
            }
        }

        public static async Task TraceAsync(this ITracingService tracingService, string operationName, 
            Func<Activity?, Task> operation, ActivityKind kind = ActivityKind.Internal)
        {
            using var activity = tracingService.StartActivity(operationName, kind);
            try
            {
                await operation(activity);
                tracingService.SetStatus(activity, ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                tracingService.RecordException(activity, ex);
                throw;
            }
        }

        public static T Trace<T>(this ITracingService tracingService, string operationName, 
            Func<Activity?, T> operation, ActivityKind kind = ActivityKind.Internal)
        {
            using var activity = tracingService.StartActivity(operationName, kind);
            try
            {
                var result = operation(activity);
                tracingService.SetStatus(activity, ActivityStatusCode.Ok);
                return result;
            }
            catch (Exception ex)
            {
                tracingService.RecordException(activity, ex);
                throw;
            }
        }

        public static void Trace(this ITracingService tracingService, string operationName, 
            Action<Activity?> operation, ActivityKind kind = ActivityKind.Internal)
        {
            using var activity = tracingService.StartActivity(operationName, kind);
            try
            {
                operation(activity);
                tracingService.SetStatus(activity, ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                tracingService.RecordException(activity, ex);
                throw;
            }
        }
    }
}