using System;
using System.Collections.Generic;
using System.Web.Http;
using WebApplication1.App_Start;
using WebApplication1.Infrastructure;

namespace WebApplication1.Controllers
{
    public class ValuesController : ApiController
    {
        private readonly ISimpleLogger _logger;

        public ValuesController()
        {
            // Get logger from the simplified observability config
            _logger = SimpleObservabilityConfig.GetLogger<ValuesController>();
        }

        // GET api/values
        public IEnumerable<string> Get()
        {
            try
            {
                _logger?.LogInformation("Getting all values");
                var values = new string[] { "value1", "value2" };
                _logger?.LogInformation("Successfully returned {Count} values", values.Length);
                return values;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting values");
                throw;
            }
        }

        // GET api/values/5
        public string Get(int id)
        {
            try
            {
                _logger?.LogInformation("Getting value for ID: {Id}", id);
                if (id <= 0)
                {
                    _logger?.LogWarning("Invalid ID provided: {Id}", id);
                    throw new ArgumentException("ID must be positive", nameof(id));
                }
                var result = $"value{id}";
                _logger?.LogInformation("Successfully returned value for ID {Id}: {Result}", id, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting value for ID: {Id}", id);
                throw;
            }
        }

        // POST api/values
        public IHttpActionResult Post([FromBody] string value)
        {
            try
            {
                _logger?.LogInformation("Creating new value: {Value}", value);
                if (string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogWarning("Empty value provided for creation");
                    return BadRequest("Value cannot be empty");
                }
                _logger?.LogInformation("Successfully created value");
                return Ok(new { message = "Value created successfully", id = new Random().Next(1, 1000) });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating value");
                return InternalServerError(ex);
            }
        }

        // PUT api/values/5
        public IHttpActionResult Put(int id, [FromBody] string value)
        {
            try
            {
                _logger?.LogInformation("Updating value for ID: {Id} with value: {Value}", id, value);
                if (id <= 0)
                {
                    _logger?.LogWarning("Invalid ID provided for update: {Id}", id);
                    return BadRequest("ID must be positive");
                }
                _logger?.LogInformation("Successfully updated value for ID {Id}", id);
                return Ok(new { message = "Value updated successfully", id = id });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating value for ID: {Id}", id);
                return InternalServerError(ex);
            }
        }

        // DELETE api/values/5
        public IHttpActionResult Delete(int id)
        {
            try
            {
                _logger?.LogInformation("Deleting value for ID: {Id}", id);
                if (id <= 0)
                {
                    _logger?.LogWarning("Invalid ID provided for deletion: {Id}", id);
                    return BadRequest("ID must be positive");
                }
                _logger?.LogInformation("Successfully deleted value for ID {Id}", id);
                return Ok(new { message = "Value deleted successfully", id = id });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting value for ID: {Id}", id);
                return InternalServerError(ex);
            }
        }
    }
}
