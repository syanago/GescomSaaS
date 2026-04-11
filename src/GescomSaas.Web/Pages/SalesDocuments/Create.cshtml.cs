using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SalesDocuments;

[Authorize]
public class CreateModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    ICommercialDocumentWorkflowService workflowService,
    INumberingService numberingService,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public string Type { get; set; } = CommercialDocumentType.SalesQuote.ToString();

    [BindProperty]
    public SalesDocumentInputModel Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> Partners { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Warehouses { get; private set; } = [];
    public QuotaUsageItem? DocumentQuota { get; private set; }
    public DateOnly QuotaReferenceDate { get; private set; }
    public IReadOnlyList<SelectListItem> Statuses { get; } =
    [
        new("Brouillon", CommercialDocumentStatus.Draft.ToString()),
        new("Ouvert", CommercialDocumentStatus.Open.ToString()),
        new("Cloture", CommercialDocumentStatus.Completed.ToString()),
        new("Annule", CommercialDocumentStatus.Cancelled.ToString())
    ];

    public async Task OnGetAsync()
    {
        var documentType = SalesDocumentCatalog.Normalize(Type);
        Type = documentType.ToString();
        var tenantId = await GetTenantIdAsync();
        var draft = await workflowService.InitializeDraftAsync(tenantId, documentType, HttpContext.RequestAborted);
        Input = SalesDocumentInputModel.FromEntity(draft);
        Input.Status = CommercialDocumentStatus.Draft;
        await LoadLookupsAsync();
        await LoadQuotasAsync(Input.DocumentDate);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var documentType = SalesDocumentCatalog.Normalize(Type);
        Type = documentType.ToString();
        Input.DocumentType = documentType;
        await LoadLookupsAsync();
        await LoadQuotasAsync(Input.DocumentDate);

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

        var entity = await workflowService.InitializeDraftAsync(tenantId, documentType, HttpContext.RequestAborted);
        Input.ApplyTo(entity);
        try
        {
            entity.Number = await numberingService.ResolveDocumentNumberAsync(tenantId, documentType, Input.Number, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("Input.Number", exception.Message);
            return Page();
        }

        DbContext.CommercialDocuments.Add(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{entity.Number} cree.";
        return RedirectToPage("/SalesDocuments/Edit", new { id = entity.Id });
    }

    public string Title => $"Nouveau {SalesDocumentCatalog.Label(SalesDocumentCatalog.Normalize(Type)).ToLowerInvariant()}";

    private async Task LoadLookupsAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Partners = await DbContext.BusinessPartners
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && (x.PartnerType == BusinessPartnerType.Customer || x.PartnerType == BusinessPartnerType.Both || x.PartnerType == BusinessPartnerType.Prospect))
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
    }

    private async Task LoadQuotasAsync(DateOnly documentDate)
    {
        var tenantId = await GetTenantIdAsync();
        QuotaReferenceDate = documentDate;
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, documentDate, HttpContext.RequestAborted);
        DocumentQuota = quotas.FirstOrDefault(x => x.Label == "Documents du mois");
    }
}
