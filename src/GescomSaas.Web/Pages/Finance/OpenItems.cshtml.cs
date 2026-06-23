using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GescomSaas.Web.Pages.Finance;

[Authorize]
public class OpenItemsModel(
    GescomSaas.Infrastructure.Persistence.ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ISettlementService settlementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.FinanceManage];

    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = FinanceScope.Receivables;

    public IReadOnlyList<OpenItemSummary> Items { get; private set; } = [];

    public decimal TotalOpen => Items.Sum(x => x.BalanceAmount);
    public decimal TotalOverdue => Items.Where(x => x.OverdueDays > 0).Sum(x => x.BalanceAmount);

    public async Task OnGetAsync()
    {
        Scope = FinanceScope.Normalize(Scope);
        var tenantId = await GetTenantIdAsync();
        Items = await settlementService.GetOpenItemsAsync(tenantId, FinanceScope.ToDirection(Scope), HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostRemindAsync(Guid documentId, ReminderLevel level)
    {
        Scope = FinanceScope.Normalize(Scope);
        if (FinanceScope.ToDirection(Scope) != PaymentDirection.Incoming)
        {
            return RedirectToPage(new { scope = Scope });
        }

        var tenantId = await GetTenantIdAsync();
        await settlementService.RegisterReminderAsync(tenantId, documentId, level, null, HttpContext.RequestAborted);
        StatusMessage = "Relance enregistree.";
        return RedirectToPage(new { scope = Scope });
    }

    public async Task<IActionResult> OnPostApplyDepositsAsync(Guid documentId)
    {
        Scope = FinanceScope.Normalize(Scope);
        if (FinanceScope.ToDirection(Scope) != PaymentDirection.Incoming)
        {
            return RedirectToPage(new { scope = Scope });
        }

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

        return RedirectToPage(new { scope = Scope });
    }

    public string Title => FinanceScope.Title(Scope);
}
