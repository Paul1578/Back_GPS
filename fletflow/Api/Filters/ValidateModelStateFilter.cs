using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace fletflow.Api.Filters
{
    public class ValidateModelStateFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .Select(x => new {
                        Field = x.Key,
                        Errors = x.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                    });

                context.Result = new BadRequestObjectResult(new {
                    message = "Modelo inválido",
                    errors
                });
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
