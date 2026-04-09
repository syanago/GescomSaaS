using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.StockDocuments;

[Authorize]
public class CreateModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IStockDocumentService stockDocumentService,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public string Type { get; set; } = StockDocumentType.Entry.ToString();

    [BindProperty]
    public StockDocumentInputModel Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> Warehouses { get; private set; } = [];
    public QuotaUsageItem? DocumentQuota { get; private set; }

    public StockDocumentType CurrentType => StockDocumentCatalog.Normalize(Type);
    public bool UsesSourceWarehouse => StockDocumentCatalog.UsesSourceWarehouse(CurrentType);
    public bool UsesDestinationWarehouse => StockDocumentCatalog.UsesDestinationWarehouse(CurrentType);

    public async Task OnGetAsync()
    {
        Type = CurrentType.ToString();
        var tenantId = await GetTenantIdAsync();
        var draft = await stockDocumentService.InitializeDraftAsync(tenantId, CurrentType, HttpContext.RequestAborted);
        Input = StockDocumentInputModel.FromEntity(draft);
        await LoadLookupsAsync();
        await LoadQuotasAsync(Input.DocumentDate);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Type = CurrentType.ToString();
        Input.DocumentType = CurrentType;
        await LoadLookupsAsync();
        await LoadQuotasAsync(Input.DocumentDate);
        ValidateWarehouses();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenantId = await GetTenantIdAsync();
        try
        {
            await tenantQuotaEnforcementService.EnsureCanCreateDocumentAsync(tenantId, Input.DocumentDate, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }

        var entity = await stockDocumentService.InitializeDraftAsync(tenantId, CurrentType, HttpContext.RequestAborted);
        Input.ApplyTo(entity);

        DbContext.StockDocuments.Add(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{entity.Number} cree.";
        return RedirectToPage("/StockDocuments/Edit", new { id = entity.Id });
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
    }

    private async Task LoadQuotasAsync(DateOnly documentDate)
    {
        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, documentDate, HttpContext.RequestAborted);
        DocumentQuota = quotas.FirstOrDefault(x => x.Label == "Documents du mois");
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
}
