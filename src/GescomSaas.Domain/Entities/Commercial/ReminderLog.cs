using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.Commercial;

public class ReminderLog : TenantEntity
{
    public Guid CommercialDocumentId { get; set; }
    public CommercialDocument? CommercialDocument { get; set; }

    public ReminderLevel ReminderLevel { get; set; } = ReminderLevel.Friendly;
    public DateTime SentOnUtc { get; set; } = DateTime.UtcNow;
    public string Channel { get; set; } = "Manual";
    public bool IsAutomatic { get; set; }
    public bool IsGrouped { get; set; }
    public DateOnly? NextActionDate { get; set; }
    public string? Notes { get; set; }
}
