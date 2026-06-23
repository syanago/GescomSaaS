using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GescomSaas.Web.Pages.Finance;

[Authorize]
public class CustomerAccountModel(
    GescomSaas.Infrastructure.Persistence.ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ISettlementService settlementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.FinanceManage];

    [BindProperty(SupportsGet = true)]
    public Guid PartnerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = FinanceScope.Receivables;

    public CustomerAccountSummary? Account { get; private set; }
    public string StatusLabel => Account?.AccountStatus switch
    {
        CustomerAccountStatus.BlockedForOrder => "Blocage commandes",
        CustomerAccountStatus.BlockedForDelivery => "Blocage livraisons",
        CustomerAccountStatus.Watch => "Sous surveillance",
        _ => "Compte OK"
    };

    public async Task<IActionResult> OnGetAsync()
    {
        Scope = FinanceScope.Normalize(Scope);
        var tenantId = await GetTenantIdAsync();
        Account = await settlementService.GetCustomerAccountAsync(tenantId, PartnerId, FinanceScope.ToDirection(Scope), HttpContext.RequestAborted);

        return Account is null
            ? RedirectToPage("/Finance/OpenItems", new { scope = Scope })
            : Page();
    }

    public async Task<IActionResult> OnPostSetDisputeAsync(Guid documentId, bool inDispute)
    {
        Scope = FinanceScope.Normalize(Scope);
        var tenantId = await GetTenantIdAsync();
        await settlementService.SetDisputeStateAsync(tenantId, documentId, inDispute, null, HttpContext.RequestAborted);
        StatusMessage = inDispute ? "Facture placee en litige." : "Litige retire.";
        return RedirectToPage(new { partnerId = PartnerId, scope = Scope });
    }

    public async Task<IActionResult> OnPostPromiseAsync(Guid documentId, DateOnly? promiseToPayDate)
    {
        Scope = FinanceScope.Normalize(Scope);
        var tenantId = await GetTenantIdAsync();
        await settlementService.SetPromiseToPayAsync(tenantId, documentId, promiseToPayDate, null, HttpContext.RequestAborted);
        StatusMessage = promiseToPayDate.HasValue
            ? "Promesse de paiement enregistree."
            : "Promesse de paiement retiree.";
        return RedirectToPage(new { partnerId = PartnerId, scope = Scope });
    }

    public async Task<IActionResult> OnPostApplyDepositsAsync(Guid documentId)
    {
        Scope = FinanceScope.Normalize(Scope);
        var tenantId = await GetTenantIdAsync();

        try
        {
            var result = await settlementService.ApplyAvailableDepositsAsync(tenantId, documentId, HttpContext.RequestAborted);
            StatusMessage = $"Acomptes imputes : {result.AppliedAmount:n2} sur {result.PaymentCount} acompte(s).";
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }

        return RedirectToPage(new { partnerId = PartnerId, scope = Scope });
    }
}
