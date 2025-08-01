using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http.Filters;

namespace WebApplication1.Filters
{
    /// <summary>
    /// Web API action filter to capture response content for logging
    /// </summary>
    public class ResponseCaptureFilter : ActionFilterAttribute
    {
        public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {
            base.OnActionExecuted(actionExecutedContext);
            
            if (actionExecutedContext?.Response?.Content != null)
            {
                try
                {
                    // Capture the response content
                    var contentTask = actionExecutedContext.Response.Content.ReadAsStringAsync();
                    contentTask.Wait(); // Safe to wait here as this is already on a background thread
                    var responseContent = contentTask.Result;
                    
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        // Store the captured response in HttpContext for the module to access
                        var httpContext = HttpContext.Current;
                        if (httpContext != null)
                        {
                            httpContext.Items["CapturedResponseBody"] = responseContent;
                            httpContext.Items["ResponseContentType"] = actionExecutedContext.Response.Content.Headers?.ContentType?.MediaType ?? "application/json";
                            
                            // Debug logging
                            System.Diagnostics.Debug.WriteLine($"[ResponseCaptureFilter] Captured {responseContent.Length} chars of response content");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Store error for debugging
                    var httpContext = HttpContext.Current;
                    if (httpContext != null)
                    {
                        httpContext.Items["CapturedResponseBody"] = $"[ResponseCaptureFilter error: {ex.Message}]";
                    }
                    System.Diagnostics.Debug.WriteLine($"[ResponseCaptureFilter] Error capturing response: {ex.Message}");
                }
            }
        }
    }
}