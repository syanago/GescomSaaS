using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record TenantAdminSummary(
    Guid TenantId,
    string CompanyName,
    string Slug,
    string PrimaryContactEmail,
    bool IsActive,
    string PlanName,
    SubscriptionStatus SubscriptionStatus,
    DateOnly StartsOn,
    DateOnly? EndsOn,
    DateOnly? NextBillingDate,
    decimal RecurringCharge,
    int UserCount,
    int CustomerCount,
    int SupplierCount,
    int ProductCount,
    int WarehouseCount,
    int MonthlyDocumentCount,
    int QuotaAlertCount,
    IReadOnlyList<QuotaUsageItem> Quotas);
