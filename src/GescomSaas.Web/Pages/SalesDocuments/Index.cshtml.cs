using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SalesDocuments;

[Authorize]
public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.SalesDocumentsManage];

    [BindProperty(SupportsGet = true)]
    public string Type { get; set; } = CommercialDocumentType.SalesQuote.ToString();

    [BindProperty(SupportsGet = true)]
    public string? PartnerName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PartnerNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DocumentNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateTo { get; set; }

    public IReadOnlyList<SalesDocumentListItem> Documents { get; private set; } = [];
    public CommercialDocumentType CurrentType { get; private set; }
    public QuotaUsageItem? DocumentQuota { get; private set; }
    public IReadOnlyList<SelectListItem> StatusOptions { get; } =
        Enum.GetValues<CommercialDocumentStatus>()
            .Select(status => new SelectListItem(status.ToString(), status.ToString()))
            .ToList();
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(PartnerName) ||
        !string.IsNullOrWhiteSpace(PartnerNumber) ||
        !string.IsNullOrWhiteSpace(DocumentNumber) ||
        !string.IsNullOrWhiteSpace(Status) ||
        DateFrom.HasValue ||
        DateTo.HasValue;

    public async Task OnGetAsync()
    {
        CurrentType = SalesDocumentCatalog.Normalize(Type);
        Type = CurrentType.ToString();

        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, cancellationToken: HttpContext.RequestAborted);
        DocumentQuota = quotas.FirstOrDefault(x => x.Label == "Documents du mois");
        var query = DbContext.CommercialDocuments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.SourceDocument)
            .Where(x => x.TenantId == tenantId && x.DocumentType == CurrentType);

        if (!string.IsNullOrWhiteSpace(PartnerName))
        {
            var partnerNameFilter = PartnerName.Trim();
            query = query.Where(x => x.Partner != null && x.Partner.Name.Contains(partnerNameFilter));
        }

        if (!string.IsNullOrWhiteSpace(PartnerNumber))
        {
            var partnerNumberFilter = PartnerNumber.Trim();
            query = query.Where(x => x.Partner != null && x.Partner.Code.Contains(partnerNumberFilter));
        }

        if (!string.IsNullOrWhiteSpace(DocumentNumber))
        {
            var documentNumberFilter = DocumentNumber.Trim();
            query = query.Where(x => x.Number.Contains(documentNumberFilter));
        }

        if (Enum.TryParse<CommercialDocumentStatus>(Status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(x => x.Status == parsedStatus);
            Status = parsedStatus.ToString();
        }
        else if (!string.IsNullOrWhiteSpace(Status))
        {
            Status = null;
        }

        if (DateFrom.HasValue)
        {
            query = query.Where(x => x.DocumentDate >= DateFrom.Value);
        }

        if (DateTo.HasValue)
        {
            query = query.Where(x => x.DocumentDate <= DateTo.Value);
        }

        Documents = await query
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Select(x => new SalesDocumentListItem(
                x.Id,
                x.Number,
                x.DocumentDate,
                x.Partner != null ? x.Partner.Code : null,
                x.Partner != null ? x.Partner.Name : "-",
                x.Status,
                x.TotalIncludingTax,
                x.CurrencyCode,
                x.SourceDocument != null ? x.SourceDocument.Number : null))
            .ToListAsync(HttpContext.RequestAborted);
    }

    public string Title => SalesDocumentCatalog.Label(CurrentType);
}

public sealed record SalesDocumentListItem(
    Guid Id,
    string Number,
    DateOnly DocumentDate,
    string? PartnerCode,
    string PartnerName,
    CommercialDocumentStatus Status,
    decimal TotalIncludingTax,
    string CurrencyCode,
    string? SourceNumber);
