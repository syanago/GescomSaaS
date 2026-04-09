using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.StockDocuments;

[Authorize]
public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public string Type { get; set; } = StockDocumentType.Entry.ToString();

    public StockDocumentType CurrentType { get; private set; }
    public IReadOnlyList<StockDocumentListItem> Documents { get; private set; } = [];
    public QuotaUsageItem? DocumentQuota { get; private set; }

    public async Task OnGetAsync()
    {
        CurrentType = StockDocumentCatalog.Normalize(Type);
        Type = CurrentType.ToString();

        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, cancellationToken: HttpContext.RequestAborted);
        DocumentQuota = quotas.FirstOrDefault(x => x.Label == "Documents du mois");

        Documents = await DbContext.StockDocuments
            .AsNoTracking()
            .Include(x => x.SourceWarehouse)
            .Include(x => x.DestinationWarehouse)
            .Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId && x.DocumentType == CurrentType)
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Select(x => new StockDocumentListItem(
                x.Id,
                x.Number,
                x.DocumentDate,
                x.Status,
                x.SourceWarehouse != null ? x.SourceWarehouse.Code + " - " + x.SourceWarehouse.Label : null,
                x.DestinationWarehouse != null ? x.DestinationWarehouse.Code + " - " + x.DestinationWarehouse.Label : null,
                x.Lines.Sum(line => line.Quantity),
                x.Lines.Count()))
            .ToListAsync(HttpContext.RequestAborted);
    }

    public string Title => StockDocumentCatalog.Label(CurrentType);
}

public sealed record StockDocumentListItem(
    Guid Id,
    string Number,
    DateOnly DocumentDate,
    StockDocumentStatus Status,
    string? SourceWarehouseLabel,
    string? DestinationWarehouseLabel,
    decimal TotalQuantity,
    int LineCount);
