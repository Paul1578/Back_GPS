using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace fletflow.Api.Middleware
{
    public class HeaderValidationMiddleware : IMiddleware
    {
        private readonly ILogger<HeaderValidationMiddleware> _logger;
        public HeaderValidationMiddleware(ILogger<HeaderValidationMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            // Sólo exigir Content-Type cuando la petición realmente lleva cuerpo.
            // PUT sin body es válido (por ejemplo, desasignar un vehículo).
            var hasBody = context.Request.ContentLength is > 0 ||
                context.Request.Headers.ContainsKey("Transfer-Encoding");
            if (hasBody && (context.Request.Method == HttpMethods.Post || context.Request.Method == HttpMethods.Put || context.Request.Method == HttpMethods.Patch))
            {
                if (!Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var contentType) ||
                    !string.Equals(contentType.MediaType.Value, "application/json", System.StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("[HeaderValidation] Content-Type inválido: {ContentType}", context.Request.ContentType);
                    context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                    await context.Response.WriteAsync("Content-Type debe ser application/json");
                    return;
                }
            }
            // Ejemplo: puedes agregar más validaciones de cabeceras aquí
            await next(context);
        }
    }
}
