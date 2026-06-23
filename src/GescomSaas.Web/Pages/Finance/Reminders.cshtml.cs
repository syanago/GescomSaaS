using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GescomSaas.Web.Pages.Finance;

[Authorize]
public class RemindersModel(
    GescomSaas.Infrastructure.Persistence.ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ISettlementService settlementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.FinanceManage];

    public IReadOnlyList<ReminderQueueItem> Items { get; private set; } = [];
    public IReadOnlyList<GroupedReminderItem> GroupedItems { get; private set; } = [];
    public decimal TotalAmount => Items.Sum(x => x.BalanceAmount);

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Items = await settlementService.GetReminderQueueAsync(tenantId, HttpContext.RequestAborted);
        GroupedItems = Items
            .GroupBy(x => new { x.PartnerId, x.PartnerCode, x.PartnerName })
            .Select(group => new GroupedReminderItem(
                group.Key.PartnerId,
                group.Key.PartnerCode,
                group.Key.PartnerName,
                group.Count(),
                group.Sum(x => x.BalanceAmount),
                group.Max(x => x.OverdueDays),
                group.ToList()))
            .OrderByDescending(x => x.MaxOverdueDays)
            .ThenBy(x => x.PartnerCode)
            .ToList();
    }

    public async Task<IActionResult> OnPostRunAsync(Guid documentId, ReminderLevel level)
    {
        var tenantId = await GetTenantIdAsync();
        await settlementService.RegisterReminderAsync(tenantId, documentId, level, null, HttpContext.RequestAborted);
        StatusMessage = "Relance enregistree.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRunGroupedAsync(Guid partnerId)
    {
        var tenantId = await GetTenantIdAsync();
        await settlementService.RegisterGroupedReminderAsync(tenantId, partnerId, null, HttpContext.RequestAborted);
        StatusMessage = "Relance groupee enregistree.";
        return RedirectToPage();
    }
}
