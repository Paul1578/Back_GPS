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
            // Solo valida para métodos que suelen llevar body
            if (context.Request.Method == HttpMethods.Post || context.Request.Method == HttpMethods.Put || context.Request.Method == HttpMethods.Patch)
            {
                if (!context.Request.ContentType?.Contains("application/json") ?? true)
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
