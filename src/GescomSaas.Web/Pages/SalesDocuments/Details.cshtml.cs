using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SalesDocuments;

[Authorize]
public class DetailsModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ICommercialDocumentWorkflowService workflowService,
    ICommercialDocumentPdfService pdfService,
    IInventoryService inventoryService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.SalesDocumentsManage];

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public SalesDocumentDetailsViewModel? Document { get; private set; }

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

            if (target.DocumentType == CommercialDocumentType.DeliveryNote)
            {
                try
                {
                    await inventoryService.CreateStockIssuesAsync(tenantId, target, HttpContext.RequestAborted);
                    target.Status = CommercialDocumentStatus.Completed;
                    await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
                    StatusMessage = $"{SalesDocumentCatalog.Label(target.DocumentType)} {target.Number} cree et sortie de stock validee.";
                    return RedirectToPage("/SalesDocuments/Details", new { id = target.Id });
                }
                catch (InvalidOperationException)
                {
                    target.Status = CommercialDocumentStatus.Open;
                    await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
                    StatusMessage = $"{SalesDocumentCatalog.Label(target.DocumentType)} {target.Number} cree. Complete les numeros de serie requis puis valide la sortie de stock.";
                    return RedirectToPage("/SalesDocuments/Edit", new { id = target.Id });
                }
            }

            StatusMessage = $"{SalesDocumentCatalog.Label(target.DocumentType)} {target.Number} cree.";
            return RedirectToPage("/SalesDocuments/Edit", new { id = target.Id });
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
            return RedirectToPage(new { id = Id });
        }
    }

    public async Task<IActionResult> OnPostPostStockAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var document = await DbContext.CommercialDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (document is null)
        {
            return NotFound();
        }

        if (document.DocumentType != CommercialDocumentType.DeliveryNote)
        {
            StatusMessage = "Seuls les bons de livraison peuvent declencher une sortie de stock.";
            return RedirectToPage(new { id = Id });
        }

        if (document.Status == CommercialDocumentStatus.Completed)
        {
            StatusMessage = "La sortie de stock de ce bon de livraison est deja validee.";
            return RedirectToPage(new { id = Id });
        }

        try
        {
            await inventoryService.CreateStockIssuesAsync(tenantId, document, HttpContext.RequestAborted);
            document.Status = CommercialDocumentStatus.Completed;
            await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
            StatusMessage = "Sortie de stock validee.";
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }

        return RedirectToPage(new { id = Id });
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
            .Select(x => new SalesDocumentDetailsViewModel(
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
                x.Lines.Select(line => new SalesDocumentDetailsLine(
                    line.Product != null ? line.Product.Sku : "-",
                    line.Description,
                    line.Quantity,
                    line.UnitPriceExcludingTax,
                    line.DiscountRate,
                    line.TaxRate,
                    line.LineTotalIncludingTax,
                    line.LotNumber,
                    line.SerialNumber)).ToList()))
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        return Document is null ? NotFound() : Page();
    }
}

public sealed record SalesDocumentDetailsViewModel(
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
    IReadOnlyList<SalesDocumentDetailsLine> Lines);

public sealed record SalesDocumentDetailsLine(
    string ProductCode,
    string Description,
    decimal Quantity,
    decimal UnitPriceExcludingTax,
    decimal DiscountRate,
    decimal TaxRate,
    decimal TotalIncludingTax,
    string? LotNumber,
    string? SerialNumber);
