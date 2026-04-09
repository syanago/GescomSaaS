using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Settings;

[Authorize(Roles = "TenantOwner,PlatformAdmin")]
public class CompanyModel(
    ApplicationDbContext dbContext,
    GescomSaas.Application.Contracts.ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty]
    public CompanyInputModel Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> SeparatorOptions { get; } =
    [
        new(".", "."),
        new(",", ","),
        new("Espace", "space"),
        new("Aucun", "none")
    ];

    public IReadOnlyList<SelectListItem> CurrencyOptions { get; } =
    [
        new("CAD - Dollar canadien", "CAD"),
        new("USD - Dollar americain", "USD"),
        new("EUR - Euro", "EUR"),
        new("GBP - Livre sterling", "GBP"),
        new("CHF - Franc suisse", "CHF"),
        new("XOF - Franc CFA BCEAO", "XOF"),
        new("XAF - Franc CFA BEAC", "XAF"),
        new("MAD - Dirham marocain", "MAD"),
        new("DZD - Dinar algerien", "DZD"),
        new("TND - Dinar tunisien", "TND"),
        new("GNF - Franc guineen", "GNF"),
        new("CDF - Franc congolais", "CDF"),
        new("NGN - Naira nigerian", "NGN"),
        new("GHS - Cedi ghaneen", "GHS"),
        new("ZAR - Rand sud-africain", "ZAR")
    ];

    public IReadOnlyDictionary<string, string> CurrencySymbolMap { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["CAD"] = "$",
        ["USD"] = "$",
        ["EUR"] = "\u20AC",
        ["GBP"] = "\u00A3",
        ["CHF"] = "CHF",
        ["XOF"] = "F",
        ["XAF"] = "F",
        ["MAD"] = "DH",
        ["DZD"] = "DA",
        ["TND"] = "DT",
        ["GNF"] = "FG",
        ["CDF"] = "FC",
        ["NGN"] = "\u20A6",
        ["GHS"] = "GH\u20B5",
        ["ZAR"] = "R"
    };

    public IReadOnlyList<SelectListItem> ThemeOptions { get; } =
    [
        new("LigCom Nuit - gabarit actuel", ApplicationTheme.LigComMidnight.ToString()),
        new("LigCom Vert Clair - 75 % vert 2, 25 % vert 1", ApplicationTheme.LigComEmeraldLight.ToString())
    ];

    public IReadOnlyList<SelectListItem> CurrencySymbolPositionOptions { get; } =
    [
        new("Avant le montant", CurrencySymbolPosition.BeforeAmount.ToString()),
        new("Apres le montant", CurrencySymbolPosition.AfterAmount.ToString())
    ];

    public IReadOnlyList<SelectListItem> StockValuationMethodOptions { get; } =
    [
        new("CMUP", StockValuationMethod.Cmup.ToString()),
        new("FIFO", StockValuationMethod.Fifo.ToString()),
        new("Dernier prix d'achat", StockValuationMethod.LastPurchaseCost.ToString())
    ];

    public async Task<IActionResult> OnGetAsync()
    {
        var tenant = await LoadTenantAsync();
        Input = CompanyInputModel.FromEntity(tenant);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var tenant = await LoadTenantAsync();
        var previousCurrencyCode = tenant.CurrencyCode;
        var normalizedMoney = NormalizeSeparators(Input.MoneyDecimalSeparator, Input.MoneyGroupSeparator, Input.MoneyDecimalPlaces);
        var normalizedQuantity = NormalizeSeparators(Input.QuantityDecimalSeparator, Input.QuantityGroupSeparator, Input.QuantityDecimalPlaces);
        var autoAdjustedMoneyGroup = !string.Equals(Input.MoneyGroupSeparator, normalizedMoney.GroupSeparator, StringComparison.Ordinal);
        var autoAdjustedQuantityGroup = !string.Equals(Input.QuantityGroupSeparator, normalizedQuantity.GroupSeparator, StringComparison.Ordinal);

        Input.MoneyDecimalSeparator = normalizedMoney.DecimalSeparator;
        Input.MoneyGroupSeparator = normalizedMoney.GroupSeparator;
        Input.QuantityDecimalSeparator = normalizedQuantity.DecimalSeparator;
        Input.QuantityGroupSeparator = normalizedQuantity.GroupSeparator;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Input.ApplyTo(tenant);
        var currencyWasChanged = !string.Equals(previousCurrencyCode, tenant.CurrencyCode, StringComparison.OrdinalIgnoreCase);

        if (currencyWasChanged)
        {
            await SynchronizeTenantCurrencyAsync(tenant.Id, previousCurrencyCode, tenant.CurrencyCode);
        }

        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = BuildStatusMessage(currencyWasChanged, autoAdjustedMoneyGroup, autoAdjustedQuantityGroup);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResynchronizeCurrenciesAsync()
    {
        var tenant = await LoadTenantAsync();

        await ForceSynchronizeTenantCurrencyAsync(tenant.Id, tenant.CurrencyCode);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"Les devises existantes du tenant ont ete realignees sur {tenant.CurrencyCode}.";
        return RedirectToPage();
    }

    private async Task<GescomSaas.Domain.Entities.SaaS.Tenant> LoadTenantAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var tenant = await DbContext.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, HttpContext.RequestAborted);
        if (tenant is null)
        {
            throw new InvalidOperationException("Tenant introuvable.");
        }

        return tenant;
    }

    private async Task SynchronizeTenantCurrencyAsync(Guid tenantId, string previousCurrencyCode, string newCurrencyCode)
    {
        var oldCode = previousCurrencyCode.Trim().ToUpperInvariant();
        var nextCode = newCurrencyCode.Trim().ToUpperInvariant();

        if (string.Equals(oldCode, nextCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var documents = await DbContext.CommercialDocuments
            .Where(x => x.TenantId == tenantId && x.CurrencyCode == oldCode)
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var document in documents)
        {
            document.CurrencyCode = nextCode;
        }

        var payments = await DbContext.Payments
            .Where(x => x.TenantId == tenantId && x.CurrencyCode == oldCode)
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var payment in payments)
        {
            payment.CurrencyCode = nextCode;
        }

        var priceLists = await DbContext.PriceLists
            .Where(x => x.TenantId == tenantId && x.CurrencyCode == oldCode)
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var priceList in priceLists)
        {
            priceList.CurrencyCode = nextCode;
        }

        var platformInvoices = await DbContext.PlatformInvoices
            .Where(x => x.TenantId == tenantId && x.CurrencyCode == oldCode)
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var invoice in platformInvoices)
        {
            invoice.CurrencyCode = nextCode;
        }
    }

    private async Task ForceSynchronizeTenantCurrencyAsync(Guid tenantId, string currencyCode)
    {
        var nextCode = currencyCode.Trim().ToUpperInvariant();

        var documents = await DbContext.CommercialDocuments
            .Where(x => x.TenantId == tenantId && x.CurrencyCode != nextCode)
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var document in documents)
        {
            document.CurrencyCode = nextCode;
        }

        var payments = await DbContext.Payments
            .Where(x => x.TenantId == tenantId && x.CurrencyCode != nextCode)
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var payment in payments)
        {
            payment.CurrencyCode = nextCode;
        }

        var priceLists = await DbContext.PriceLists
            .Where(x => x.TenantId == tenantId && x.CurrencyCode != nextCode)
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var priceList in priceLists)
        {
            priceList.CurrencyCode = nextCode;
        }

        var platformInvoices = await DbContext.PlatformInvoices
            .Where(x => x.TenantId == tenantId && x.CurrencyCode != nextCode)
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var invoice in platformInvoices)
        {
            invoice.CurrencyCode = nextCode;
        }
    }

    private static string NormalizeSeparator(string value) => value switch
    {
        "space" => " ",
        "none" => string.Empty,
        _ => value
    };

    private static (string DecimalSeparator, string GroupSeparator) NormalizeSeparators(string decimalSeparator, string groupSeparator, int decimalPlaces)
    {
        if (decimalPlaces <= 0)
        {
            return (decimalSeparator, groupSeparator);
        }

        return NormalizeSeparator(decimalSeparator) == NormalizeSeparator(groupSeparator)
            ? (decimalSeparator, "none")
            : (decimalSeparator, groupSeparator);
    }

    private static string BuildStatusMessage(bool currencyWasChanged, bool autoAdjustedMoneyGroup, bool autoAdjustedQuantityGroup)
    {
        var messages = new List<string>();

        messages.Add(currencyWasChanged
            ? "Parametres de societe mis a jour et devise synchronisee sur les donnees existantes du tenant."
            : "Parametres de societe mis a jour.");

        if (autoAdjustedMoneyGroup)
        {
            messages.Add("Le separateur de milliers de la monnaie a ete bascule sur 'Aucun' pour eviter un conflit avec le separateur decimal.");
        }

        if (autoAdjustedQuantityGroup)
        {
            messages.Add("Le separateur de milliers des quantites a ete bascule sur 'Aucun' pour eviter un conflit avec le separateur decimal.");
        }

        return string.Join(" ", messages);
    }
}
