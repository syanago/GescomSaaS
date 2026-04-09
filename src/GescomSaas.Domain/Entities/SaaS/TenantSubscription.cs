using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.SaaS;

public class TenantSubscription : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid SubscriptionPlanId { get; set; }
    public SubscriptionPlan? SubscriptionPlan { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;
    public DateOnly StartsOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? EndsOn { get; set; }
    public DateOnly? NextBillingDate { get; set; }
    public bool AutoRenew { get; set; } = true;
    public decimal? MonthlyPriceOverride { get; set; }
    public int? MaxUsersOverride { get; set; }
    public int? MaxCustomersOverride { get; set; }
    public int? MaxSuppliersOverride { get; set; }
    public int? MaxProductsOverride { get; set; }
    public int? MaxWarehousesOverride { get; set; }
    public int? MaxMonthlyDocumentsOverride { get; set; }
}
