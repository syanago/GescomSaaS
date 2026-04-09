using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GescomSaas.Web.Pages.Inventory;

[Authorize]
public class AdjustmentsModel(
    GescomSaas.Infrastructure.Persistence.ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IInventoryService inventoryService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty]
    public StockAdjustmentInputModel Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> Products { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Warehouses { get; private set; } = [];
    public string ProductTrackingMetadataJson { get; private set; } = "{}";

    public IReadOnlyList<SelectListItem> AdjustmentTypes { get; } =
    [
        new("Ajustement positif", StockMovementType.AdjustmentIn.ToString()),
        new("Ajustement négatif", StockMovementType.AdjustmentOut.ToString())
    ];

    public async Task OnGetAsync()
    {
        await LoadLookupsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();

        if (!ModelState.IsValid || !Input.ProductId.HasValue || !Input.WarehouseId.HasValue)
        {
            if (!Input.ProductId.HasValue)
            {
                ModelState.AddModelError("Input.ProductId", "Sélectionnez un article.");
            }

            if (!Input.WarehouseId.HasValue)
            {
                ModelState.AddModelError("Input.WarehouseId", "Sélectionnez un dépôt.");
            }

            return Page();
        }

        var tenantId = await GetTenantIdAsync();

        try
        {
            await inventoryService.RegisterAdjustmentAsync(
                tenantId,
                new StockAdjustmentRequest(
                    Input.ProductId.Value,
                    Input.WarehouseId.Value,
                    Input.MovementDate,
                    Input.MovementType,
                    Input.Quantity,
                    Input.UnitCost,
                    Input.ReferenceNumber,
                    Input.LotNumber,
                    Input.SerialNumber,
                    Input.ExpirationDate),
                HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        StatusMessage = "Ajustement d'inventaire enregistré.";
        return RedirectToPage("/Inventory/Index");
    }

    private async Task LoadLookupsAsync()
    {
        var tenantId = await GetTenantIdAsync();

        var products = await DbContext.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TrackStock && x.IsActive)
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

        Warehouses = await DbContext.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new SelectListItem($"{x.Code} - {x.Label}", x.Id.ToString()))
            .ToListAsync(HttpContext.RequestAborted);
    }

    private sealed record ProductTrackingOption(
        Guid Id,
        string Sku,
        string Label,
        string UnitOfMeasure,
        StockIdentityTrackingMode TrackingMode);
}
