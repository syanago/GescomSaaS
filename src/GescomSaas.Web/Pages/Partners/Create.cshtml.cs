using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Partners;

[Authorize]
public class CreateModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    INumberingService numberingService,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty]
    public PartnerInputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = PartnerScope.Customers;

    public IReadOnlyList<SelectListItem> PaymentTerms { get; private set; } = [];
    public QuotaUsageItem? CustomerQuota { get; private set; }
    public QuotaUsageItem? SupplierQuota { get; private set; }

    public IReadOnlyList<SelectListItem> PartnerTypes { get; } =
    [
        new("Client", BusinessPartnerType.Customer.ToString()),
        new("Fournisseur", BusinessPartnerType.Supplier.ToString()),
        new("Mixte", BusinessPartnerType.Both.ToString()),
        new("Prospect", BusinessPartnerType.Prospect.ToString())
    ];

    public async Task OnGetAsync()
    {
        Scope = PartnerScope.Normalize(Scope);
        Input.PartnerType = Scope == PartnerScope.Suppliers ? BusinessPartnerType.Supplier : BusinessPartnerType.Customer;
        var tenantId = await GetTenantIdAsync();
        var rule = await numberingService.GetReferenceRuleAsync(tenantId, GetNumberingScope(), HttpContext.RequestAborted);
        Input.Code = rule.Preview;
        await LoadLookupsAsync();
        await LoadQuotasAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Scope = PartnerScope.Normalize(Scope);
        await LoadLookupsAsync();
        await LoadQuotasAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenantId = await GetTenantIdAsync();
        try
        {
            Input.Code = await numberingService.ResolveReferenceCodeAsync(tenantId, GetNumberingScope(), Input.Code, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("Input.Code", exception.Message);
            return Page();
        }

        if (await DbContext.BusinessPartners.AnyAsync(x => x.TenantId == tenantId && x.Code == Input.Code.Trim(), HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Code", "Ce code existe deja.");
            return Page();
        }

        try
        {
            await tenantQuotaEnforcementService.EnsureCanCreatePartnerAsync(tenantId, Input.PartnerType, Input.IsActive, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }

        var partner = new BusinessPartner
        {
            TenantId = tenantId
        };

        Input.ApplyTo(partner);

        DbContext.BusinessPartners.Add(partner);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{PartnerScope.CreateTitle(Scope)} enregistre.";
        return RedirectToPage("/Partners/Index", new { scope = Scope });
    }

    public string Title => PartnerScope.CreateTitle(Scope);

    private ReferenceNumberingScope GetNumberingScope() =>
        Scope == PartnerScope.Suppliers ? ReferenceNumberingScope.Supplier : ReferenceNumberingScope.Customer;

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

    private async Task LoadQuotasAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, cancellationToken: HttpContext.RequestAborted);
        CustomerQuota = quotas.FirstOrDefault(x => x.Label == "Clients");
        SupplierQuota = quotas.FirstOrDefault(x => x.Label == "Fournisseurs");
    }
}
