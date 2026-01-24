using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace fletflow.Api.Middleware
{
    public class RoleValidationMiddleware : IMiddleware
    {
        private readonly ILogger<RoleValidationMiddleware> _logger;
        public RoleValidationMiddleware(ILogger<RoleValidationMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            // Solo valida si el endpoint requiere autorización
            var endpoint = context.GetEndpoint();
            var authorizeAttr = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
            if (authorizeAttr != null && !string.IsNullOrEmpty(authorizeAttr.Roles))
            {
                var user = context.User;
                if (!user.Identity?.IsAuthenticated ?? true)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("No autenticado");
                    return;
                }
                var userRoles = user.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
                var requiredRoles = authorizeAttr.Roles.Split(',').Select(r => r.Trim());
                if (!userRoles.Any(r => requiredRoles.Contains(r)))
                {
                    _logger.LogWarning("[RoleValidationMiddleware] Usuario sin permisos. Requiere: {RequiredRoles}, Tiene: {UserRoles}", string.Join(",", requiredRoles), string.Join(",", userRoles));
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("No tienes permisos suficientes");
                    return;
                }
            }
            await next(context);
        }
    }
}
