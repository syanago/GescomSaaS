using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.StockDocuments;

[Authorize]
public class DetailsModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IStockDocumentService stockDocumentService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public StockDocumentDetailsViewModel? Document { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostPostAsync()
    {
        var tenantId = await GetTenantIdAsync();
        try
        {
            await stockDocumentService.PostAsync(tenantId, Id, HttpContext.RequestAborted);
            StatusMessage = "Document de stock valide.";
        }
        catch (DbUpdateConcurrencyException)
        {
            StatusMessage = "Le document a ete modifie ou valide entre-temps. Recharge la fiche pour voir son etat actuel.";
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
        Document = await DbContext.StockDocuments
            .AsNoTracking()
            .Include(x => x.SourceWarehouse)
            .Include(x => x.DestinationWarehouse)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Where(x => x.Id == Id && x.TenantId == tenantId)
            .Select(x => new StockDocumentDetailsViewModel(
                x.Id,
                x.Number,
                x.DocumentType,
                x.Status,
                x.DocumentDate,
                x.SourceWarehouse != null ? x.SourceWarehouse.Code + " - " + x.SourceWarehouse.Label : null,
                x.DestinationWarehouse != null ? x.DestinationWarehouse.Code + " - " + x.DestinationWarehouse.Label : null,
                x.Notes,
                x.PostedOnUtc,
                x.Lines.Select(line => new StockDocumentDetailsLine(
                    line.Product != null ? line.Product.Sku : "-",
                    line.Description,
                    line.Quantity,
                    line.UnitCost,
                    line.LotNumber,
                    line.SerialNumber,
                    line.ExpirationDate)).ToList()))
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        return Document is null ? NotFound() : Page();
    }
}

public sealed record StockDocumentDetailsViewModel(
    Guid Id,
    string Number,
    StockDocumentType DocumentType,
    StockDocumentStatus Status,
    DateOnly DocumentDate,
    string? SourceWarehouseLabel,
    string? DestinationWarehouseLabel,
    string? Notes,
    DateTime? PostedOnUtc,
    IReadOnlyList<StockDocumentDetailsLine> Lines);

public sealed record StockDocumentDetailsLine(
    string ProductCode,
    string Description,
    decimal Quantity,
    decimal UnitCost,
    string? LotNumber,
    string? SerialNumber,
    DateOnly? ExpirationDate);
