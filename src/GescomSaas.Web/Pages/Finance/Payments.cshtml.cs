using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GescomSaas.Web.Pages.Finance;

[Authorize]
public class PaymentsModel(
    GescomSaas.Infrastructure.Persistence.ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    ISettlementService settlementService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = FinanceScope.Receivables;

    public IReadOnlyList<PaymentHistoryItem> Payments { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Scope = FinanceScope.Normalize(Scope);
        var tenantId = await GetTenantIdAsync();
        Payments = await settlementService.GetPaymentsAsync(tenantId, FinanceScope.ToDirection(Scope), HttpContext.RequestAborted);
    }

    public string Title => FinanceScope.PaymentTitle(Scope);
}
