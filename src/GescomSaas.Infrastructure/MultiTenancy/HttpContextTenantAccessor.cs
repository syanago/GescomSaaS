using System.Security.Claims;
using GescomSaas.Application.Contracts;
using Microsoft.AspNetCore.Http;

namespace GescomSaas.Infrastructure.MultiTenancy;

public class HttpContextTenantAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentTenantAccessor
{
    public Guid? GetTenantId()
    {
        var rawTenantId = httpContextAccessor.HttpContext?.User.FindFirstValue("tenant_id");
        return Guid.TryParse(rawTenantId, out var tenantId) ? tenantId : null;
    }
}
