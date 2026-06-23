using GescomSaas.Application.Contracts;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace GescomSaas.Web.Pages.Settings;

[Authorize]
public abstract class SettingsPageModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    IOptions<LigComRuntimeOptions> runtimeOptions) : CommercialPageModel(dbContext, currentTenantAccessor), IAsyncPageFilter
{
    protected IUserPermissionService UserPermissionService { get; } = userPermissionService;
    protected LigComRuntimeOptions RuntimeOptions { get; } = runtimeOptions.Value;
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

        if (RuntimeOptions.Mode == LigComNodeMode.LocalNode
            && !context.HttpContext.User.IsInRole("PlatformAdmin")
            && !context.HttpContext.User.IsInRole("TenantOwner"))
        {
            context.Result = Forbid();
            return;
        }

        await next();
    }
}
