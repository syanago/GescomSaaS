using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Infrastructure.Services;

public class TenantQuotaEnforcementService(ApplicationDbContext dbContext) : ITenantQuotaEnforcementService
{
    public async Task<IReadOnlyList<QuotaUsageItem>> GetQuotaUsageAsync(Guid tenantId, DateOnly? documentDate = null, CancellationToken cancellationToken = default)
    {
        var quota = await GetQuotaContextAsync(tenantId, cancellationToken);
        var referenceDate = documentDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(referenceDate.Year, referenceDate.Month, 1);
        var monthEnd = new DateOnly(referenceDate.Year, referenceDate.Month, DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month));

        var userCount = await dbContext.Users
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId, cancellationToken);

        var customerCount = await dbContext.BusinessPartners
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId &&
                     x.IsActive &&
                     (x.PartnerType == BusinessPartnerType.Customer ||
                      x.PartnerType == BusinessPartnerType.Both ||
                      x.PartnerType == BusinessPartnerType.Prospect),
                cancellationToken);

        var supplierCount = await dbContext.BusinessPartners
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId &&
                     x.IsActive &&
                     (x.PartnerType == BusinessPartnerType.Supplier ||
                      x.PartnerType == BusinessPartnerType.Both),
                cancellationToken);

        var productCount = await dbContext.Products
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);

        var warehouseCount = await dbContext.Warehouses
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId, cancellationToken);

        var monthlyCommercialDocuments = await dbContext.CommercialDocuments
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId &&
                     x.DocumentDate >= monthStart &&
                     x.DocumentDate <= monthEnd &&
                     x.Status != CommercialDocumentStatus.Cancelled,
                cancellationToken);

        var monthlyStockDocuments = await dbContext.StockDocuments
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId &&
                     x.DocumentDate >= monthStart &&
                     x.DocumentDate <= monthEnd &&
                     x.Status != StockDocumentStatus.Cancelled,
                cancellationToken);

        var monthlyDocumentCount = monthlyCommercialDocuments + monthlyStockDocuments;

        return
        [
            new QuotaUsageItem("Utilisateurs", userCount, quota.MaxUsers, userCount > quota.MaxUsers),
            new QuotaUsageItem("Clients", customerCount, quota.MaxCustomers, customerCount > quota.MaxCustomers),
            new QuotaUsageItem("Fournisseurs", supplierCount, quota.MaxSuppliers, supplierCount > quota.MaxSuppliers),
            new QuotaUsageItem("Articles", productCount, quota.MaxProducts, productCount > quota.MaxProducts),
            new QuotaUsageItem("Depots", warehouseCount, quota.MaxWarehouses, warehouseCount > quota.MaxWarehouses),
            new QuotaUsageItem("Documents du mois", monthlyDocumentCount, quota.MaxMonthlyDocuments, monthlyDocumentCount > quota.MaxMonthlyDocuments)
        ];
    }

    public async Task EnsureCanManageUsersAsync(Guid tenantId, int additionalUsers = 1, CancellationToken cancellationToken = default)
    {
        if (additionalUsers <= 0)
        {
            return;
        }

        var quota = await GetQuotaContextAsync(tenantId, cancellationToken);
        var currentUsers = await dbContext.Users
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId, cancellationToken);

        EnsureQuota(
            "Utilisateurs",
            quota.PlanLabel,
            currentUsers,
            quota.MaxUsers,
            currentUsers + additionalUsers);
    }

    public Task EnsureCanCreatePartnerAsync(Guid tenantId, BusinessPartnerType partnerType, bool isActive, CancellationToken cancellationToken = default) =>
        EnsurePartnerQuotaAsync(tenantId, null, partnerType, isActive, cancellationToken);

    public async Task EnsureCanUpdatePartnerAsync(Guid tenantId, Guid partnerId, BusinessPartnerType partnerType, bool isActive, CancellationToken cancellationToken = default)
    {
        var existingPartner = await dbContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == partnerId && x.TenantId == tenantId, cancellationToken);

        if (existingPartner is null)
        {
            throw new InvalidOperationException("Tiers introuvable.");
        }

        var quota = await GetQuotaContextAsync(tenantId, cancellationToken);

        var currentCustomerCount = await dbContext.BusinessPartners
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId &&
                     x.IsActive &&
                     (x.PartnerType == BusinessPartnerType.Customer ||
                      x.PartnerType == BusinessPartnerType.Both ||
                      x.PartnerType == BusinessPartnerType.Prospect),
                cancellationToken);

        var currentSupplierCount = await dbContext.BusinessPartners
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId &&
                     x.IsActive &&
                     (x.PartnerType == BusinessPartnerType.Supplier ||
                      x.PartnerType == BusinessPartnerType.Both),
                cancellationToken);

        var customerCountExcludingCurrent = currentCustomerCount - (ConsumesCustomerQuota(existingPartner.PartnerType) && existingPartner.IsActive ? 1 : 0);
        var supplierCountExcludingCurrent = currentSupplierCount - (ConsumesSupplierQuota(existingPartner.PartnerType) && existingPartner.IsActive ? 1 : 0);
        var projectedCustomers = customerCountExcludingCurrent + (ConsumesCustomerQuota(partnerType) && isActive ? 1 : 0);
        var projectedSuppliers = supplierCountExcludingCurrent + (ConsumesSupplierQuota(partnerType) && isActive ? 1 : 0);

        EnsureQuotaChange("Clients", quota.PlanLabel, currentCustomerCount, quota.MaxCustomers, projectedCustomers);
        EnsureQuotaChange("Fournisseurs", quota.PlanLabel, currentSupplierCount, quota.MaxSuppliers, projectedSuppliers);
    }

    public async Task EnsureCanCreateProductAsync(Guid tenantId, bool isActive, CancellationToken cancellationToken = default)
    {
        if (!isActive)
        {
            return;
        }

        var quota = await GetQuotaContextAsync(tenantId, cancellationToken);
        var currentProducts = await dbContext.Products
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);

        EnsureQuota(
            "Articles",
            quota.PlanLabel,
            currentProducts,
            quota.MaxProducts,
            currentProducts + 1);
    }

    public async Task EnsureCanUpdateProductAsync(Guid tenantId, Guid productId, bool isActive, CancellationToken cancellationToken = default)
    {
        var existingProduct = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == productId && x.TenantId == tenantId, cancellationToken);

        if (existingProduct is null)
        {
            throw new InvalidOperationException("Article introuvable.");
        }

        var quota = await GetQuotaContextAsync(tenantId, cancellationToken);
        var currentActiveProducts = await dbContext.Products
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);

        var activeProductsExcludingCurrent = currentActiveProducts - (existingProduct.IsActive ? 1 : 0);

        EnsureQuotaChange(
            "Articles",
            quota.PlanLabel,
            currentActiveProducts,
            quota.MaxProducts,
            activeProductsExcludingCurrent + (isActive ? 1 : 0));
    }

    public async Task EnsureCanCreateWarehouseAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var quota = await GetQuotaContextAsync(tenantId, cancellationToken);
        var currentWarehouses = await dbContext.Warehouses
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId, cancellationToken);

        EnsureQuota(
            "Depots",
            quota.PlanLabel,
            currentWarehouses,
            quota.MaxWarehouses,
            currentWarehouses + 1);
    }

    public async Task EnsureCanCreateDocumentAsync(Guid tenantId, DateOnly documentDate, CancellationToken cancellationToken = default)
    {
        var quota = await GetQuotaContextAsync(tenantId, cancellationToken);
        var monthStart = new DateOnly(documentDate.Year, documentDate.Month, 1);
        var monthEnd = new DateOnly(documentDate.Year, documentDate.Month, DateTime.DaysInMonth(documentDate.Year, documentDate.Month));

        var monthlyCommercialDocuments = await dbContext.CommercialDocuments
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId &&
                     x.DocumentDate >= monthStart &&
                     x.DocumentDate <= monthEnd &&
                     x.Status != CommercialDocumentStatus.Cancelled,
                cancellationToken);

        var monthlyStockDocuments = await dbContext.StockDocuments
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId &&
                     x.DocumentDate >= monthStart &&
                     x.DocumentDate <= monthEnd &&
                     x.Status != StockDocumentStatus.Cancelled,
                cancellationToken);

        var monthlyDocuments = monthlyCommercialDocuments + monthlyStockDocuments;

        EnsureQuota(
            "Documents du mois",
            quota.PlanLabel,
            monthlyDocuments,
            quota.MaxMonthlyDocuments,
            monthlyDocuments + 1);
    }

    private async Task EnsurePartnerQuotaAsync(Guid tenantId, Guid? partnerId, BusinessPartnerType partnerType, bool isActive, CancellationToken cancellationToken)
    {
        var quota = await GetQuotaContextAsync(tenantId, cancellationToken);

        var customerCount = await dbContext.BusinessPartners
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId &&
                     x.Id != partnerId &&
                     x.IsActive &&
                     (x.PartnerType == BusinessPartnerType.Customer ||
                      x.PartnerType == BusinessPartnerType.Both ||
                      x.PartnerType == BusinessPartnerType.Prospect),
                cancellationToken);

        var supplierCount = await dbContext.BusinessPartners
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId &&
                     x.Id != partnerId &&
                     x.IsActive &&
                     (x.PartnerType == BusinessPartnerType.Supplier ||
                      x.PartnerType == BusinessPartnerType.Both),
                cancellationToken);

        var projectedCustomers = customerCount + (ConsumesCustomerQuota(partnerType) && isActive ? 1 : 0);
        var projectedSuppliers = supplierCount + (ConsumesSupplierQuota(partnerType) && isActive ? 1 : 0);

        EnsureQuotaChange(
            "Clients",
            quota.PlanLabel,
            customerCount,
            quota.MaxCustomers,
            projectedCustomers);

        EnsureQuotaChange(
            "Fournisseurs",
            quota.PlanLabel,
            supplierCount,
            quota.MaxSuppliers,
            projectedSuppliers);
    }

    private async Task<QuotaContext> GetQuotaContextAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            throw new InvalidOperationException("Tenant introuvable.");
        }

        var subscription = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .Include(x => x.SubscriptionPlan)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.StartsOn)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription?.SubscriptionPlan is null)
        {
            return new QuotaContext("Sans abonnement", 0, 0, 0, 0, 0, 0);
        }

        return new QuotaContext(
            subscription.SubscriptionPlan.Label,
            subscription.MaxUsersOverride ?? subscription.SubscriptionPlan.MaxUsers,
            subscription.MaxCustomersOverride ?? subscription.SubscriptionPlan.MaxCustomers,
            subscription.MaxSuppliersOverride ?? subscription.SubscriptionPlan.MaxSuppliers,
            subscription.MaxProductsOverride ?? subscription.SubscriptionPlan.MaxProducts,
            subscription.MaxWarehousesOverride ?? subscription.SubscriptionPlan.MaxWarehouses,
            subscription.MaxMonthlyDocumentsOverride ?? subscription.SubscriptionPlan.MaxMonthlyDocuments);
    }

    private static bool ConsumesCustomerQuota(BusinessPartnerType partnerType) =>
        partnerType is BusinessPartnerType.Customer or BusinessPartnerType.Both or BusinessPartnerType.Prospect;

    private static bool ConsumesSupplierQuota(BusinessPartnerType partnerType) =>
        partnerType is BusinessPartnerType.Supplier or BusinessPartnerType.Both;

    private static void EnsureQuota(string quotaLabel, string planLabel, int currentUsage, int limit, int projectedUsage)
    {
        if (projectedUsage <= limit)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Quota {quotaLabel.ToLowerInvariant()} atteint pour le plan {planLabel} ({currentUsage}/{limit}). " +
            $"{BuildUpgradeGuidance()}");
    }

    private static void EnsureQuotaChange(string quotaLabel, string planLabel, int currentUsage, int limit, int projectedUsage)
    {
        if (projectedUsage <= limit || projectedUsage <= currentUsage)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Quota {quotaLabel.ToLowerInvariant()} atteint pour le plan {planLabel} ({currentUsage}/{limit}). " +
            $"{BuildUpgradeGuidance()}");
    }

    private static string BuildUpgradeGuidance() =>
        "Passez ce tenant sur un plan superieur dans SaaS Admin > Tenants, ou demandez a l'administrateur plateforme de mettre a niveau l'abonnement.";

    private sealed record QuotaContext(
        string PlanLabel,
        int MaxUsers,
        int MaxCustomers,
        int MaxSuppliers,
        int MaxProducts,
        int MaxWarehouses,
        int MaxMonthlyDocuments);
}
