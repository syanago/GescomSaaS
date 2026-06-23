using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.PurchaseDocuments;

[Authorize]
public class CreateModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ICommercialDocumentWorkflowService workflowService,
    INumberingService numberingService,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.PurchasesDocumentsManage];

    private static readonly BusinessPartnerType[] AllowedPartnerTypes =
    [
        BusinessPartnerType.Supplier,
        BusinessPartnerType.Both
    ];

    [BindProperty(SupportsGet = true)]
    public string Type { get; set; } = CommercialDocumentType.PurchaseRequest.ToString();

    [BindProperty]
    public PurchaseDocumentInputModel Input { get; set; } = new();

    [BindProperty]
    public AssistedPartnerEntryInputModel PartnerEntry { get; set; } = new();

    public IReadOnlyList<PartnerLookupOption> PartnerOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Warehouses { get; private set; } = [];
    public QuotaUsageItem? DocumentQuota { get; private set; }
    public DateOnly QuotaReferenceDate { get; private set; }
    public PartnerLookupMode PartnerLookupMode { get; private set; } = GescomSaas.Domain.Enums.PartnerLookupMode.Code;
    public IReadOnlyList<SelectListItem> Statuses { get; } =
    [
        new("Brouillon", CommercialDocumentStatus.Draft.ToString()),
        new("Ouvert", CommercialDocumentStatus.Open.ToString()),
        new("Cloture", CommercialDocumentStatus.Completed.ToString()),
        new("Annule", CommercialDocumentStatus.Cancelled.ToString())
    ];

    public async Task OnGetAsync()
    {
        var documentType = PurchaseDocumentCatalog.Normalize(Type);
        Type = documentType.ToString();
        var tenantId = await GetTenantIdAsync();
        var draft = await workflowService.InitializeDraftAsync(tenantId, documentType, HttpContext.RequestAborted);
        Input = PurchaseDocumentInputModel.FromEntity(draft);
        Input.Status = CommercialDocumentStatus.Draft;
        await LoadLookupsAsync();
        await LoadQuotasAsync(Input.DocumentDate);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var documentType = PurchaseDocumentCatalog.Normalize(Type);
        Type = documentType.ToString();
        Input.DocumentType = documentType;
        await LoadLookupsAsync();
        await ResolvePartnerAsync();
        await LoadQuotasAsync(Input.DocumentDate);

        ModelState.Remove("Input.PartnerId");
        ModelState.ClearValidationState(nameof(Input));
        var inputIsValid = TryValidateModel(Input, nameof(Input));

        if (!inputIsValid)
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
        return RedirectToPage("/PurchaseDocuments/Edit", new { id = entity.Id });
    }

    public string Title => $"Nouveau {PurchaseDocumentCatalog.Label(PurchaseDocumentCatalog.Normalize(Type)).ToLowerInvariant()}";
    public string PartnerLookupLabel => PartnerLookupMode == GescomSaas.Domain.Enums.PartnerLookupMode.Code ? "Code du fournisseur" : "Nom du fournisseur";
    public string PartnerLookupPlaceholder => PartnerLookupMode == GescomSaas.Domain.Enums.PartnerLookupMode.Code ? "Exemple : FOU-0001" : "Exemple : Atelier Nova";

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
    }

    private async Task LoadQuotasAsync(DateOnly documentDate)
    {
        var tenantId = await GetTenantIdAsync();
        QuotaReferenceDate = documentDate;
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, documentDate, HttpContext.RequestAborted);
        DocumentQuota = quotas.FirstOrDefault(x => x.Label == "Documents du mois");
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
