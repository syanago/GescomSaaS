using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Partners;

[Authorize]
public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = PartnerScope.Customers;

    public IReadOnlyList<PartnerListItem> Partners { get; private set; } = [];
    public QuotaUsageItem? ScopeQuota { get; private set; }

    public async Task OnGetAsync()
    {
        Scope = PartnerScope.Normalize(Scope);
        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, cancellationToken: HttpContext.RequestAborted);
        ScopeQuota = quotas.FirstOrDefault(x => x.Label == (Scope == PartnerScope.Suppliers ? "Fournisseurs" : "Clients"));

        Partners = await DbContext.BusinessPartners
            .AsNoTracking()
            .Include(x => x.PaymentTerm)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new PartnerListItem(
                x.Id,
                x.Code,
                x.Name,
                x.PartnerType,
                x.Email,
                x.PhoneNumber,
                x.PaymentTerm != null ? x.PaymentTerm.Label : "-",
                x.CreditLimit,
                x.IsActive))
            .ToListAsync(HttpContext.RequestAborted);

        Partners = Partners
            .Where(x => PartnerScope.MatchesScope(x.PartnerType, Scope))
            .ToList();
    }

    public string Title => PartnerScope.Title(Scope);
    public string QuotaLabel => Scope == PartnerScope.Suppliers ? "Fournisseurs" : "Clients";
}

public sealed record PartnerListItem(
    Guid Id,
    string Code,
    string Name,
    BusinessPartnerType PartnerType,
    string? Email,
    string? PhoneNumber,
    string PaymentTermLabel,
    decimal CreditLimit,
    bool IsActive);
