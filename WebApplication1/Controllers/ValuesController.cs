using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MyCompany.Observability.Framework;

namespace WebApplication1.Controllers
{
    public class ValuesController : ApiController
    {
        private readonly ILogger<ValuesController> _logger;

        public ValuesController()
        {
            // Get logger from the service provider configured in Global.asax
            _logger =  LoggerFactoryProvider.LoggerFactory.CreateLogger<ValuesController>();
        }
        // GET api/values
        public IEnumerable<string> Get()
        {
            _logger.LogInformation("Starting to process GET request for all values");
            
            // Demonstrate that Activity.Current is available and working
            var currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                _logger.LogInformation("Activity.Current is available with ID: {ActivityId}", currentActivity.Id);
                currentActivity.AddTag("custom.operation", "get_all_values");
                currentActivity.AddEvent(new ActivityEvent("Processing GET request for all values"));
            }
            else
            {
                _logger.LogWarning("Activity.Current is null - tracing may not be working properly");
            }

            var result = new string[] { "value1", "value2" };
            _logger.LogInformation("Returning {Count} values", result.Length);
            
            return result;
        }

        // GET api/values/5
        public string Get(int id)
        {
            _logger.LogInformation("Starting to process GET request for value ID: {ValueId}", id);
            
            var currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                _logger.LogInformation("Activity.Current is available with ID: {ActivityId}", currentActivity.Id);
                currentActivity.AddTag("custom.operation", "get_value_by_id");
                currentActivity.AddTag("custom.value_id", id);
                currentActivity.AddEvent(new ActivityEvent($"Processing GET request for value ID: {id}"));
            }
            else
            {
                _logger.LogWarning("Activity.Current is null for GET request with ID: {ValueId}", id);
            }

            if (id == 999)
            {
                _logger.LogError("Invalid ID requested: {ValueId}", id);
                // Test exception handling and tracing
                throw new ArgumentException($"Invalid ID: {id}");
            }

            var result = $"value{id}";
            _logger.LogInformation("Returning value for ID {ValueId}: {Result}", id, result);
            
            return result;
        }

        // GET api/values/slow
        [HttpGet]
        [Route("api/values/slow")]
        public async Task<string> GetSlow()
        {
            _logger.LogInformation("Starting slow operation");
            
            var currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                _logger.LogInformation("Activity.Current is available for slow operation with ID: {ActivityId}", currentActivity.Id);
                currentActivity.AddTag("custom.operation", "slow_operation");
                currentActivity.AddEvent(new ActivityEvent("Starting slow operation"));
            }
            else
            {
                _logger.LogWarning("Activity.Current is null for slow operation");
            }

            // Simulate slow operation
            _logger.LogInformation("Simulating 2-second delay");
            await Task.Delay(2000);

            if (currentActivity != null)
            {
                currentActivity.AddEvent(new ActivityEvent("Completed slow operation"));
                _logger.LogInformation("Added completion event to activity");
            }

            _logger.LogInformation("Slow operation completed, returning result");
            return "slow_result";
        }

        // POST api/values
        public void Post([FromBody] string value)
        {
            _logger.LogInformation("Starting POST request with value: {Value}", value ?? "null");
            
            var currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                _logger.LogInformation("Activity.Current is available for POST with ID: {ActivityId}", currentActivity.Id);
                currentActivity.AddTag("custom.operation", "post_value");
                currentActivity.AddTag("custom.value_length", value?.Length ?? 0);
                currentActivity.AddEvent(new ActivityEvent("Processing POST request"));
            }
            else
            {
                _logger.LogWarning("Activity.Current is null for POST request");
            }
            
            _logger.LogInformation("POST request completed");
        }

        // PUT api/values/5
        public void Put(int id, [FromBody] string value)
        {
            _logger.LogInformation("Starting PUT request for ID: {ValueId} with value: {Value}", id, value ?? "null");
            
            var currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                _logger.LogInformation("Activity.Current is available for PUT with ID: {ActivityId}", currentActivity.Id);
                currentActivity.AddTag("custom.operation", "put_value");
                currentActivity.AddTag("custom.value_id", id);
                currentActivity.AddTag("custom.value_length", value?.Length ?? 0);
                currentActivity.AddEvent(new ActivityEvent($"Processing PUT request for ID: {id}"));
            }
            else
            {
                _logger.LogWarning("Activity.Current is null for PUT request with ID: {ValueId}", id);
            }
            
            _logger.LogInformation("PUT request completed for ID: {ValueId}", id);
        }

        // DELETE api/values/5
        public void Delete(int id)
        {
            _logger.LogInformation("Starting DELETE request for ID: {ValueId}", id);
            
            var currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                _logger.LogInformation("Activity.Current is available for DELETE with ID: {ActivityId}", currentActivity.Id);
                currentActivity.AddTag("custom.operation", "delete_value");
                currentActivity.AddTag("custom.value_id", id);
                currentActivity.AddEvent(new ActivityEvent($"Processing DELETE request for ID: {id}"));
            }
            else
            {
                _logger.LogWarning("Activity.Current is null for DELETE request with ID: {ValueId}", id);
            }
            
            _logger.LogInformation("DELETE request completed for ID: {ValueId}", id);
        }
    }
}
