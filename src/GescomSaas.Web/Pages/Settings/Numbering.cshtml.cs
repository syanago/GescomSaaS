using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Settings;

[Authorize(Roles = "TenantOwner,PlatformAdmin")]
public class NumberingModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    INumberingService numberingService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty]
    public NumberingSettingsInputModel Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> ModeOptions { get; } =
    [
        new("Automatique avec racine", NumberingMode.AutomaticWithPrefix.ToString()),
        new("Manuel avec racine", NumberingMode.ManualWithPrefix.ToString()),
        new("Manuel", NumberingMode.Manual.ToString())
    ];

    public async Task<IActionResult> OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Input = await BuildInputAsync(tenantId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var tenantId = await GetTenantIdAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await SaveReferenceSettingsAsync(tenantId);
        await SaveDocumentSettingsAsync(tenantId);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = "Regles de numerotation mises a jour.";
        return RedirectToPage();
    }

    private async Task<NumberingSettingsInputModel> BuildInputAsync(Guid tenantId)
    {
        var input = new NumberingSettingsInputModel();

        foreach (var item in ReferenceDefinitions)
        {
            var rule = await numberingService.GetReferenceRuleAsync(tenantId, item.Scope, HttpContext.RequestAborted);
            input.References.Add(new NumberingRuleInputModel
            {
                Key = item.Scope.ToString(),
                Label = item.Label,
                Mode = rule.Mode,
                Prefix = rule.Prefix,
                NumberLength = rule.NumberLength,
                Preview = string.IsNullOrWhiteSpace(rule.Preview) ? "Saisie libre" : rule.Preview
            });
        }

        foreach (var item in DocumentDefinitions)
        {
            var rule = await numberingService.GetDocumentRuleAsync(tenantId, item.Type, HttpContext.RequestAborted);
            input.Documents.Add(new NumberingRuleInputModel
            {
                Key = item.Type.ToString(),
                Label = item.Label,
                Mode = rule.Mode,
                Prefix = rule.Prefix,
                NumberLength = rule.NumberLength,
                Preview = string.IsNullOrWhiteSpace(rule.Preview) ? "Saisie libre" : rule.Preview
            });
        }

        return input;
    }

    private async Task SaveReferenceSettingsAsync(Guid tenantId)
    {
        var existing = await DbContext.ReferenceNumberingSettings
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Scope, HttpContext.RequestAborted);

        foreach (var definition in ReferenceDefinitions)
        {
            var row = Input.References.First(x => x.Key == definition.Scope.ToString());
            if (!existing.TryGetValue(definition.Scope, out var setting))
            {
                setting = new ReferenceNumberingSetting
                {
                    TenantId = tenantId,
                    Scope = definition.Scope,
                    NumberLength = 4,
                    NextValue = 1
                };

                DbContext.ReferenceNumberingSettings.Add(setting);
            }

            setting.Mode = row.Mode;
            setting.Prefix = row.Mode == NumberingMode.Manual ? string.Empty : row.Prefix.Trim();
            setting.NumberLength = NormalizeLength(row.NumberLength);
        }
    }

    private async Task SaveDocumentSettingsAsync(Guid tenantId)
    {
        var existing = await DbContext.DocumentSequences
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.DocumentType, HttpContext.RequestAborted);

        foreach (var definition in DocumentDefinitions)
        {
            var row = Input.Documents.First(x => x.Key == definition.Type.ToString());
            if (!existing.TryGetValue(definition.Type, out var sequence))
            {
                sequence = new DocumentSequence
                {
                    TenantId = tenantId,
                    DocumentType = definition.Type,
                    NumberLength = 4,
                    NextValue = 1
                };

                DbContext.DocumentSequences.Add(sequence);
            }

            sequence.Mode = row.Mode;
            sequence.Prefix = row.Mode == NumberingMode.Manual ? string.Empty : row.Prefix.Trim();
            sequence.NumberLength = NormalizeLength(row.NumberLength);
        }
    }

    private static readonly IReadOnlyList<ReferenceDefinition> ReferenceDefinitions =
    [
        new(ReferenceNumberingScope.Customer, "Clients"),
        new(ReferenceNumberingScope.Supplier, "Fournisseurs"),
        new(ReferenceNumberingScope.Product, "Articles"),
        new(ReferenceNumberingScope.Warehouse, "Depots"),
        new(ReferenceNumberingScope.PaymentTerm, "Conditions de paiement"),
        new(ReferenceNumberingScope.TaxCode, "Taxes"),
        new(ReferenceNumberingScope.ProductCategory, "Familles"),
        new(ReferenceNumberingScope.PriceList, "Listes de prix")
    ];

    private static readonly IReadOnlyList<DocumentDefinition> DocumentDefinitions =
    [
        new(CommercialDocumentType.SalesQuote, "Devis"),
        new(CommercialDocumentType.SalesOrder, "Commandes clients"),
        new(CommercialDocumentType.DeliveryNote, "Bons de livraison"),
        new(CommercialDocumentType.SalesInvoice, "Factures clients"),
        new(CommercialDocumentType.SalesCreditNote, "Avoirs clients"),
        new(CommercialDocumentType.PurchaseRequest, "Demandes d'achat"),
        new(CommercialDocumentType.PurchaseOrder, "Commandes fournisseurs"),
        new(CommercialDocumentType.GoodsReceipt, "Receptions"),
        new(CommercialDocumentType.PurchaseInvoice, "Factures fournisseurs"),
        new(CommercialDocumentType.SupplierCreditNote, "Avoirs fournisseurs")
    ];

    public sealed class NumberingSettingsInputModel
    {
        public List<NumberingRuleInputModel> References { get; set; } = [];
        public List<NumberingRuleInputModel> Documents { get; set; } = [];
    }

    public sealed class NumberingRuleInputModel
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public NumberingMode Mode { get; set; } = NumberingMode.AutomaticWithPrefix;
        public string Prefix { get; set; } = string.Empty;
        public int NumberLength { get; set; } = 4;
        public string Preview { get; set; } = string.Empty;
    }

    private static int NormalizeLength(int length) => Math.Clamp(length <= 0 ? 4 : length, 1, 12);

    private sealed record ReferenceDefinition(ReferenceNumberingScope Scope, string Label);
    private sealed record DocumentDefinition(CommercialDocumentType Type, string Label);
}
