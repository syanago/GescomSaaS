using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SalesDocuments;

[Authorize]
public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public string Type { get; set; } = CommercialDocumentType.SalesQuote.ToString();

    public IReadOnlyList<SalesDocumentListItem> Documents { get; private set; } = [];
    public CommercialDocumentType CurrentType { get; private set; }
    public QuotaUsageItem? DocumentQuota { get; private set; }

    public async Task OnGetAsync()
    {
        CurrentType = SalesDocumentCatalog.Normalize(Type);
        Type = CurrentType.ToString();

        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, cancellationToken: HttpContext.RequestAborted);
        DocumentQuota = quotas.FirstOrDefault(x => x.Label == "Documents du mois");
        Documents = await DbContext.CommercialDocuments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.SourceDocument)
            .Where(x => x.TenantId == tenantId && x.DocumentType == CurrentType)
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Select(x => new SalesDocumentListItem(
                x.Id,
                x.Number,
                x.DocumentDate,
                x.Partner != null ? x.Partner.Name : "-",
                x.Status,
                x.TotalIncludingTax,
                x.CurrencyCode,
                x.SourceDocument != null ? x.SourceDocument.Number : null))
            .ToListAsync(HttpContext.RequestAborted);
    }

    public string Title => SalesDocumentCatalog.Label(CurrentType);
}

public sealed record SalesDocumentListItem(
    Guid Id,
    string Number,
    DateOnly DocumentDate,
    string PartnerName,
    CommercialDocumentStatus Status,
    decimal TotalIncludingTax,
    string CurrencyCode,
    string? SourceNumber);
