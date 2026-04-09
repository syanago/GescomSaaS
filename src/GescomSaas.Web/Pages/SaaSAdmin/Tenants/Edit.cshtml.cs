using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SaaSAdmin.Tenants;

[Authorize(Roles = "PlatformAdmin")]
public class EditModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IPlatformAdministrationService platformAdministrationService,
    ITenantDisplayFormatter displayFormatter) : PlatformAdminPageModel(dbContext, userManager)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public TenantInputModel Input { get; set; } = new();

    public TenantAdminSummary? Summary { get; private set; }
    public IReadOnlyList<SelectListItem> Plans { get; private set; } = [];
    public IReadOnlyList<TenantUserItem> Users { get; private set; } = [];
    public IReadOnlyList<TenantInvoiceItem> Invoices { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadPlansAsync();
        if (!ModelState.IsValid)
        {
            await LoadSidePanelsAsync();
            return Page();
        }

        var tenant = await DbContext.Tenants.FirstOrDefaultAsync(x => x.Id == Id, HttpContext.RequestAborted);
        if (tenant is null)
        {
            return NotFound();
        }

        if (await DbContext.Tenants.AnyAsync(x => x.Slug == Input.Slug.Trim().ToLowerInvariant() && x.Id != Id, HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Slug", "Ce slug existe deja.");
            await LoadSidePanelsAsync();
            return Page();
        }

        var subscription = await DbContext.TenantSubscriptions
            .OrderByDescending(x => x.StartsOn)
            .FirstOrDefaultAsync(x => x.TenantId == Id, HttpContext.RequestAborted)
            ?? new TenantSubscription { TenantId = Id };

        Input.ApplyTo(tenant, subscription);

        if (subscription.Id == Guid.Empty)
        {
            DbContext.TenantSubscriptions.Add(subscription);
        }

        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{tenant.CompanyName} mis a jour.";
        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostGenerateInvoiceAsync()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var invoice = await platformAdministrationService.GeneratePlatformInvoiceAsync(Id, today, today.AddDays(15), HttpContext.RequestAborted);
            StatusMessage = $"Facture plateforme {invoice.InvoiceNumber} generee.";
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }

        return RedirectToPage(new { id = Id });
    }

    private async Task<IActionResult> LoadAsync()
    {
        var tenant = await DbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == Id, HttpContext.RequestAborted);

        if (tenant is null)
        {
            return NotFound();
        }

        var subscription = await DbContext.TenantSubscriptions
            .AsNoTracking()
            .OrderByDescending(x => x.StartsOn)
            .FirstOrDefaultAsync(x => x.TenantId == Id, HttpContext.RequestAborted);

        Input = TenantInputModel.FromEntity(tenant, subscription);
        await LoadPlansAsync();
        await LoadSidePanelsAsync();
        return Page();
    }

    private async Task LoadPlansAsync()
    {
        var plans = await DbContext.SubscriptionPlans
            .AsNoTracking()
            .OrderBy(x => x.MonthlyPrice)
            .ThenBy(x => x.Label)
            .ToListAsync(HttpContext.RequestAborted);

        Plans = plans
            .Select(x => new SelectListItem($"{x.Label} ({displayFormatter.Money(x.MonthlyPrice)})", x.Id.ToString()))
            .ToList();
    }

    private async Task LoadSidePanelsAsync()
    {
        Summary = (await platformAdministrationService.GetTenantSummariesAsync(HttpContext.RequestAborted)).FirstOrDefault(x => x.TenantId == Id);

        var tenantUsers = await UserManager.Users
            .AsNoTracking()
            .Where(x => x.TenantId == Id)
            .OrderBy(x => x.Email)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.FirstName,
                x.LastName
            })
            .ToListAsync(HttpContext.RequestAborted);

        Users = tenantUsers
            .Select(x => new TenantUserItem(
                x.Id,
                BuildDisplayName(x.FirstName, x.LastName, x.Email),
                x.Email ?? string.Empty))
            .ToList();

        Invoices = await DbContext.PlatformInvoices
            .AsNoTracking()
            .Where(x => x.TenantId == Id)
            .OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Take(8)
            .Select(x => new TenantInvoiceItem(x.Id, x.InvoiceNumber, x.IssueDate, x.DueDate, x.Status, x.TotalIncludingTax, x.CurrencyCode))
            .ToListAsync(HttpContext.RequestAborted);
    }

    private static string BuildDisplayName(string? firstName, string? lastName, string? email)
    {
        var values = new[] { firstName, lastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();

        return values.Length > 0 ? string.Join(' ', values) : email ?? string.Empty;
    }
}

public sealed record TenantUserItem(string UserId, string DisplayName, string Email);
public sealed record TenantInvoiceItem(Guid Id, string InvoiceNumber, DateOnly IssueDate, DateOnly DueDate, PlatformInvoiceStatus Status, decimal TotalIncludingTax, string CurrencyCode);
