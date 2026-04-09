using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.StockDocuments;

[Authorize]
public class DeleteModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public StockDocumentDeleteViewModel? Document { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var document = await DbContext.StockDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (document is null)
        {
            return NotFound();
        }

        if (document.Status != StockDocumentStatus.Draft)
        {
            StatusMessage = "Seuls les documents brouillon peuvent etre supprimes.";
            return RedirectToPage("/StockDocuments/Details", new { id = Id });
        }

        try
        {
            await DbContext.StockDocuments
                .Where(x => x.Id == document.Id && x.TenantId == tenantId && x.Status == StockDocumentStatus.Draft)
                .ExecuteDeleteAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            StatusMessage = "Le document etait deja supprime ou modifie entre-temps.";
            return RedirectToPage("/StockDocuments/Index", new { type = document.DocumentType });
        }

        StatusMessage = "Document de stock supprime.";
        return RedirectToPage("/StockDocuments/Index", new { type = document.DocumentType });
    }

    private async Task<IActionResult> LoadAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Document = await DbContext.StockDocuments
            .AsNoTracking()
            .Where(x => x.Id == Id && x.TenantId == tenantId)
            .Select(x => new StockDocumentDeleteViewModel(
                x.Id,
                x.Number,
                x.DocumentType,
                x.Status,
                x.DocumentDate))
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        return Document is null ? NotFound() : Page();
    }
}

public sealed record StockDocumentDeleteViewModel(
    Guid Id,
    string Number,
    StockDocumentType DocumentType,
    StockDocumentStatus Status,
    DateOnly DocumentDate);
