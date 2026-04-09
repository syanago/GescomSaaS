using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GescomSaas.Web.Pages.StockDocuments;

[Authorize]
public class EditModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public StockDocumentInputModel Input { get; set; } = new();

    [BindProperty]
    public StockDocumentLineInputModel NewLine { get; set; } = new();

    public IReadOnlyList<SelectListItem> Warehouses { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Products { get; private set; } = [];
    public IReadOnlyList<StockDocumentLineItem> Lines { get; private set; } = [];
    public IReadOnlyList<SelectListItem> SerialEntryModes { get; } =
    [
        new("Numero unique", "Single"),
        new("Enumeration", "Enumeration"),
        new("Plage debut/fin", "Range")
    ];
    public IReadOnlyList<SelectListItem> LotEntryModes { get; } =
    [
        new("Lot unique", "Single"),
        new("Repartition multi-lots", "Breakdown")
    ];
    public string ProductTrackingMetadataJson { get; private set; } = "{}";

    public StockDocumentType CurrentType => Input.DocumentType;
    public bool UsesSourceWarehouse => StockDocumentCatalog.UsesSourceWarehouse(CurrentType);
    public bool UsesDestinationWarehouse => StockDocumentCatalog.UsesDestinationWarehouse(CurrentType);
    public bool IsPosted => Input.Status == StockDocumentStatus.Posted;
    public bool RequiresSerializedOutputs => CurrentType is StockDocumentType.Exit or StockDocumentType.Transfer;

    public async Task<IActionResult> OnGetAsync() => await LoadPageAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();
        await LoadLinesAsync();

        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.StockDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        if (entity.Status != StockDocumentStatus.Draft)
        {
            ModelState.AddModelError(string.Empty, "Seuls les documents brouillon peuvent etre modifies.");
            Input = StockDocumentInputModel.FromEntity(entity);
            return Page();
        }

        Input.DocumentType = entity.DocumentType;
        Input.Status = entity.Status;
        ValidateWarehouses();

        ModelState.ClearValidationState(nameof(Input));
        if (!TryValidateModel(Input, nameof(Input)))
        {
            return Page();
        }

        Input.ApplyTo(entity);
        try
        {
            await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            StatusMessage = "Le document a ete modifie entre-temps. Recharge l'ecran puis recommence.";
            return RedirectToPage("/StockDocuments/Edit", new { id = Id });
        }

        StatusMessage = $"{entity.Number} mis a jour.";
        return RedirectToPage("/StockDocuments/Edit", new { id = Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync()
    {
        await LoadLookupsAsync();
        await LoadLinesAsync();

        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.StockDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        Input = StockDocumentInputModel.FromEntity(entity);

        if (entity.Status != StockDocumentStatus.Draft)
        {
            ModelState.AddModelError(string.Empty, "Seuls les documents brouillon peuvent etre modifies.");
            return Page();
        }

        ProductTrackingOption? selectedProduct = null;
        if (NewLine.ProductId.HasValue)
        {
            selectedProduct = await DbContext.Products
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.IsActive && x.TrackStock && x.Id == NewLine.ProductId.Value)
                .Select(x => new ProductTrackingOption(
                    x.Id,
                    x.Sku,
                    x.Label,
                    x.Description,
                    x.UnitOfMeasure,
                    x.StockIdentityTrackingMode,
                    x.PurchasePrice))
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            if (selectedProduct is not null)
            {
                if (string.IsNullOrWhiteSpace(NewLine.Description))
                {
                    NewLine.Description = selectedProduct.Label;
                }

                if (NewLine.UnitCost <= 0m)
                {
                    NewLine.UnitCost = selectedProduct.PurchasePrice;
                }
            }
        }

        ModelState.Remove("NewLine.UnitCost");
        ModelState.ClearValidationState(nameof(NewLine));
        var isValid = TryValidateModel(NewLine, nameof(NewLine));
        if (string.IsNullOrWhiteSpace(NewLine.Description))
        {
            ModelState.AddModelError("NewLine.Description", "La designation est requise.");
            isValid = false;
        }

        if (!isValid || !NewLine.ProductId.HasValue || selectedProduct is null)
        {
            if (!NewLine.ProductId.HasValue)
            {
                ModelState.AddModelError("NewLine.ProductId", "Selectionne un article.");
            }
            else if (selectedProduct is null)
            {
                ModelState.AddModelError("NewLine.ProductId", "L'article selectionne est introuvable ou n'est pas stocke.");
            }

            StatusMessage = "La ligne n'a pas ete ajoutee. Corrige les champs signales puis recommence.";
            return Page();
        }

        List<StockDocumentLine> linesToCreate;
        try
        {
            linesToCreate = BuildLinesForInsert(NewLine, selectedProduct, RequiresSerializedOutputs);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            StatusMessage = "La ligne n'a pas ete ajoutee. Corrige les champs signales puis recommence.";
            return Page();
        }

        DbContext.StockDocumentLines.AddRange(linesToCreate.Select(line =>
        {
            line.StockDocumentId = entity.Id;
            return line;
        }));
        try
        {
            await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            StatusMessage = "Le document a ete modifie entre-temps. Recharge l'ecran puis recommence.";
            return RedirectToPage("/StockDocuments/Edit", new { id = Id });
        }
        catch (DbUpdateException exception)
        {
            ModelState.AddModelError(string.Empty, $"La ligne n'a pas pu etre enregistree : {exception.GetBaseException().Message}");
            StatusMessage = "La ligne n'a pas ete ajoutee. Consulte le message d'erreur dans le formulaire.";
            return Page();
        }
        catch (Exception exception)
        {
            ModelState.AddModelError(string.Empty, $"La ligne n'a pas pu etre enregistree : {exception.Message}");
            StatusMessage = "La ligne n'a pas ete ajoutee. Consulte le message d'erreur dans le formulaire.";
            return Page();
        }

        StatusMessage = linesToCreate.Count == 1
            ? "Ligne ajoutee."
            : $"{linesToCreate.Count} lignes serialisees ajoutees.";
        return RedirectToPage("/StockDocuments/Edit", new { id = Id });
    }

    public async Task<IActionResult> OnPostDeleteLineAsync(Guid lineId)
    {
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.StockDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        if (entity.Status != StockDocumentStatus.Draft)
        {
            StatusMessage = "Ce document ne peut plus etre modifie.";
            return RedirectToPage("/StockDocuments/Details", new { id = Id });
        }

        var lineExists = await DbContext.StockDocumentLines
            .AsNoTracking()
            .AnyAsync(x => x.Id == lineId && x.StockDocumentId == entity.Id, HttpContext.RequestAborted);

        if (!lineExists)
        {
            return NotFound();
        }

        try
        {
            await DbContext.StockDocumentLines
                .Where(x => x.Id == lineId && x.StockDocumentId == entity.Id)
                .ExecuteDeleteAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            StatusMessage = "La ligne etait deja supprimee ou a ete modifiee entre-temps.";
            return RedirectToPage("/StockDocuments/Edit", new { id = Id });
        }

        StatusMessage = "Ligne supprimee.";
        return RedirectToPage("/StockDocuments/Edit", new { id = Id });
    }

    private async Task<IActionResult> LoadPageAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.StockDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        Input = StockDocumentInputModel.FromEntity(entity);
        await LoadLookupsAsync();
        await LoadLinesAsync();
        return Page();
    }

    private async Task LoadLookupsAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Warehouses = await DbContext.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Code)
            .Select(x => new SelectListItem($"{x.Code} - {x.Label}", x.Id.ToString()))
            .ToListAsync(HttpContext.RequestAborted);

        var products = await DbContext.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && x.TrackStock)
            .OrderBy(x => x.Sku)
            .Select(x => new ProductTrackingOption(
                x.Id,
                x.Sku,
                x.Label,
                x.Description,
                x.UnitOfMeasure,
                x.StockIdentityTrackingMode,
                x.PurchasePrice))
            .ToListAsync(HttpContext.RequestAborted);

        Products = products.Select(x => new SelectListItem($"{x.Sku} - {x.Label}", x.Id.ToString())).ToList();
        ProductTrackingMetadataJson = JsonSerializer.Serialize(products.ToDictionary(
            x => x.Id.ToString(),
            x => new
            {
                mode = x.TrackingMode.ToString(),
                sku = x.Sku,
                label = x.Label,
                description = x.Description,
                unit = x.UnitOfMeasure,
                purchasePrice = x.PurchasePrice
            }));
    }

    private async Task LoadLinesAsync()
    {
        Lines = await DbContext.StockDocumentLines
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.StockDocumentId == Id)
            .OrderBy(x => x.CreatedOnUtc)
            .Select(x => new StockDocumentLineItem(
                x.Id,
                x.Product != null ? x.Product.Sku : "-",
                x.Description,
                x.Quantity,
                x.UnitCost,
                x.LotNumber,
                x.SerialNumber,
                x.ExpirationDate))
            .ToListAsync(HttpContext.RequestAborted);
    }

    private void ValidateWarehouses()
    {
        if (UsesSourceWarehouse && !Input.SourceWarehouseId.HasValue)
        {
            ModelState.AddModelError("Input.SourceWarehouseId", "Selectionne un depot source.");
        }

        if (UsesDestinationWarehouse && !Input.DestinationWarehouseId.HasValue)
        {
            ModelState.AddModelError("Input.DestinationWarehouseId", "Selectionne un depot destination.");
        }

        if (CurrentType == StockDocumentType.Transfer && Input.SourceWarehouseId == Input.DestinationWarehouseId)
        {
            ModelState.AddModelError(string.Empty, "Le depot source et le depot destination doivent etre differents.");
        }
    }

    private static List<StockDocumentLine> BuildLinesForInsert(
        StockDocumentLineInputModel input,
        ProductTrackingOption product,
        bool requiresSerializedOutputs)
    {
        if (product.TrackingMode != StockIdentityTrackingMode.SerialNumber || !requiresSerializedOutputs)
        {
            if (product.TrackingMode == StockIdentityTrackingMode.Lot && requiresSerializedOutputs)
            {
                var lots = LotLineBatchParser.Parse(input.LotEntryMode, input.LotNumber, input.Quantity, input.LotBreakdown);
                if (lots.Count == 0)
                {
                    throw new InvalidOperationException($"L'article {product.Sku} est gere par lot. Saisis un lot unique ou une repartition multi-lots.");
                }

                return lots.Select(lot => new StockDocumentLine
                {
                    ProductId = input.ProductId,
                    Description = input.Description.Trim(),
                    Quantity = lot.Quantity,
                    UnitCost = input.UnitCost,
                    LotNumber = lot.LotNumber,
                    ExpirationDate = input.ExpirationDate
                }).ToList();
            }

            var line = new StockDocumentLine();
            input.ApplyTo(line);
            return [line];
        }

        var serials = SerializedLineBatchParser.Parse(
            input.SerialEntryMode,
            input.SerialNumber,
            input.SerialNumberList,
            input.SerialRangeStart,
            input.SerialRangeEnd);

        if (serials.Count == 0)
        {
            throw new InvalidOperationException($"L'article {product.Sku} est serialize. Saisis les numeros de serie par enumeration ou par plage.");
        }

        return serials.Select(serial => new StockDocumentLine
        {
            ProductId = input.ProductId,
            Description = input.Description.Trim(),
            Quantity = 1m,
            UnitCost = input.UnitCost,
            LotNumber = string.IsNullOrWhiteSpace(input.LotNumber) ? null : input.LotNumber.Trim().ToUpperInvariant(),
            SerialNumber = serial,
            ExpirationDate = input.ExpirationDate
        }).ToList();
    }
}

public sealed record StockDocumentLineItem(
    Guid Id,
    string ProductCode,
    string Description,
    decimal Quantity,
    decimal UnitCost,
    string? LotNumber,
    string? SerialNumber,
    DateOnly? ExpirationDate);

sealed record ProductTrackingOption(
    Guid Id,
    string Sku,
    string Label,
    string? Description,
    string UnitOfMeasure,
    StockIdentityTrackingMode TrackingMode,
    decimal PurchasePrice);
