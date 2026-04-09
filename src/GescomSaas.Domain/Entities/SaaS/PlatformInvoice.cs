using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.SaaS;

public class PlatformInvoice : TenantEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15));
    public DateOnly PeriodStart { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly PeriodEnd { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public PlatformInvoiceStatus Status { get; set; } = PlatformInvoiceStatus.Draft;
    public string CurrencyCode { get; set; } = "CAD";
    public decimal TotalExcludingTax { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalIncludingTax { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? Notes { get; set; }

    public Guid? TenantSubscriptionId { get; set; }
    public TenantSubscription? TenantSubscription { get; set; }
    public Tenant? Tenant { get; set; }

    public ICollection<PlatformInvoiceLine> Lines { get; set; } = new List<PlatformInvoiceLine>();
}
