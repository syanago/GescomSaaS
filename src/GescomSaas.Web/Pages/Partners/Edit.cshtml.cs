using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Partners;

[Authorize]
public class EditModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesPartnersManage];

    [BindProperty]
    public PartnerInputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = PartnerScope.Customers;

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public IReadOnlyList<SelectListItem> PaymentTerms { get; private set; } = [];
    public QuotaUsageItem? CustomerQuota { get; private set; }
    public QuotaUsageItem? SupplierQuota { get; private set; }
    public BusinessPartnerType OriginalPartnerType { get; private set; }
    public bool OriginalIsActive { get; private set; }

    public IReadOnlyList<SelectListItem> PartnerTypes { get; } =
    [
        new("Client", BusinessPartnerType.Customer.ToString()),
        new("Fournisseur", BusinessPartnerType.Supplier.ToString()),
        new("Mixte", BusinessPartnerType.Both.ToString()),
        new("Prospect", BusinessPartnerType.Prospect.ToString())
    ];

    public async Task<IActionResult> OnGetAsync()
    {
        Scope = PartnerScope.Normalize(Scope);
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        Input = PartnerInputModel.FromEntity(entity);
        OriginalPartnerType = entity.PartnerType;
        OriginalIsActive = entity.IsActive;
        await LoadLookupsAsync();
        await LoadQuotasAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Scope = PartnerScope.Normalize(Scope);
        await LoadLookupsAsync();
        await LoadOriginalStateAsync();
        await LoadQuotasAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.BusinessPartners
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        if (await DbContext.BusinessPartners.AnyAsync(x => x.TenantId == tenantId && x.Code == Input.Code.Trim() && x.Id != Id, HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Code", "Ce code existe deja.");
            return Page();
        }

        try
        {
            await tenantQuotaEnforcementService.EnsureCanUpdatePartnerAsync(tenantId, Id, Input.PartnerType, Input.IsActive, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }

        Input.ApplyTo(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{PartnerScope.Title(Scope).TrimEnd('s')} mis a jour.";
        return RedirectToPage("/Partners/Details", new { id = entity.Id, scope = Scope });
    }

    public string Title => $"Modifier {PartnerScope.Title(Scope).TrimEnd('s').ToLowerInvariant()}";

    private async Task LoadLookupsAsync()
    {
        var tenantId = await GetTenantIdAsync();
        PaymentTerms = await DbContext.PaymentTerms
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new SelectListItem($"{x.Code} - {x.Label}", x.Id.ToString()))
            .ToListAsync(HttpContext.RequestAborted);
    }

    private async Task LoadOriginalStateAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.BusinessPartners
            .AsNoTracking()
            .Where(x => x.Id == Id && x.TenantId == tenantId)
            .Select(x => new
            {
                x.PartnerType,
                x.IsActive
            })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (entity is not null)
        {
            OriginalPartnerType = entity.PartnerType;
            OriginalIsActive = entity.IsActive;
        }
    }

    private async Task LoadQuotasAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, cancellationToken: HttpContext.RequestAborted);
        CustomerQuota = quotas.FirstOrDefault(x => x.Label == "Clients");
        SupplierQuota = quotas.FirstOrDefault(x => x.Label == "Fournisseurs");
    }
}
