using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GescomSaas.Web.Pages.Finance;

[Authorize]
public class PaymentsModel(
    GescomSaas.Infrastructure.Persistence.ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ISettlementService settlementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.FinanceManage];

    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = FinanceScope.Receivables;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Method { get; set; }

    public IReadOnlyList<PaymentHistoryItem> Payments { get; private set; } = [];
    public IReadOnlyList<SelectListItem> MethodOptions { get; } =
        Enum.GetValues<PaymentMethod>()
            .Select(method => new SelectListItem(method.ToString(), method.ToString()))
            .ToList();
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Search) ||
        DateFrom.HasValue ||
        DateTo.HasValue ||
        !string.IsNullOrWhiteSpace(Method);

    public async Task OnGetAsync()
    {
        Scope = FinanceScope.Normalize(Scope);
        var tenantId = await GetTenantIdAsync();
        Payments = await settlementService.GetPaymentsAsync(tenantId, FinanceScope.ToDirection(Scope), HttpContext.RequestAborted);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var searchTerm = Search.Trim();
            Payments = Payments
                .Where(x =>
                    x.ReferenceNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    x.PartnerName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (Enum.TryParse<PaymentMethod>(Method, ignoreCase: true, out var parsedMethod))
        {
            Payments = Payments.Where(x => x.Method == parsedMethod).ToList();
            Method = parsedMethod.ToString();
        }
        else if (!string.IsNullOrWhiteSpace(Method))
        {
            Method = null;
        }

        if (DateFrom.HasValue)
        {
            Payments = Payments.Where(x => x.PaymentDate >= DateFrom.Value).ToList();
        }

        if (DateTo.HasValue)
        {
            Payments = Payments.Where(x => x.PaymentDate <= DateTo.Value).ToList();
        }
    }

    public string Title => FinanceScope.PaymentTitle(Scope);
}
