using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SaaSAdmin.Invoices;

[Authorize(Roles = "PlatformAdmin")]
public class DetailsModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IPlatformInvoicePdfService platformInvoicePdfService) : PlatformAdminPageModel(dbContext, userManager)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public PlatformInvoice? Invoice { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnGetPdfAsync()
    {
        var pdf = await platformInvoicePdfService.GeneratePdfAsync(Id, HttpContext.RequestAborted);
        return File(pdf.Content, pdf.ContentType, pdf.FileName);
    }

    public async Task<IActionResult> OnPostMarkPaidAsync()
    {
        var invoice = await DbContext.PlatformInvoices.FirstOrDefaultAsync(x => x.Id == Id, HttpContext.RequestAborted);
        if (invoice is null) return NotFound();
        invoice.Status = PlatformInvoiceStatus.Paid;
        invoice.PaidOn = DateOnly.FromDateTime(DateTime.UtcNow);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{invoice.InvoiceNumber} marquee comme payee.";
        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostCancelAsync()
    {
        var invoice = await DbContext.PlatformInvoices.FirstOrDefaultAsync(x => x.Id == Id, HttpContext.RequestAborted);
        if (invoice is null) return NotFound();
        invoice.Status = PlatformInvoiceStatus.Cancelled;
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{invoice.InvoiceNumber} annulee.";
        return RedirectToPage(new { id = Id });
    }

    private async Task<IActionResult> LoadAsync()
    {
        Invoice = await DbContext.PlatformInvoices
            .AsNoTracking()
            .Include(x => x.Tenant)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == Id, HttpContext.RequestAborted);

        return Invoice is null ? NotFound() : Page();
    }
}
