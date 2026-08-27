using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PokerClub.Api.Services;

namespace PokerClub.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class VkAuthorizeAttribute : Attribute, IAsyncActionFilter
{
    public bool RequireAdmin { get; set; } = false;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetRequiredService<IVkAuthValidator>();
        var result = validator.Validate(context.HttpContext);

        if (!result.IsValid || string.IsNullOrWhiteSpace(result.VkUserId))
        {
            context.Result = new UnauthorizedObjectResult(new 
            { 
                Message = result.ErrorMessage ?? "Ошибка авторизации через VK." 
            });
            return;
        }

        if (RequireAdmin && !result.IsAdmin)
        {
            context.Result = new ObjectResult(new 
            { 
                Message = "Доступ запрещен: требуются права администратора клуба." 
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        context.HttpContext.Items["VkUserId"] = result.VkUserId;
        context.HttpContext.Items["IsAdmin"] = result.IsAdmin;

        await next();
    }
}
