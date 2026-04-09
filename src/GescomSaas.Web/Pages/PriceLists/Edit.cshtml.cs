using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.PriceLists;

[Authorize]
public class EditModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public PriceListInputModel Input { get; set; } = new();

    [BindProperty]
    public PriceListLineInputModel NewLine { get; set; } = new();

    public IReadOnlyList<SelectListItem> Products { get; private set; } = [];
    public IReadOnlyList<PriceListLineItem> Lines { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        return await LoadPageAsync();
    }

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
        var entity = await DbContext.PriceLists.FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (entity is null)
        {
            return NotFound();
        }

        var code = Input.Code.Trim().ToUpperInvariant();
        if (await DbContext.PriceLists.AnyAsync(x => x.TenantId == tenantId && x.Code == code && x.Id != Id, HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Code", "Ce code existe deja.");
            await LoadLookupsAsync();
            await LoadLinesAsync();
            return Page();
        }

        if (Input.IsDefault)
        {
            var defaults = await DbContext.PriceLists.Where(x => x.TenantId == tenantId && x.IsDefault && x.Id != Id).ToListAsync(HttpContext.RequestAborted);
            foreach (var item in defaults)
            {
                item.IsDefault = false;
            }
        }

        Input.ApplyTo(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{entity.Code} mis a jour.";
        return RedirectToPage("/PriceLists/Edit", new { id = Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.PriceLists.FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (entity is null)
        {
            return NotFound();
        }

        Input = PriceListInputModel.FromEntity(entity);

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

        var productExists = await DbContext.Products.AnyAsync(x => x.Id == NewLine.ProductId.Value && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (!productExists)
        {
            ModelState.AddModelError("NewLine.ProductId", "Article introuvable.");
            await LoadLookupsAsync();
            await LoadLinesAsync();
            return Page();
        }

        var line = new PriceListLine
        {
            PriceListId = Id
        };
        NewLine.ApplyTo(line);

        DbContext.PriceListLines.Add(line);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = "Ligne tarifaire ajoutee.";
        return RedirectToPage("/PriceLists/Edit", new { id = Id });
    }

    public async Task<IActionResult> OnPostDeleteLineAsync(Guid lineId)
    {
        var tenantId = await GetTenantIdAsync();
        var line = await DbContext.PriceListLines
            .Include(x => x.PriceList)
            .FirstOrDefaultAsync(x => x.Id == lineId && x.PriceList != null && x.PriceList.TenantId == tenantId && x.PriceListId == Id, HttpContext.RequestAborted);

        if (line is null)
        {
            return NotFound();
        }

        DbContext.PriceListLines.Remove(line);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = "Ligne tarifaire supprimee.";
        return RedirectToPage("/PriceLists/Edit", new { id = Id });
    }

    private async Task<IActionResult> LoadPageAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.PriceLists.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (entity is null)
        {
            return NotFound();
        }

        Input = PriceListInputModel.FromEntity(entity);
        await LoadLookupsAsync();
        await LoadLinesAsync();
        return Page();
    }

    private async Task LoadLookupsAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Products = await DbContext.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Sku)
            .Select(x => new SelectListItem($"{x.Sku} - {x.Label}", x.Id.ToString()))
            .ToListAsync(HttpContext.RequestAborted);
    }

    private async Task LoadLinesAsync()
    {
        Lines = await DbContext.PriceListLines
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.PriceListId == Id)
            .OrderBy(x => x.Product!.Sku)
            .ThenBy(x => x.ValidFrom)
            .Select(x => new PriceListLineItem(
                x.Id,
                x.Product != null ? x.Product.Sku : "-",
                x.Product != null ? x.Product.Label : "Article",
                x.UnitPrice,
                x.ValidFrom,
                x.ValidTo))
            .ToListAsync(HttpContext.RequestAborted);
    }
}

public sealed record PriceListLineItem(
    Guid Id,
    string ProductCode,
    string ProductLabel,
    decimal UnitPrice,
    DateOnly? ValidFrom,
    DateOnly? ValidTo);
