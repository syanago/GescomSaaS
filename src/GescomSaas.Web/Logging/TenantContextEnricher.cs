using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace GescomSaas.Web.Logging;

/// <summary>
/// Enrichisseur Serilog qui ajoute TenantId et UserId a chaque log entry,
/// extraits des claims de l'utilisateur authentifie.
///
/// Apres traitement, un log JSON typique contient :
///   { "@t": "...", "TenantId": "abc-123", "UserId": "user@x.com",
///     "CorrelationId": "0HNL...", "@m": "Document cree", ... }
///
/// Cela permet de filtrer dans Seq/Elastic/Loki par tenant ou utilisateur,
/// indispensable pour le support et l'audit en multi-tenant.
/// </summary>
public sealed class TenantContextEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContextEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var tenantId = user.FindFirstValue("tenant_id");
        if (!string.IsNullOrEmpty(tenantId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TenantId", tenantId));
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", userId));
        }

        var userName = user.Identity.Name;
        if (!string.IsNullOrEmpty(userName))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserName", userName));
        }
    }
}
