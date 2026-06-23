using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Partners;

[Authorize]
public class DetailsModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ISettlementService settlementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesPartnersManage];

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = PartnerScope.Customers;

    public BusinessPartner? Partner { get; private set; }
    public CustomerAccountSummary? ReceivablesAccount { get; private set; }
    public CustomerAccountSummary? PayablesAccount { get; private set; }

    public bool ShowReceivables =>
        Partner?.PartnerType is BusinessPartnerType.Customer or BusinessPartnerType.Both or BusinessPartnerType.Prospect;

    public bool ShowPayables =>
        Partner?.PartnerType is BusinessPartnerType.Supplier or BusinessPartnerType.Both;

    public async Task<IActionResult> OnGetAsync()
    {
        Scope = PartnerScope.Normalize(Scope);
        var tenantId = await GetTenantIdAsync();

        Partner = await DbContext.BusinessPartners
            .AsNoTracking()
            .Include(x => x.PaymentTerm)
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (Partner is null)
        {
            return NotFound();
        }

        if (ShowReceivables)
        {
            ReceivablesAccount = await settlementService.GetCustomerAccountAsync(
                tenantId,
                Partner.Id,
                PaymentDirection.Incoming,
                HttpContext.RequestAborted);
        }

        if (ShowPayables)
        {
            PayablesAccount = await settlementService.GetCustomerAccountAsync(
                tenantId,
                Partner.Id,
                PaymentDirection.Outgoing,
                HttpContext.RequestAborted);
        }

        return Page();
    }
}
