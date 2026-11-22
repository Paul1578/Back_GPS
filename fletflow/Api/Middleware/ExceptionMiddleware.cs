// fletflow/Api/Middleware/ExceptionMiddleware.cs
using System.Net;
using System.Text.Json;

namespace fletflow.Api.Middleware
{
    public class ExceptionMiddleware : IMiddleware
    {
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(ILogger<ExceptionMiddleware> logger)
        {
            _logger = logger;
        }

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

                _logger.LogError(ex, "[ExceptionMiddleware] Error procesando la solicitud. TraceId: {TraceId}, Path: {Path}, Status: {Status}",
                    context.TraceIdentifier, context.Request.Path, (int)status);

                var payload = new
                {
                    traceId = context.TraceIdentifier,
                    status = (int)status,
                    error = status.ToString(),
                    message = status == HttpStatusCode.InternalServerError
                        ? "Ocurrió un error interno. Comparte el identificador de seguimiento con soporte."
                        : ex.Message
                };

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)status;
                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
        }
    }
}
