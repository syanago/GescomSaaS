using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Partners;

[Authorize]
public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesPartnersManage];

    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = PartnerScope.Customers;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public IReadOnlyList<PartnerListItem> Partners { get; private set; } = [];
    public QuotaUsageItem? ScopeQuota { get; private set; }
    public IReadOnlyList<SelectListItem> StatusOptions { get; } =
    [
        new("Tous les etats", string.Empty),
        new("Actifs", "active"),
        new("Inactifs", "inactive")
    ];
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Search) ||
        !string.IsNullOrWhiteSpace(Status);

    public async Task OnGetAsync()
    {
        Scope = PartnerScope.Normalize(Scope);
        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, cancellationToken: HttpContext.RequestAborted);
        ScopeQuota = quotas.FirstOrDefault(x => x.Label == (Scope == PartnerScope.Suppliers ? "Fournisseurs" : "Clients"));

        IQueryable<GescomSaas.Domain.Entities.Commercial.BusinessPartner> query = DbContext.BusinessPartners
            .AsNoTracking()
            .Include(x => x.PaymentTerm)
            .Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var searchTerm = Search.Trim();
            query = query.Where(x =>
                x.Code.Contains(searchTerm) ||
                x.Name.Contains(searchTerm) ||
                (x.Email != null && x.Email.Contains(searchTerm)) ||
                (x.PhoneNumber != null && x.PhoneNumber.Contains(searchTerm)));
        }

        if (string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsActive);
            Status = "active";
        }
        else if (string.Equals(Status, "inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsActive);
            Status = "inactive";
        }
        else if (!string.IsNullOrWhiteSpace(Status))
        {
            Status = null;
        }

        Partners = await query
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
