using GescomSaas.Application.Contracts;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GescomSaas.Web.Pages;

[Authorize]
public abstract class CommercialPermissionPageModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    protected IUserPermissionService UserPermissionService { get; } = userPermissionService;
    protected abstract IReadOnlyCollection<string> RequiredPermissionKeys { get; }

    public override Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (RequiredPermissionKeys.Count > 0
            && !await UserPermissionService.HasAnyPermissionAsync(User, RequiredPermissionKeys, HttpContext.RequestAborted))
        {
            context.Result = Forbid();
            return;
        }

        await next();
    }
}
