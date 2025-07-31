using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using MyCompany.Observability.Services;
using WebApplication1.App_Start;
using WebApplication1.Infrastructure;

namespace WebApplication1.Controllers
{
    public class ValuesController : ApiController
    {
        private readonly ISimpleLogger _logger;
        private readonly ITracingService _tracingService;
        private readonly IMetricsService _metricsService;

        public ValuesController()
        {
            // Get services from the simplified observability config
            _logger = SimpleObservabilityConfig.GetLogger<ValuesController>();
            _tracingService = SimpleObservabilityConfig.GetTracingService();
            _metricsService = SimpleObservabilityConfig.GetMetricsService();
        }

        // GET api/values
        public IEnumerable<string> Get()
        {
            var activity = _tracingService?.StartActivity("GET api/values", ActivityKind.Server);
            try
            {
                _logger?.LogInformation("Getting all values");
                _tracingService?.AddTag(activity, "operation", "get_all_values");
                var values = new string[] { "value1", "value2" };
                _metricsService?.IncrementCounter("api_requests_total", 1, new[]
                {
                    new KeyValuePair<string, object>("method", "GET"),
                    new KeyValuePair<string, object>("endpoint", "api/values"),
                    new KeyValuePair<string, object>("status", "success")
                });
                _tracingService?.AddTag(activity, "result_count", values.Length);
                _tracingService?.SetStatus(activity, ActivityStatusCode.Ok);
                _logger?.LogInformation("Successfully returned {Count} values", values.Length);
                return values;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting values");
                _tracingService?.RecordException(activity, ex);
                _metricsService?.RecordErrorCount("Exception", "get_all_values");
                throw;
            }
            finally { activity?.Dispose(); }
        }

        // GET api/values/5
        public string Get(int id)
        {
            var activity = _tracingService?.StartActivity($"GET api/values/{id}", ActivityKind.Server);
            try
            {
                _logger?.LogInformation("Getting value for ID: {Id}", id);
                _tracingService?.AddTag(activity, "operation", "get_value_by_id");
                _tracingService?.AddTag(activity, "value_id", id);
                if (id <= 0)
                {
                    _logger?.LogWarning("Invalid ID provided: {Id}", id);
                    _tracingService?.AddTag(activity, "error", "invalid_id");
                    _tracingService?.SetStatus(activity, ActivityStatusCode.Error, "Invalid ID");
                    _metricsService?.IncrementCounter("api_requests_total", 1, new[]
                    {
                        new KeyValuePair<string, object>("method", "GET"),
                        new KeyValuePair<string, object>("endpoint", "api/values/{id}"),
                        new KeyValuePair<string, object>("status", "error")
                    });
                    throw new ArgumentException("ID must be positive", nameof(id));
                }
                var result = $"value{id}";
                _metricsService?.IncrementCounter("api_requests_total", 1, new[]
                {
                    new KeyValuePair<string, object>("method", "GET"),
                    new KeyValuePair<string, object>("endpoint", "api/values/{id}"),
                    new KeyValuePair<string, object>("status", "success")
                });
                _tracingService?.AddTag(activity, "result", result);
                _tracingService?.SetStatus(activity, ActivityStatusCode.Ok);
                _logger?.LogInformation("Successfully returned value for ID {Id}: {Result}", id, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting value for ID: {Id}", id);
                _tracingService?.RecordException(activity, ex);
                _metricsService?.RecordErrorCount("Exception", "get_value_by_id");
                throw;
            }
            finally { activity?.Dispose(); }
        }

        // POST api/values
        public IHttpActionResult Post([FromBody] string value)
        {
            var activity = _tracingService?.StartActivity("POST api/values", ActivityKind.Server);
            try
            {
                _logger?.LogInformation("Creating new value: {Value}", value);
                _tracingService?.AddTag(activity, "operation", "create_value");
                _tracingService?.AddTag(activity, "value_length", value?.Length ?? 0);
                if (string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogWarning("Empty value provided for creation");
                    _tracingService?.AddTag(activity, "error", "empty_value");
                    _tracingService?.SetStatus(activity, ActivityStatusCode.Error, "Empty value");
                    _metricsService?.IncrementCounter("api_requests_total", 1, new[]
                    {
                        new KeyValuePair<string, object>("method", "POST"),
                        new KeyValuePair<string, object>("endpoint", "api/values"),
                        new KeyValuePair<string, object>("status", "bad_request")
                    });
                    return BadRequest("Value cannot be empty");
                }
                // Simulate some processing
                _tracingService?.AddEvent(activity, "processing_value", new Dictionary<string, object>
                {
                    ["timestamp"] = DateTimeOffset.UtcNow,
                    ["value_preview"] = value.Length > 10 ? value.Substring(0, 10) + "..." : value
                });
                _metricsService?.IncrementCounter("api_requests_total", 1, new[]
                {
                    new KeyValuePair<string, object>("method", "POST"),
                    new KeyValuePair<string, object>("endpoint", "api/values"),
                    new KeyValuePair<string, object>("status", "success")
                });
                _tracingService?.SetStatus(activity, ActivityStatusCode.Ok);
                _logger?.LogInformation("Successfully created value");
                return Ok(new { message = "Value created successfully", id = new Random().Next(1, 1000) });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating value");
                _tracingService?.RecordException(activity, ex);
                _metricsService?.RecordErrorCount("Exception", "create_value");
                return InternalServerError(ex);
            }
            finally { activity?.Dispose(); }
        }

        // PUT api/values/5
        public IHttpActionResult Put(int id, [FromBody] string value)
        {
            var activity = _tracingService?.StartActivity($"PUT api/values/{id}", ActivityKind.Server);
            try
            {
                _logger?.LogInformation("Updating value for ID: {Id} with value: {Value}", id, value);
                _tracingService?.AddTag(activity, "operation", "update_value");
                _tracingService?.AddTag(activity, "value_id", id);
                _tracingService?.AddTag(activity, "value_length", value?.Length ?? 0);
                if (id <= 0)
                {
                    _logger?.LogWarning("Invalid ID provided for update: {Id}", id);
                    _tracingService?.AddTag(activity, "error", "invalid_id");
                    _tracingService?.SetStatus(activity, ActivityStatusCode.Error, "Invalid ID");
                    _metricsService?.IncrementCounter("api_requests_total", 1, new[]
                    {
                        new KeyValuePair<string, object>("method", "PUT"),
                        new KeyValuePair<string, object>("endpoint", "api/values/{id}"),
                        new KeyValuePair<string, object>("status", "bad_request")
                    });
                    return BadRequest("ID must be positive");
                }
                _metricsService?.IncrementCounter("api_requests_total", 1, new[]
                {
                    new KeyValuePair<string, object>("method", "PUT"),
                    new KeyValuePair<string, object>("endpoint", "api/values/{id}"),
                    new KeyValuePair<string, object>("status", "success")
                });
                _tracingService?.SetStatus(activity, ActivityStatusCode.Ok);
                _logger?.LogInformation("Successfully updated value for ID {Id}", id);
                return Ok(new { message = "Value updated successfully", id = id });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating value for ID: {Id}", id);
                _tracingService?.RecordException(activity, ex);
                _metricsService?.RecordErrorCount("Exception", "update_value");
                return InternalServerError(ex);
            }
            finally { activity?.Dispose(); }
        }

        // DELETE api/values/5
        public IHttpActionResult Delete(int id)
        {
            var activity = _tracingService?.StartActivity($"DELETE api/values/{id}", ActivityKind.Server);
            try
            {
                _logger?.LogInformation("Deleting value for ID: {Id}", id);
                _tracingService?.AddTag(activity, "operation", "delete_value");
                _tracingService?.AddTag(activity, "value_id", id);
                if (id <= 0)
                {
                    _logger?.LogWarning("Invalid ID provided for deletion: {Id}", id);
                    _tracingService?.AddTag(activity, "error", "invalid_id");
                    _tracingService?.SetStatus(activity, ActivityStatusCode.Error, "Invalid ID");
                    _metricsService?.IncrementCounter("api_requests_total", 1, new[]
                    {
                        new KeyValuePair<string, object>("method", "DELETE"),
                        new KeyValuePair<string, object>("endpoint", "api/values/{id}"),
                        new KeyValuePair<string, object>("status", "bad_request")
                    });
                    return BadRequest("ID must be positive");
                }
                _metricsService?.IncrementCounter("api_requests_total", 1, new[]
                {
                    new KeyValuePair<string, object>("method", "DELETE"),
                    new KeyValuePair<string, object>("endpoint", "api/values/{id}"),
                    new KeyValuePair<string, object>("status", "success")
                });
                _tracingService?.SetStatus(activity, ActivityStatusCode.Ok);
                _logger?.LogInformation("Successfully deleted value for ID {Id}", id);
                return Ok(new { message = "Value deleted successfully", id = id });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting value for ID: {Id}", id);
                _tracingService?.RecordException(activity, ex);
                _metricsService?.RecordErrorCount("Exception", "delete_value");
                return InternalServerError(ex);
            }
            finally { activity?.Dispose(); }
        }
    }
}
