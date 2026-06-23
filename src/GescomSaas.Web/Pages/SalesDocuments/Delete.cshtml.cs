using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SalesDocuments;

[Authorize]
public class DeleteModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ICommercialDocumentWorkflowService workflowService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.SalesDocumentsManage];

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public CommercialDocument? Document { get; private set; }
    public string? BlockingReason { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await LoadAsync();
        if (result is NotFoundResult) return result;
        if (!string.IsNullOrWhiteSpace(BlockingReason)) return Page();

        var sourceDocumentId = Document!.SourceDocumentId;

        var stockMovements = await DbContext.StockMovements
            .Where(x => x.ReferenceNumber == Document.Number && x.TenantId == Document.TenantId)
            .ToListAsync(HttpContext.RequestAborted);

        DbContext.StockMovements.RemoveRange(stockMovements);
        DbContext.CommercialDocuments.Remove(Document);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        if (sourceDocumentId.HasValue)
        {
            await workflowService.SynchronizeSourceStatusAsync(sourceDocumentId.Value, HttpContext.RequestAborted);
        }

        StatusMessage = $"{Document.Number} supprime.";
        return RedirectToPage("/SalesDocuments/Index", new { type = Document.DocumentType });
    }

    private async Task<IActionResult> LoadAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Document = await DbContext.CommercialDocuments
            .Include(x => x.DerivedDocuments)
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (Document is null)
        {
            return NotFound();
        }

        BlockingReason = Document.DerivedDocuments.Count > 0
            ? "Cette piece a deja servi de source pour d'autres documents."
            : null;

        return Page();
    }
}
