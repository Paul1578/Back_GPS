// fletflow/Api/Middleware/ExceptionMiddleware.cs
using System.Net;
using System.Text.Json;

namespace fletflow.Api.Middleware
{
    public class ExceptionMiddleware : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                var status = ex switch
                {
                    ArgumentException or InvalidOperationException => HttpStatusCode.BadRequest,
                    UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                    KeyNotFoundException => HttpStatusCode.NotFound,
                    _ => HttpStatusCode.InternalServerError
                };

                var payload = new
                {
                    traceId = context.TraceIdentifier,
                    status = (int)status,
                    error = status.ToString(),
                    message = ex.Message
                };

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)status;
                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
        }
    }
}
