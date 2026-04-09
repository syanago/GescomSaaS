using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.SaaSAdmin.Tenants;

public class TenantInputModel
{
    [Required]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string PrimaryContactEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(2)]
    public string CountryCode { get; set; } = "CA";

    [Required]
    [StringLength(3)]
    public string CurrencyCode { get; set; } = "CAD";

    public bool IsActive { get; set; } = true;

    [Required]
    public Guid? SubscriptionPlanId { get; set; }

    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Trial;
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

    public static TenantInputModel FromEntity(Tenant tenant, TenantSubscription? subscription) =>
        new()
        {
            CompanyName = tenant.CompanyName,
            Slug = tenant.Slug,
            PrimaryContactEmail = tenant.PrimaryContactEmail,
            CountryCode = tenant.CountryCode,
            CurrencyCode = tenant.CurrencyCode,
            IsActive = tenant.IsActive,
            SubscriptionPlanId = subscription?.SubscriptionPlanId,
            SubscriptionStatus = subscription?.Status ?? SubscriptionStatus.Trial,
            StartsOn = subscription?.StartsOn ?? DateOnly.FromDateTime(DateTime.UtcNow),
            EndsOn = subscription?.EndsOn,
            NextBillingDate = subscription?.NextBillingDate,
            AutoRenew = subscription?.AutoRenew ?? true,
            MonthlyPriceOverride = subscription?.MonthlyPriceOverride,
            MaxUsersOverride = subscription?.MaxUsersOverride,
            MaxCustomersOverride = subscription?.MaxCustomersOverride,
            MaxSuppliersOverride = subscription?.MaxSuppliersOverride,
            MaxProductsOverride = subscription?.MaxProductsOverride,
            MaxWarehousesOverride = subscription?.MaxWarehousesOverride,
            MaxMonthlyDocumentsOverride = subscription?.MaxMonthlyDocumentsOverride
        };

    public void ApplyTo(Tenant tenant, TenantSubscription subscription)
    {
        tenant.CompanyName = CompanyName.Trim();
        tenant.Slug = Slug.Trim().ToLowerInvariant();
        tenant.PrimaryContactEmail = PrimaryContactEmail.Trim();
        tenant.CountryCode = CountryCode.Trim().ToUpperInvariant();
        tenant.CurrencyCode = CurrencyCode.Trim().ToUpperInvariant();
        tenant.IsActive = IsActive;

        subscription.SubscriptionPlanId = SubscriptionPlanId ?? Guid.Empty;
        subscription.Status = SubscriptionStatus;
        subscription.StartsOn = StartsOn;
        subscription.EndsOn = EndsOn;
        subscription.NextBillingDate = NextBillingDate;
        subscription.AutoRenew = AutoRenew;
        subscription.MonthlyPriceOverride = MonthlyPriceOverride > 0m ? MonthlyPriceOverride : null;
        subscription.MaxUsersOverride = NormalizeNullable(MaxUsersOverride);
        subscription.MaxCustomersOverride = NormalizeNullable(MaxCustomersOverride);
        subscription.MaxSuppliersOverride = NormalizeNullable(MaxSuppliersOverride);
        subscription.MaxProductsOverride = NormalizeNullable(MaxProductsOverride);
        subscription.MaxWarehousesOverride = NormalizeNullable(MaxWarehousesOverride);
        subscription.MaxMonthlyDocumentsOverride = NormalizeNullable(MaxMonthlyDocumentsOverride);
    }

    private static int? NormalizeNullable(int? value) => value.HasValue && value.Value > 0 ? value.Value : null;
}
