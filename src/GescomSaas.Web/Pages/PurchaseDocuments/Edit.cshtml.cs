using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GescomSaas.Web.Pages.PurchaseDocuments;

[Authorize]
public class EditModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    ICommercialDocumentWorkflowService workflowService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public PurchaseDocumentInputModel Input { get; set; } = new();

    [BindProperty]
    public PurchaseDocumentLineInputModel NewLine { get; set; } = new();

    public IReadOnlyList<SelectListItem> Partners { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Warehouses { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Products { get; private set; } = [];
    public string ProductTrackingMetadataJson { get; private set; } = "{}";
    public IReadOnlyList<SelectListItem> Statuses { get; } =
    [
        new("Brouillon", CommercialDocumentStatus.Draft.ToString()),
        new("Ouvert", CommercialDocumentStatus.Open.ToString()),
        new("Partiellement traite", CommercialDocumentStatus.PartiallyProcessed.ToString()),
        new("Cloture", CommercialDocumentStatus.Completed.ToString()),
        new("Annule", CommercialDocumentStatus.Cancelled.ToString())
    ];

    public IReadOnlyList<PurchaseLineItem> Lines { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync() => await LoadPageAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)))
        {
            await LoadLookupsAsync();
            await LoadLinesAsync();
            return Page();
        }

        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.CommercialDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        Input.ApplyTo(entity);
        workflowService.RecalculateTotals(entity);
        try
        {
            await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            StatusMessage = "La piece a ete modifiee entre-temps. Recharge l'ecran puis recommence.";
            return RedirectToPage("/PurchaseDocuments/Edit", new { id = Id });
        }

        StatusMessage = $"{entity.Number} mis a jour.";
        return RedirectToPage("/PurchaseDocuments/Edit", new { id = Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.CommercialDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        Input = PurchaseDocumentInputModel.FromEntity(entity);

        ModelState.Clear();
        var lineIsValid = TryValidateModel(NewLine, nameof(NewLine));
        if (!lineIsValid || !NewLine.ProductId.HasValue)
        {
            if (!NewLine.ProductId.HasValue)
            {
                ModelState.AddModelError("NewLine.ProductId", "Selectionnez un article.");
            }

            await LoadLookupsAsync();
            await LoadLinesAsync();
            return Page();
        }

        var line = new CommercialDocumentLine();
        NewLine.ApplyTo(line);
        ApplyLineTotals(line);
        line.CommercialDocumentId = entity.Id;
        DbContext.CommercialDocumentLines.Add(line);
        try
        {
            await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
            await RefreshDocumentTotalsAsync(entity.Id, tenantId, entity.Status == CommercialDocumentStatus.Draft ? CommercialDocumentStatus.Open : entity.Status);
        }
        catch (DbUpdateConcurrencyException)
        {
            StatusMessage = "La piece a ete modifiee entre-temps. Recharge l'ecran puis recommence.";
            return RedirectToPage("/PurchaseDocuments/Edit", new { id = Id });
        }

        StatusMessage = "Ligne ajoutee.";
        return RedirectToPage("/PurchaseDocuments/Edit", new { id = Id });
    }

    public async Task<IActionResult> OnPostDeleteLineAsync(Guid lineId)
    {
        var tenantId = await GetTenantIdAsync();
        var entityExists = await DbContext.CommercialDocuments
            .AsNoTracking()
            .AnyAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (!entityExists)
        {
            return NotFound();
        }

        var deletedCount = await DbContext.CommercialDocumentLines
            .Where(x => x.Id == lineId && x.CommercialDocumentId == Id)
            .ExecuteDeleteAsync(HttpContext.RequestAborted);

        if (deletedCount == 0)
        {
            return NotFound();
        }

        try
        {
            var currentStatus = await DbContext.CommercialDocuments
                .AsNoTracking()
                .Where(x => x.Id == Id && x.TenantId == tenantId)
                .Select(x => x.Status)
                .FirstAsync(HttpContext.RequestAborted);

            await RefreshDocumentTotalsAsync(Id, tenantId, currentStatus);
        }
        catch (DbUpdateConcurrencyException)
        {
            StatusMessage = "La ligne etait deja supprimee ou la piece a ete modifiee entre-temps.";
            return RedirectToPage("/PurchaseDocuments/Edit", new { id = Id });
        }

        StatusMessage = "Ligne supprimee.";
        return RedirectToPage("/PurchaseDocuments/Edit", new { id = Id });
    }

    private async Task<IActionResult> LoadPageAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.CommercialDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        Input = PurchaseDocumentInputModel.FromEntity(entity);
        await LoadLookupsAsync();
        await LoadLinesAsync();
        return Page();
    }

    private async Task LoadLookupsAsync()
    {
        var tenantId = await GetTenantIdAsync();

        Partners = await DbContext.BusinessPartners
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && (x.PartnerType == BusinessPartnerType.Supplier || x.PartnerType == BusinessPartnerType.Both))
            .OrderBy(x => x.Code)
            .Select(x => new SelectListItem($"{x.Code} - {x.Name}", x.Id.ToString()))
            .ToListAsync(HttpContext.RequestAborted);

        Warehouses = await DbContext.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Code)
            .Select(x => new SelectListItem($"{x.Code} - {x.Label}", x.Id.ToString()))
            .ToListAsync(HttpContext.RequestAborted);

        var products = await DbContext.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Sku)
            .Select(x => new ProductTrackingOption(
                x.Id,
                x.Sku,
                x.Label,
                x.UnitOfMeasure,
                x.StockIdentityTrackingMode))
            .ToListAsync(HttpContext.RequestAborted);

        Products = products
            .Select(x => new SelectListItem($"{x.Sku} - {x.Label}", x.Id.ToString()))
            .ToList();

        ProductTrackingMetadataJson = JsonSerializer.Serialize(products.ToDictionary(
            x => x.Id.ToString(),
            x => new
            {
                mode = x.TrackingMode.ToString(),
                sku = x.Sku,
                unit = x.UnitOfMeasure
            }));
    }

    private async Task LoadLinesAsync()
    {
        Lines = await DbContext.CommercialDocumentLines
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.CommercialDocumentId == Id)
            .OrderBy(x => x.CreatedOnUtc)
            .Select(x => new PurchaseLineItem(
                x.Id,
                x.Product != null ? x.Product.Sku : "-",
                x.Description,
                x.Quantity,
                x.UnitPriceExcludingTax,
                x.DiscountRate,
                x.TaxRate,
                x.LineTotalIncludingTax,
                x.LotNumber,
                x.SerialNumber))
            .ToListAsync(HttpContext.RequestAborted);
    }

    private async Task RefreshDocumentTotalsAsync(Guid documentId, Guid tenantId, CommercialDocumentStatus status)
    {
        var lines = await DbContext.CommercialDocumentLines
            .AsNoTracking()
            .Where(x => x.CommercialDocumentId == documentId)
            .Select(x => new
            {
                x.LineTotalExcludingTax,
                x.LineTaxAmount,
                x.LineTotalIncludingTax
            })
            .ToListAsync(HttpContext.RequestAborted);

        var totalExcludingTax = lines.Sum(x => x.LineTotalExcludingTax);
        var totalTax = lines.Sum(x => x.LineTaxAmount);
        var totalIncludingTax = lines.Sum(x => x.LineTotalIncludingTax);

        var updatedCount = await DbContext.CommercialDocuments
            .Where(x => x.Id == documentId && x.TenantId == tenantId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.TotalExcludingTax, totalExcludingTax)
                .SetProperty(x => x.TotalTax, totalTax)
                .SetProperty(x => x.TotalIncludingTax, totalIncludingTax)
                .SetProperty(x => x.UpdatedOnUtc, _ => DateTime.UtcNow), HttpContext.RequestAborted);

        if (updatedCount == 0)
        {
            throw new DbUpdateConcurrencyException();
        }
    }

    private static void ApplyLineTotals(CommercialDocumentLine line)
    {
        var baseTotal = decimal.Round(line.Quantity * line.UnitPriceExcludingTax, 2);
        var discounted = decimal.Round(baseTotal * (1 - (line.DiscountRate / 100m)), 2);
        var tax = decimal.Round(discounted * (line.TaxRate / 100m), 2);

        line.LineTotalExcludingTax = discounted;
        line.LineTaxAmount = tax;
        line.LineTotalIncludingTax = discounted + tax;
    }
}

public sealed record PurchaseLineItem(
    Guid Id,
    string ProductCode,
    string Description,
    decimal Quantity,
    decimal UnitPriceExcludingTax,
    decimal DiscountRate,
    decimal TaxRate,
    decimal TotalIncludingTax,
    string? LotNumber,
    string? SerialNumber);

sealed record ProductTrackingOption(
    Guid Id,
    string Sku,
    string Label,
    string UnitOfMeasure,
    StockIdentityTrackingMode TrackingMode);
