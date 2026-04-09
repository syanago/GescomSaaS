using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.SaaS;

public class TenantQuotaNotification : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string QuotaLabel { get; set; } = string.Empty;
    public PlatformNotificationSeverity Severity { get; set; } = PlatformNotificationSeverity.Warning;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Used { get; set; }
    public int Limit { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedOnUtc { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedOnUtc { get; set; }
    public DateTime LastTriggeredOnUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastEmailSentOnUtc { get; set; }
}
