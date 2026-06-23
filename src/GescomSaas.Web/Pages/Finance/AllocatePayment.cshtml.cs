using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GescomSaas.Web.Pages.Finance;

[Authorize]
public class AllocatePaymentModel(
    GescomSaas.Infrastructure.Persistence.ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ISettlementService settlementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.FinanceManage];

    [BindProperty(SupportsGet = true)]
    public Guid PaymentId { get; set; }

    [BindProperty]
    public PaymentAllocationEntryInputModel Input { get; set; } = new();

    public PaymentHistoryItem? Payment { get; private set; }
    public CustomerAccountSummary? Account { get; private set; }
    public IReadOnlyList<SelectListItem> Documents { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        if (!await LoadAsync(tenantId))
        {
            return RedirectToPage("/Finance/Payments");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var tenantId = await GetTenantIdAsync();
        if (!await LoadAsync(tenantId))
        {
            return RedirectToPage("/Finance/Payments");
        }

        if (!ModelState.IsValid || !Input.CommercialDocumentId.HasValue)
        {
            if (!Input.CommercialDocumentId.HasValue)
            {
                ModelState.AddModelError("Input.CommercialDocumentId", "Selectionnez une facture.");
            }

            return Page();
        }

        try
        {
            await settlementService.AllocatePaymentAsync(
                tenantId,
                new PaymentManualAllocationRequest(PaymentId, Input.CommercialDocumentId.Value, Input.Amount, Input.Notes),
                HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }

        StatusMessage = "Affectation enregistree.";
        return RedirectToPage("/Finance/CustomerAccount", new { partnerId = Payment!.PartnerId, scope = Payment.Direction == PaymentDirection.Outgoing ? FinanceScope.Payables : FinanceScope.Receivables });
    }

    private async Task<bool> LoadAsync(Guid tenantId)
    {
        var allPayments = await settlementService.GetPaymentsAsync(tenantId, null, HttpContext.RequestAborted);
        Payment = allPayments.FirstOrDefault(x => x.PaymentId == PaymentId);
        if (Payment is null)
        {
            return false;
        }

        Account = await settlementService.GetCustomerAccountAsync(tenantId, Payment.PartnerId, Payment.Direction, HttpContext.RequestAborted);
        if (Account is null)
        {
            return false;
        }

        Documents = Account.OpenItems
            .Select(x => new SelectListItem($"{x.Number} - {x.DocumentDate:dd/MM/yyyy} - {x.BalanceAmount:n2}", x.DocumentId.ToString()))
            .ToList();

        if (Input.Amount <= 0m)
        {
            Input.Amount = Payment.AvailableAmount;
        }

        return true;
    }
}
