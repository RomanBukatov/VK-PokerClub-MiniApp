using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using PokerClub.Api.Services;
using PokerClub.Domain.Constants;
using PokerClub.Infrastructure.Data;

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

        bool isConfiguredAdmin = validator.IsConfiguredAdmin(result.VkUserId);
        if (!isConfiguredAdmin)
        {
            var dbContext = context.HttpContext.RequestServices.GetService<AppDbContext>();
            if (dbContext != null)
            {
                var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.VkId == result.VkUserId);
                if (user != null && MasterClubCardConstants.IsMasterAdminCard(user.ClubCardId))
                {
                    isConfiguredAdmin = true;
                }
            }
        }

        if (RequireAdmin && !isConfiguredAdmin)
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
        context.HttpContext.Items["IsAdmin"] = isConfiguredAdmin;

        await next();
    }
}
