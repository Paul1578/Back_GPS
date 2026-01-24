using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace fletflow.Api.Filters
{
    /// <summary>
    /// Agrega una respuesta 401 a todos los endpoints protegidos.
    /// </summary>
    public class SwaggerUnauthorizedResponseOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var hasAuth = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
                .Concat(context.MethodInfo.GetCustomAttributes(true))
                .Any(attr => attr.GetType().Name.Contains("Authorize")) ?? false;

            if (hasAuth)
            {
                operation.Responses.TryAdd("401", new OpenApiResponse { Description = "No autorizado. Token inválido o ausente." });
            }
        }
    }
}
