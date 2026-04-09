using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SaaSAdmin.Invoices;

[Authorize(Roles = "PlatformAdmin")]
public class IndexModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PlatformAdminPageModel(dbContext, userManager)
{
    [BindProperty(SupportsGet = true)]
    public string Status { get; set; } = string.Empty;

    public IReadOnlyList<PlatformInvoiceListItem> Invoices { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var query = DbContext.PlatformInvoices
            .AsNoTracking()
            .Include(x => x.Tenant)
            .AsQueryable();

        if (Enum.TryParse<PlatformInvoiceStatus>(Status, out var parsedStatus))
        {
            query = query.Where(x => x.Status == parsedStatus);
        }

        Invoices = await query
            .OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Select(x => new PlatformInvoiceListItem(
                x.Id,
                x.InvoiceNumber,
                x.Tenant != null ? x.Tenant.CompanyName : "-",
                x.IssueDate,
                x.DueDate,
                x.Status,
                x.TotalIncludingTax,
                x.CurrencyCode))
            .ToListAsync(HttpContext.RequestAborted);
    }
}

public sealed record PlatformInvoiceListItem(Guid Id, string InvoiceNumber, string TenantName, DateOnly IssueDate, DateOnly DueDate, PlatformInvoiceStatus Status, decimal TotalIncludingTax, string CurrencyCode);
