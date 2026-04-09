using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.PurchaseDocuments;

[Authorize]
public class DetailsModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    ICommercialDocumentWorkflowService workflowService,
    ICommercialDocumentPdfService pdfService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public PurchaseDocumentDetailsViewModel? Document { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnGetPdfAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var pdf = await pdfService.GeneratePdfAsync(tenantId, Id, HttpContext.RequestAborted);
        return File(pdf.Content, pdf.ContentType, pdf.FileName);
    }

    public async Task<IActionResult> OnPostTransformAsync(CommercialDocumentType targetType)
    {
        var tenantId = await GetTenantIdAsync();
        try
        {
            var target = await workflowService.CreateFromSourceAsync(tenantId, Id, targetType, HttpContext.RequestAborted);
            await workflowService.SynchronizeSourceStatusAsync(Id, HttpContext.RequestAborted);

            if (target.DocumentType == CommercialDocumentType.GoodsReceipt)
            {
                await CreateStockReceiptsAsync(target, tenantId);
            }

            StatusMessage = $"{PurchaseDocumentCatalog.Label(target.DocumentType)} {target.Number} cree.";
            return RedirectToPage("/PurchaseDocuments/Edit", new { id = target.Id });
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
            return RedirectToPage(new { id = Id });
        }
    }

    private async Task<IActionResult> LoadAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Document = await DbContext.CommercialDocuments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.Warehouse)
            .Include(x => x.SourceDocument)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Where(x => x.Id == Id && x.TenantId == tenantId)
            .Select(x => new PurchaseDocumentDetailsViewModel(
                x.Id,
                x.Number,
                x.DocumentType,
                x.Status,
                x.DocumentDate,
                x.DueDate,
                x.CurrencyCode,
                x.Partner != null ? x.Partner.Name : "-",
                x.Warehouse != null ? x.Warehouse.Label : "-",
                x.SourceDocument != null ? x.SourceDocument.Number : null,
                x.Notes,
                x.TotalExcludingTax,
                x.TotalTax,
                x.TotalIncludingTax,
                x.Lines.Select(line => new PurchaseDocumentDetailsLine(
                    line.Product != null ? line.Product.Sku : "-",
                    line.Description,
                    line.Quantity,
                    line.UnitPriceExcludingTax,
                    line.DiscountRate,
                    line.TaxRate,
                    line.LineTotalIncludingTax)).ToList()))
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        return Document is null ? NotFound() : Page();
    }

    private async Task CreateStockReceiptsAsync(CommercialDocument goodsReceipt, Guid tenantId)
    {
        if (!goodsReceipt.WarehouseId.HasValue)
        {
            return;
        }

        foreach (var line in goodsReceipt.Lines.Where(x => x.ProductId.HasValue))
        {
            var product = await DbContext.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == line.ProductId && x.TenantId == tenantId, HttpContext.RequestAborted);

            if (product is null || !product.TrackStock)
            {
                continue;
            }

            DbContext.StockMovements.Add(new StockMovement
            {
                TenantId = tenantId,
                ProductId = product.Id,
                WarehouseId = goodsReceipt.WarehouseId.Value,
                MovementDate = goodsReceipt.DocumentDate,
                MovementType = StockMovementType.Receipt,
                Quantity = line.Quantity,
                UnitCost = line.UnitPriceExcludingTax,
                ReferenceNumber = goodsReceipt.Number
            });
        }

        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
    }
}

public sealed record PurchaseDocumentDetailsViewModel(
    Guid Id,
    string Number,
    CommercialDocumentType DocumentType,
    CommercialDocumentStatus Status,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    string CurrencyCode,
    string PartnerName,
    string WarehouseLabel,
    string? SourceNumber,
    string? Notes,
    decimal TotalExcludingTax,
    decimal TotalTax,
    decimal TotalIncludingTax,
    IReadOnlyList<PurchaseDocumentDetailsLine> Lines);

public sealed record PurchaseDocumentDetailsLine(
    string ProductCode,
    string Description,
    decimal Quantity,
    decimal UnitPriceExcludingTax,
    decimal DiscountRate,
    decimal TaxRate,
    decimal TotalIncludingTax);
