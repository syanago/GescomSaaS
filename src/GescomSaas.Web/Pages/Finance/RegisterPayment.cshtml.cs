using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Finance;

[Authorize]
public class RegisterPaymentModel(
    GescomSaas.Infrastructure.Persistence.ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ISettlementService settlementService,
    INumberingService numberingService,
    ITenantDisplayFormatter displayFormatter) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.FinanceManage];

    private static readonly BusinessPartnerType[] ReceivablePartnerTypes =
    [
        BusinessPartnerType.Customer,
        BusinessPartnerType.Both,
        BusinessPartnerType.Prospect
    ];

    private static readonly BusinessPartnerType[] PayablePartnerTypes =
    [
        BusinessPartnerType.Supplier,
        BusinessPartnerType.Both
    ];

    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = FinanceScope.Receivables;

    [BindProperty(SupportsGet = true)]
    public Guid? DocumentId { get; set; }

    [BindProperty]
    public PaymentEntryInputModel Input { get; set; } = new();

    [BindProperty]
    public AssistedPartnerEntryInputModel PartnerEntry { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid? SelectedPartnerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public PaymentType? PresetType { get; set; }

    public IReadOnlyList<SelectListItem> Documents { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Methods { get; private set; } = [];
    public IReadOnlyList<SelectListItem> PaymentTypes { get; } =
    [
        new("Reglement standard", PaymentType.Standard.ToString()),
        new("Acompte client", PaymentType.Deposit.ToString())
    ];
    public IReadOnlyList<SelectListItem> AllocationModes { get; } =
    [
        new("Saisie manuelle", PaymentAllocationMode.Manual.ToString()),
        new("Affectation auto sur les echeances les plus anciennes", PaymentAllocationMode.OldestDueDate.ToString()),
        new("Affectation auto sur les factures les plus anciennes", PaymentAllocationMode.OldestDocumentDate.ToString())
    ];
    public IReadOnlyList<PartnerLookupOption> PartnerOptions { get; private set; } = [];
    public PartnerLookupMode PartnerLookupMode { get; private set; } = GescomSaas.Domain.Enums.PartnerLookupMode.Code;
    public PaymentAllocationMode DefaultIncomingAllocationMode { get; private set; } = PaymentAllocationMode.Manual;

    public async Task OnGetAsync()
    {
        Scope = FinanceScope.Normalize(Scope);
        if (PresetType.HasValue)
        {
            Input.Type = PresetType.Value;
        }

        await LoadPartnerLookupsAsync();
        await LoadDocumentChoicesAsync();
        if (FinanceScope.ToDirection(Scope) == PaymentDirection.Incoming)
        {
            Input.AllocationMode = DefaultIncomingAllocationMode;
        }

        EnsureSelectedMethodIsAllowed();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Scope = FinanceScope.Normalize(Scope);
        await LoadPartnerLookupsAsync();
        await ResolvePartnerAsync();
        await LoadDocumentChoicesAsync();

        if (!IsCurrentMethodAllowed())
        {
            ModelState.AddModelError("Input.Method", "Le mode de reglement selectionne n'est pas autorise pour ce tenant.");
        }

        if (!SelectedPartnerId.HasValue)
        {
            ModelState.AddModelError("Input.DocumentId", "Selectionnez ou creez d'abord un tiers.");
        }

        if (Input.Type == PaymentType.Standard &&
            !Input.DocumentId.HasValue &&
            (FinanceScope.ToDirection(Scope) != PaymentDirection.Incoming || Input.AllocationMode == PaymentAllocationMode.Manual))
        {
            ModelState.AddModelError("Input.DocumentId", "Selectionnez une facture, choisis une affectation automatique ou enregistrez ce montant comme acompte.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenantId = await GetTenantIdAsync();
        var partnerId = SelectedPartnerId.GetValueOrDefault();

        try
        {
            await settlementService.RegisterPaymentAsync(
                tenantId,
                new PaymentRegistrationRequest(
                    Input.DocumentId,
                    partnerId,
                    FinanceScope.ToDirection(Scope),
                    Input.PaymentDate,
                    Input.Amount,
                    Input.Type,
                    Input.AllocationMode,
                    Input.Method,
                    Input.ReferenceNumber,
                    Input.Notes),
                HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        StatusMessage = Input.Type == PaymentType.Deposit
            ? "Acompte enregistre."
            : "Reglement enregistre.";
        return RedirectToPage("/Finance/OpenItems", new { scope = Scope });
    }

    public string Title => $"Saisir un reglement {(FinanceScope.Normalize(Scope) == FinanceScope.Payables ? "fournisseur" : "client")}";
    public string SubmitLabel => Input.Type == PaymentType.Deposit ? "Enregistrer l'acompte" : "Enregistrer le reglement";
    public string PartnerLookupLabel => PartnerLookupMode == GescomSaas.Domain.Enums.PartnerLookupMode.Code ? "Code du tiers" : "Nom du tiers";
    public string PartnerLookupPlaceholder => PartnerLookupMode == GescomSaas.Domain.Enums.PartnerLookupMode.Code ? "Exemple : CLI-0001" : "Exemple : Maison Atlas";

    private async Task LoadPartnerLookupsAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var partnerTypes = GetAllowedPartnerTypes();
        var partnerContext = await PartnerAssistService.LoadOptionsAsync(DbContext, tenantId, partnerTypes, HttpContext.RequestAborted);
        var tenant = partnerContext.Tenant;
        PartnerLookupMode = tenant.PartnerLookupMode;
        DefaultIncomingAllocationMode = tenant.IncomingPaymentAllocationMode;
        PartnerOptions = partnerContext.Options;

        var allowedMethods = PaymentMethodCatalog.DeserializeSelection(tenant.PaymentMethodsJson);
        Methods = allowedMethods
            .Select(x => new SelectListItem(PaymentMethodCatalog.GetLabel(x), x.ToString()))
            .ToList();
    }

    private async Task LoadDocumentChoicesAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var items = await settlementService.GetOpenItemsAsync(tenantId, FinanceScope.ToDirection(Scope), HttpContext.RequestAborted);

        if (SelectedPartnerId.HasValue)
        {
            items = items.Where(x => x.PartnerId == SelectedPartnerId.Value).ToList();
            if (string.IsNullOrWhiteSpace(PartnerEntry.Lookup))
            {
                var currentPartner = PartnerOptions.FirstOrDefault(x => x.Id == SelectedPartnerId.Value);
                if (currentPartner is not null)
                {
                    PartnerEntry.Lookup = currentPartner.DisplayValue;
                }
            }
        }

        Documents = items
            .Select(x => new SelectListItem($"{x.Number} - {(string.IsNullOrWhiteSpace(x.PartnerCode) ? x.PartnerName : $"{x.PartnerCode} - {x.PartnerName}")} - {displayFormatter.Money(x.BalanceAmount, x.CurrencyCode)}", x.DocumentId.ToString()))
            .ToList();

        var selected = DocumentId ?? Input.DocumentId;
        if (selected.HasValue)
        {
            var item = items.FirstOrDefault(x => x.DocumentId == selected.Value);
            if (item is not null)
            {
                Input.DocumentId = item.DocumentId;
                SelectedPartnerId = item.PartnerId;
                if (string.IsNullOrWhiteSpace(PartnerEntry.Lookup) && item.PartnerId.HasValue)
                {
                    var selectedPartner = PartnerOptions.FirstOrDefault(x => x.Id == item.PartnerId.Value);
                    if (selectedPartner is not null)
                    {
                        PartnerEntry.Lookup = selectedPartner.DisplayValue;
                    }
                }

                if (Input.Amount <= 0m)
                {
                    Input.Amount = item.BalanceAmount;
                }
            }
        }
    }

    private void EnsureSelectedMethodIsAllowed()
    {
        if (Methods.Count == 0)
        {
            return;
        }

        if (!IsCurrentMethodAllowed()
            && Methods[0].Value is { } firstMethodValue
            && Enum.TryParse<PaymentMethod>(firstMethodValue, out var fallbackMethod))
        {
            Input.Method = fallbackMethod;
        }
    }

    private bool IsCurrentMethodAllowed() =>
        Methods.Any(x => string.Equals(x.Value, Input.Method.ToString(), StringComparison.OrdinalIgnoreCase));

    private IReadOnlyCollection<BusinessPartnerType> GetAllowedPartnerTypes() =>
        FinanceScope.Normalize(Scope) == FinanceScope.Payables
            ? PayablePartnerTypes
            : ReceivablePartnerTypes;

    private async Task ResolvePartnerAsync()
    {
        if (string.IsNullOrWhiteSpace(PartnerEntry.Lookup))
        {
            SelectedPartnerId = null;
            return;
        }

        var partnerType = FinanceScope.Normalize(Scope) == FinanceScope.Payables
            ? BusinessPartnerType.Supplier
            : BusinessPartnerType.Customer;
        var numberingScope = FinanceScope.Normalize(Scope) == FinanceScope.Payables
            ? ReferenceNumberingScope.Supplier
            : ReferenceNumberingScope.Customer;
        var tenantId = await GetTenantIdAsync();

        var result = await PartnerAssistService.ResolveOrCreateAsync(
            DbContext,
            numberingService,
            tenantId,
            GetAllowedPartnerTypes(),
            partnerType,
            numberingScope,
            PartnerLookupMode,
            PartnerEntry,
            HttpContext.RequestAborted);

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            ModelState.AddModelError("Input.DocumentId", result.ErrorMessage);
            SelectedPartnerId = null;
            return;
        }

        SelectedPartnerId = result.PartnerId;
        PartnerEntry.Lookup = result.LookupValue ?? PartnerEntry.Lookup;
    }
}
