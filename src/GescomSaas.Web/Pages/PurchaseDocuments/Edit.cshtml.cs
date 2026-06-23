using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
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
    IUserPermissionService userPermissionService,
    ICommercialDocumentWorkflowService workflowService,
    INumberingService numberingService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.PurchasesDocumentsManage];

    private static readonly BusinessPartnerType[] AllowedPartnerTypes =
    [
        BusinessPartnerType.Supplier,
        BusinessPartnerType.Both
    ];

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public PurchaseDocumentInputModel Input { get; set; } = new();

    [BindProperty]
    public AssistedPartnerEntryInputModel PartnerEntry { get; set; } = new();

    [BindProperty]
    public PurchaseDocumentLineInputModel NewLine { get; set; } = new();

    public IReadOnlyList<PartnerLookupOption> PartnerOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Warehouses { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Products { get; private set; } = [];
    public string ProductTrackingMetadataJson { get; private set; } = "{}";
    public PartnerLookupMode PartnerLookupMode { get; private set; } = GescomSaas.Domain.Enums.PartnerLookupMode.Code;
    public IReadOnlyList<SelectListItem> Statuses { get; } =
    [
        new("Brouillon", CommercialDocumentStatus.Draft.ToString()),
        new("Ouvert", CommercialDocumentStatus.Open.ToString()),
        new("Partiellement traite", CommercialDocumentStatus.PartiallyProcessed.ToString()),
        new("Cloture", CommercialDocumentStatus.Completed.ToString()),
        new("Annule", CommercialDocumentStatus.Cancelled.ToString())
    ];

    public IReadOnlyList<PurchaseLineItem> Lines { get; private set; } = [];
    public string PartnerLookupLabel => PartnerLookupMode == GescomSaas.Domain.Enums.PartnerLookupMode.Code ? "Code du fournisseur" : "Nom du fournisseur";
    public string PartnerLookupPlaceholder => PartnerLookupMode == GescomSaas.Domain.Enums.PartnerLookupMode.Code ? "Exemple : FOU-0001" : "Exemple : Atelier Nova";

    public async Task<IActionResult> OnGetAsync() => await LoadPageAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Clear();
        await LoadLookupsAsync();
        await ResolvePartnerAsync();
        if (!TryValidateModel(Input, nameof(Input)))
        {
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

        // Place la nouvelle ligne en derniere position
        var currentMaxSortOrder = await DbContext.CommercialDocumentLines
            .Where(x => x.CommercialDocumentId == entity.Id)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(HttpContext.RequestAborted) ?? -1;
        line.SortOrder = currentMaxSortOrder + 1;

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

    public async Task<IActionResult> OnPostUpdateLineAsync(Guid lineId, decimal quantity, decimal unitPrice, decimal discountRate, decimal taxRate)
    {
        // Validation cote serveur
        if (quantity <= 0)
        {
            return new JsonResult(new { ok = false, error = "La quantite doit etre superieure a 0." }) { StatusCode = 400 };
        }
        if (unitPrice < 0)
        {
            return new JsonResult(new { ok = false, error = "Le prix unitaire ne peut pas etre negatif." }) { StatusCode = 400 };
        }
        if (discountRate < 0 || discountRate > 100)
        {
            return new JsonResult(new { ok = false, error = "La remise doit etre comprise entre 0 et 100." }) { StatusCode = 400 };
        }
        if (taxRate < 0 || taxRate > 100)
        {
            return new JsonResult(new { ok = false, error = "Le taux de taxe doit etre compris entre 0 et 100." }) { StatusCode = 400 };
        }

        var tenantId = await GetTenantIdAsync();
        var doc = await DbContext.CommercialDocuments
            .AsNoTracking()
            .Where(x => x.Id == Id && x.TenantId == tenantId)
            .Select(x => new { x.Id, x.Status })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (doc is null)
        {
            return new JsonResult(new { ok = false, error = "Document introuvable." }) { StatusCode = 404 };
        }

        if (doc.Status != CommercialDocumentStatus.Draft && doc.Status != CommercialDocumentStatus.Open)
        {
            return new JsonResult(new { ok = false, error = "Le document n'est plus modifiable dans son statut actuel." }) { StatusCode = 409 };
        }

        var line = await DbContext.CommercialDocumentLines
            .FirstOrDefaultAsync(x => x.Id == lineId && x.CommercialDocumentId == Id, HttpContext.RequestAborted);

        if (line is null)
        {
            return new JsonResult(new { ok = false, error = "Ligne introuvable." }) { StatusCode = 404 };
        }

        line.Quantity = quantity;
        line.UnitPriceExcludingTax = unitPrice;
        line.DiscountRate = discountRate;
        line.TaxRate = taxRate;
        ApplyLineTotals(line);

        try
        {
            await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
            await RefreshDocumentTotalsAsync(Id, tenantId, doc.Status);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new JsonResult(new { ok = false, error = "La piece a ete modifiee entre-temps. Recharge l'ecran." }) { StatusCode = 409 };
        }

        var totals = await DbContext.CommercialDocuments
            .AsNoTracking()
            .Where(x => x.Id == Id)
            .Select(x => new { x.TotalExcludingTax, x.TotalTax, x.TotalIncludingTax })
            .FirstAsync(HttpContext.RequestAborted);

        return new JsonResult(new
        {
            ok = true,
            line = new
            {
                id = line.Id,
                quantity = line.Quantity,
                unitPrice = line.UnitPriceExcludingTax,
                discountRate = line.DiscountRate,
                taxRate = line.TaxRate,
                subtotal = line.LineTotalExcludingTax,
                tax = line.LineTaxAmount,
                total = line.LineTotalIncludingTax
            },
            document = new
            {
                totalExcludingTax = totals.TotalExcludingTax,
                totalTax = totals.TotalTax,
                totalIncludingTax = totals.TotalIncludingTax
            }
        });
    }

    public async Task<IActionResult> OnPostBulkUpdateLinesAsync(List<Guid> lineIds, decimal? discountRate, decimal? taxRate)
    {
        if (lineIds is null || lineIds.Count == 0)
        {
            return new JsonResult(new { ok = false, error = "Aucune ligne selectionnee." }) { StatusCode = 400 };
        }

        if (lineIds.Distinct().Count() != lineIds.Count)
        {
            return new JsonResult(new { ok = false, error = "Doublons detectes dans la selection." }) { StatusCode = 400 };
        }

        if (!discountRate.HasValue && !taxRate.HasValue)
        {
            return new JsonResult(new { ok = false, error = "Aucune valeur a appliquer (remise ou taxe requise)." }) { StatusCode = 400 };
        }

        if (discountRate is < 0m or > 100m)
        {
            return new JsonResult(new { ok = false, error = "La remise doit etre comprise entre 0 et 100." }) { StatusCode = 400 };
        }

        if (taxRate is < 0m or > 100m)
        {
            return new JsonResult(new { ok = false, error = "Le taux de taxe doit etre compris entre 0 et 100." }) { StatusCode = 400 };
        }

        var tenantId = await GetTenantIdAsync();
        var doc = await DbContext.CommercialDocuments
            .AsNoTracking()
            .Where(x => x.Id == Id && x.TenantId == tenantId)
            .Select(x => new { x.Id, x.Status })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (doc is null)
        {
            return new JsonResult(new { ok = false, error = "Document introuvable." }) { StatusCode = 404 };
        }

        if (doc.Status != CommercialDocumentStatus.Draft && doc.Status != CommercialDocumentStatus.Open)
        {
            return new JsonResult(new { ok = false, error = "Le document n'est plus modifiable." }) { StatusCode = 409 };
        }

        var lines = await DbContext.CommercialDocumentLines
            .Where(x => x.CommercialDocumentId == Id && lineIds.Contains(x.Id))
            .ToListAsync(HttpContext.RequestAborted);

        if (lines.Count != lineIds.Count)
        {
            return new JsonResult(new { ok = false, error = "Au moins une ligne n'appartient pas a ce document." }) { StatusCode = 400 };
        }

        foreach (var line in lines)
        {
            if (discountRate.HasValue) line.DiscountRate = discountRate.Value;
            if (taxRate.HasValue) line.TaxRate = taxRate.Value;
            ApplyLineTotals(line);
        }

        try
        {
            await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
            await RefreshDocumentTotalsAsync(Id, tenantId, doc.Status);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new JsonResult(new { ok = false, error = "La piece a ete modifiee entre-temps." }) { StatusCode = 409 };
        }

        var totals = await DbContext.CommercialDocuments
            .AsNoTracking()
            .Where(x => x.Id == Id)
            .Select(x => new { x.TotalExcludingTax, x.TotalTax, x.TotalIncludingTax })
            .FirstAsync(HttpContext.RequestAborted);

        return new JsonResult(new
        {
            ok = true,
            count = lines.Count,
            lines = lines.Select(l => new
            {
                id = l.Id,
                quantity = l.Quantity,
                unitPrice = l.UnitPriceExcludingTax,
                discountRate = l.DiscountRate,
                taxRate = l.TaxRate,
                subtotal = l.LineTotalExcludingTax,
                tax = l.LineTaxAmount,
                total = l.LineTotalIncludingTax
            }),
            document = new
            {
                totalExcludingTax = totals.TotalExcludingTax,
                totalTax = totals.TotalTax,
                totalIncludingTax = totals.TotalIncludingTax
            }
        });
    }

    public async Task<IActionResult> OnPostReorderLinesAsync(List<Guid> lineIds)
    {
        if (lineIds is null || lineIds.Count == 0)
        {
            return new JsonResult(new { ok = false, error = "Liste de lignes vide." }) { StatusCode = 400 };
        }

        if (lineIds.Distinct().Count() != lineIds.Count)
        {
            return new JsonResult(new { ok = false, error = "Doublons detectes dans l'ordre des lignes." }) { StatusCode = 400 };
        }

        var tenantId = await GetTenantIdAsync();
        var doc = await DbContext.CommercialDocuments
            .AsNoTracking()
            .Where(x => x.Id == Id && x.TenantId == tenantId)
            .Select(x => new { x.Id, x.Status })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (doc is null)
        {
            return new JsonResult(new { ok = false, error = "Document introuvable." }) { StatusCode = 404 };
        }

        if (doc.Status != CommercialDocumentStatus.Draft && doc.Status != CommercialDocumentStatus.Open)
        {
            return new JsonResult(new { ok = false, error = "Le document n'est plus modifiable." }) { StatusCode = 409 };
        }

        var lines = await DbContext.CommercialDocumentLines
            .Where(x => x.CommercialDocumentId == Id)
            .ToListAsync(HttpContext.RequestAborted);

        var existingIds = lines.Select(x => x.Id).ToHashSet();
        if (lineIds.Any(id => !existingIds.Contains(id)))
        {
            return new JsonResult(new { ok = false, error = "Au moins une ligne n'appartient pas a ce document." }) { StatusCode = 400 };
        }

        if (lineIds.Count != lines.Count)
        {
            return new JsonResult(new { ok = false, error = "L'ordre fourni ne couvre pas toutes les lignes du document." }) { StatusCode = 400 };
        }

        var byId = lines.ToDictionary(x => x.Id);
        for (var i = 0; i < lineIds.Count; i++)
        {
            byId[lineIds[i]].SortOrder = i;
        }

        try
        {
            await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new JsonResult(new { ok = false, error = "La piece a ete modifiee entre-temps." }) { StatusCode = 409 };
        }

        return new JsonResult(new { ok = true, count = lineIds.Count });
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

        var partnerContext = await PartnerAssistService.LoadOptionsAsync(DbContext, tenantId, AllowedPartnerTypes, HttpContext.RequestAborted);
        PartnerLookupMode = partnerContext.Tenant.PartnerLookupMode;
        PartnerOptions = partnerContext.Options;

        if (Input.PartnerId.HasValue && string.IsNullOrWhiteSpace(PartnerEntry.Lookup))
        {
            var selectedPartner = PartnerOptions.FirstOrDefault(x => x.Id == Input.PartnerId.Value);
            if (selectedPartner is not null)
            {
                PartnerEntry.Lookup = selectedPartner.DisplayValue;
            }
        }

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
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedOnUtc)
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

    private async Task ResolvePartnerAsync()
    {
        if (string.IsNullOrWhiteSpace(PartnerEntry.Lookup))
        {
            Input.PartnerId = null;
            return;
        }

        var tenantId = await GetTenantIdAsync();
        var result = await PartnerAssistService.ResolveOrCreateAsync(
            DbContext,
            numberingService,
            tenantId,
            AllowedPartnerTypes,
            BusinessPartnerType.Supplier,
            ReferenceNumberingScope.Supplier,
            PartnerLookupMode,
            PartnerEntry,
            HttpContext.RequestAborted);

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            ModelState.AddModelError("Input.PartnerId", result.ErrorMessage);
            Input.PartnerId = null;
            return;
        }

        Input.PartnerId = result.PartnerId;
        PartnerEntry.Lookup = result.LookupValue ?? PartnerEntry.Lookup;
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
