using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GescomSaas.Web.Pages.Finance;

[Authorize]
public class RegisterPaymentModel(
    GescomSaas.Infrastructure.Persistence.ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    ISettlementService settlementService,
    ITenantDisplayFormatter displayFormatter) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = FinanceScope.Receivables;

    [BindProperty(SupportsGet = true)]
    public Guid? DocumentId { get; set; }

    [BindProperty]
    public PaymentEntryInputModel Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> Documents { get; private set; } = [];

    public IReadOnlyList<SelectListItem> Methods { get; } =
    [
        new("Virement", PaymentMethod.BankTransfer.ToString()),
        new("Especes", PaymentMethod.Cash.ToString()),
        new("Cheque", PaymentMethod.Check.ToString()),
        new("Carte", PaymentMethod.Card.ToString()),
        new("Mobile Money", PaymentMethod.MobileMoney.ToString()),
        new("Autre", PaymentMethod.Other.ToString())
    ];

    public async Task OnGetAsync()
    {
        Scope = FinanceScope.Normalize(Scope);
        await LoadLookupsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Scope = FinanceScope.Normalize(Scope);
        await LoadLookupsAsync();

        if (!ModelState.IsValid || !Input.DocumentId.HasValue)
        {
            if (!Input.DocumentId.HasValue)
            {
                ModelState.AddModelError("Input.DocumentId", "Selectionnez une facture.");
            }

            return Page();
        }

        var tenantId = await GetTenantIdAsync();

        try
        {
            await settlementService.RegisterPaymentAsync(
                tenantId,
                new PaymentRegistrationRequest(
                    Input.DocumentId.Value,
                    Input.PaymentDate,
                    Input.Amount,
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

        StatusMessage = "Reglement enregistre.";
        return RedirectToPage("/Finance/OpenItems", new { scope = Scope });
    }

    public string Title => $"Saisir un reglement {(FinanceScope.Normalize(Scope) == FinanceScope.Payables ? "fournisseur" : "client")}";

    private async Task LoadLookupsAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var items = await settlementService.GetOpenItemsAsync(tenantId, FinanceScope.ToDirection(Scope), HttpContext.RequestAborted);
        Documents = items
            .Select(x => new SelectListItem($"{x.Number} - {x.PartnerName} - {displayFormatter.Money(x.BalanceAmount, x.CurrencyCode)}", x.DocumentId.ToString()))
            .ToList();

        var selected = DocumentId ?? Input.DocumentId;
        if (selected.HasValue)
        {
            var item = items.FirstOrDefault(x => x.DocumentId == selected.Value);
            if (item is not null)
            {
                Input.DocumentId = item.DocumentId;
                if (Input.Amount <= 0m)
                {
                    Input.Amount = item.BalanceAmount;
                }
            }
        }
    }
}
