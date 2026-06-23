using GescomSaas.Application.Catalog;
using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace GescomSaas.Infrastructure.Services;

public class CommercialDashboardService(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : ICommercialDashboardService
{
    private static readonly CultureInfo FrenchCanada = CultureInfo.GetCultureInfo("fr-CA");

    public async Task<DashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(cancellationToken);
        if (tenant is null)
        {
            return DashboardSnapshot.Empty(CommercialFeatureCatalog.Modules);
        }

        var subscription = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .Include(x => x.SubscriptionPlan)
            .Where(x => x.TenantId == tenant.Id)
            .OrderByDescending(x => x.StartsOn)
            .FirstOrDefaultAsync(cancellationToken);

        var reportingDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(reportingDate.Year, reportingDate.Month, 1);
        var customerCount = await dbContext.BusinessPartners
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenant.Id &&
                     x.IsActive &&
                     (x.PartnerType == BusinessPartnerType.Customer || x.PartnerType == BusinessPartnerType.Both || x.PartnerType == BusinessPartnerType.Prospect),
                cancellationToken);

        var supplierCount = await dbContext.BusinessPartners
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenant.Id &&
                     x.IsActive &&
                     (x.PartnerType == BusinessPartnerType.Supplier || x.PartnerType == BusinessPartnerType.Both),
                cancellationToken);

        var recentDocuments = await dbContext.CommercialDocuments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Where(x => x.TenantId == tenant.Id)
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Take(8)
            .Select(
                x => new RecentDocumentItem(
                    x.Number,
                    x.DocumentType,
                    x.Partner != null ? x.Partner.Name : "Tiers non renseigne",
                    x.DocumentDate,
                    x.Status,
                    x.TotalIncludingTax,
                    x.CurrencyCode))
            .ToListAsync(cancellationToken);

        var salesFlows = await dbContext.CommercialDocuments
            .AsNoTracking()
            .Where(
                x => x.TenantId == tenant.Id &&
                     (x.DocumentType == CommercialDocumentType.SalesQuote ||
                      x.DocumentType == CommercialDocumentType.SalesOrder ||
                      x.DocumentType == CommercialDocumentType.SalesInvoice ||
                      x.DocumentType == CommercialDocumentType.SalesCreditNote) &&
                     x.Status != CommercialDocumentStatus.Cancelled)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                OpenQuotes = g.Count(x => x.DocumentType == CommercialDocumentType.SalesQuote && x.Status != CommercialDocumentStatus.Completed),
                OpenOrders = g.Count(x => x.DocumentType == CommercialDocumentType.SalesOrder && x.Status != CommercialDocumentStatus.Completed),
                InvoiceCountThisMonth = g.Count(x => x.DocumentType == CommercialDocumentType.SalesInvoice && x.DocumentDate >= monthStart),
                CreditNoteCountThisMonth = g.Count(x => x.DocumentType == CommercialDocumentType.SalesCreditNote && x.DocumentDate >= monthStart),
                RevenueThisMonth =
                    g.Where(x => x.DocumentDate >= monthStart)
                        .Sum(x => x.DocumentType == CommercialDocumentType.SalesInvoice
                            ? x.TotalIncludingTax
                            : x.DocumentType == CommercialDocumentType.SalesCreditNote
                                ? -x.TotalIncludingTax
                                : 0m)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var purchaseFlows = await dbContext.CommercialDocuments
            .AsNoTracking()
            .Where(x => x.TenantId == tenant.Id)
            .Where(
                x => (x.DocumentType == CommercialDocumentType.PurchaseRequest ||
                      x.DocumentType == CommercialDocumentType.PurchaseOrder ||
                      x.DocumentType == CommercialDocumentType.PurchaseInvoice ||
                      x.DocumentType == CommercialDocumentType.SupplierCreditNote) &&
                     x.Status != CommercialDocumentStatus.Cancelled)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                OpenRequests = g.Count(x => x.DocumentType == CommercialDocumentType.PurchaseRequest && x.Status != CommercialDocumentStatus.Completed),
                OpenOrders = g.Count(x => x.DocumentType == CommercialDocumentType.PurchaseOrder && x.Status != CommercialDocumentStatus.Completed),
                InvoiceCountThisMonth = g.Count(x => x.DocumentType == CommercialDocumentType.PurchaseInvoice && x.DocumentDate >= monthStart),
                CreditNoteCountThisMonth = g.Count(x => x.DocumentType == CommercialDocumentType.SupplierCreditNote && x.DocumentDate >= monthStart),
                SpendThisMonth =
                    g.Where(x => x.DocumentDate >= monthStart)
                        .Sum(x => x.DocumentType == CommercialDocumentType.PurchaseInvoice
                            ? x.TotalIncludingTax
                            : x.DocumentType == CommercialDocumentType.SupplierCreditNote
                                ? -x.TotalIncludingTax
                                : 0m)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var receivables = await GetOpenBalancesAsync(tenant.Id, CommercialDocumentType.SalesInvoice, reportingDate, cancellationToken);
        var payables = await GetOpenBalancesAsync(tenant.Id, CommercialDocumentType.PurchaseInvoice, reportingDate, cancellationToken);

        var sales = new SalesDashboardSnapshot(
            salesFlows?.OpenQuotes ?? 0,
            salesFlows?.OpenOrders ?? 0,
            salesFlows?.InvoiceCountThisMonth ?? 0,
            salesFlows?.CreditNoteCountThisMonth ?? 0,
            decimal.Round(salesFlows?.RevenueThisMonth ?? 0m, 2),
            receivables.Sum(x => x.BalanceAmount),
            receivables.Where(x => x.OverdueDays > 0).Sum(x => x.BalanceAmount),
            receivables.Count(x => x.OverdueDays > 0));

        var purchases = new PurchaseDashboardSnapshot(
            purchaseFlows?.OpenRequests ?? 0,
            purchaseFlows?.OpenOrders ?? 0,
            purchaseFlows?.InvoiceCountThisMonth ?? 0,
            purchaseFlows?.CreditNoteCountThisMonth ?? 0,
            decimal.Round(purchaseFlows?.SpendThisMonth ?? 0m, 2),
            payables.Sum(x => x.BalanceAmount),
            payables.Where(x => x.OverdueDays > 0).Sum(x => x.BalanceAmount),
            payables.Count(x => x.OverdueDays > 0));

        var finance = await GetFinanceSnapshotAsync(tenant.Id, receivables, payables, cancellationToken);
        var stock = await GetStockSnapshotAsync(tenant.Id, cancellationToken);
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenant.Id, reportingDate, cancellationToken);

        var metrics = new[]
        {
            new DashboardMetric("Clients actifs", FormatCount(tenant, customerCount), "Base clients du tenant"),
            new DashboardMetric("CA net du mois", FormatMoney(tenant, sales.RevenueThisMonth), $"{FormatCount(tenant, sales.InvoiceCountThisMonth)} facture(s) et {FormatCount(tenant, sales.CreditNoteCountThisMonth)} avoir(s)"),
            new DashboardMetric("Achats du mois", FormatMoney(tenant, purchases.SpendThisMonth), $"{FormatCount(tenant, supplierCount)} fournisseur(s) actifs"),
            new DashboardMetric("Encours clients", FormatMoney(tenant, finance.ReceivableBalance), $"{FormatCount(tenant, finance.OverdueReceivableCount)} facture(s) en retard"),
            new DashboardMetric("Encours fournisseurs", FormatMoney(tenant, finance.PayableBalance), $"{FormatCount(tenant, finance.OverduePayableCount)} facture(s) a regler en retard"),
            new DashboardMetric("Stock valorise", FormatMoney(tenant, stock.TotalStockValue), $"{FormatCount(tenant, stock.TrackedProductCount)} article(s) stockes"),
            new DashboardMetric("Articles a surveiller", FormatCount(tenant, stock.WatchItemCount), "Seuil de vigilance fixe a 5 unites")
        };

        return new DashboardSnapshot(
            tenant.CompanyName,
            subscription?.SubscriptionPlan?.Label ?? "Plan non defini",
            subscription?.Status ?? SubscriptionStatus.Trial,
            reportingDate,
            monthStart.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", FrenchCanada),
            quotas,
            metrics,
            sales,
            purchases,
            finance,
            stock,
            recentDocuments,
            CommercialFeatureCatalog.Modules);
    }

    private async Task<FinanceDashboardSnapshot> GetFinanceSnapshotAsync(
        Guid tenantId,
        IReadOnlyList<OpenBalanceItem> receivables,
        IReadOnlyList<OpenBalanceItem> payables,
        CancellationToken cancellationToken)
    {
        var recentPayments = await dbContext.Payments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.PaymentDate)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Take(6)
            .Select(x => new RecentPaymentDashboardItem(
                x.PaymentDate,
                x.ReferenceNumber,
                x.Partner != null ? x.Partner.Name : "Tiers non renseigne",
                x.Direction,
                x.Method,
                x.Amount,
                x.CurrencyCode))
            .ToListAsync(cancellationToken);

        var recentReminders = await dbContext.ReminderLogs
            .AsNoTracking()
            .Include(x => x.CommercialDocument)
            .ThenInclude(x => x!.Partner)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.SentOnUtc)
            .Take(5)
            .Select(x => new RecentReminderDashboardItem(
                x.SentOnUtc,
                x.CommercialDocument != null ? x.CommercialDocument.Number : "-",
                x.CommercialDocument != null && x.CommercialDocument.Partner != null
                    ? x.CommercialDocument.Partner.Name
                    : "Tiers non renseigne",
                x.ReminderLevel,
                x.Channel,
                x.Notes))
            .ToListAsync(cancellationToken);

        return new FinanceDashboardSnapshot(
            receivables.Count,
            receivables.Sum(x => x.BalanceAmount),
            receivables.Count(x => x.OverdueDays > 0),
            receivables.Where(x => x.OverdueDays > 0).Sum(x => x.BalanceAmount),
            payables.Count,
            payables.Sum(x => x.BalanceAmount),
            payables.Count(x => x.OverdueDays > 0),
            payables.Where(x => x.OverdueDays > 0).Sum(x => x.BalanceAmount),
            recentPayments,
            recentReminders);
    }

    private async Task<StockDashboardSnapshot> GetStockSnapshotAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var trackedProducts = await dbContext.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TrackStock && x.IsActive)
            .Select(x => new
            {
                x.Sku,
                x.Label,
                x.UnitOfMeasure,
                OnHandQuantity = x.StockMovements.Sum(m => (decimal?)m.Quantity) ?? 0m,
                StockValue = x.StockMovements.Sum(m => (decimal?)(m.Quantity * m.UnitCost)) ?? 0m
            })
            .OrderBy(x => x.Sku)
            .ToListAsync(cancellationToken);

        var watchItems = trackedProducts
            .Where(x => x.OnHandQuantity <= 5m)
            .OrderBy(x => x.OnHandQuantity)
            .ThenBy(x => x.Sku)
            .Take(6)
            .Select(x => new StockWatchItem(x.Sku, x.Label, x.UnitOfMeasure, x.OnHandQuantity, decimal.Round(x.StockValue, 2)))
            .ToList();

        var topWarehouses = await dbContext.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new
            {
                x.Code,
                x.Label,
                TotalQuantity = dbContext.StockMovements.Where(m => m.TenantId == tenantId && m.WarehouseId == x.Id).Sum(m => (decimal?)m.Quantity) ?? 0m,
                TotalValue = dbContext.StockMovements.Where(m => m.TenantId == tenantId && m.WarehouseId == x.Id).Sum(m => (decimal?)(m.Quantity * m.UnitCost)) ?? 0m
            })
            .ToListAsync(cancellationToken);

        var topWarehouseItems = topWarehouses
            .OrderByDescending(x => x.TotalValue)
            .ThenBy(x => x.Code)
            .Take(4)
            .Select(x => new WarehouseValueItem(
                x.Code,
                x.Label,
                x.TotalQuantity,
                x.TotalValue))
            .ToList();

        return new StockDashboardSnapshot(
            trackedProducts.Count,
            trackedProducts.Sum(x => x.OnHandQuantity),
            trackedProducts.Sum(x => x.StockValue),
            trackedProducts.Count(x => x.OnHandQuantity <= 5m),
            watchItems,
            topWarehouseItems);
    }

    private async Task<IReadOnlyList<OpenBalanceItem>> GetOpenBalancesAsync(
        Guid tenantId,
        CommercialDocumentType documentType,
        DateOnly reportingDate,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.CommercialDocuments
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.DocumentType == documentType && x.Status != CommercialDocumentStatus.Cancelled)
            .Select(x => new
            {
                x.DueDate,
                x.TotalIncludingTax,
                PaidAmount = x.PaymentAllocations.Sum(a => (decimal?)a.AllocatedAmount) ?? 0m
            })
            .ToListAsync(cancellationToken);

        return items
            .Select(x =>
            {
                var balance = x.TotalIncludingTax - x.PaidAmount;
                var overdueDays = x.DueDate.HasValue && balance > 0m && x.DueDate.Value < reportingDate
                    ? reportingDate.DayNumber - x.DueDate.Value.DayNumber
                    : 0;

                return new OpenBalanceItem(balance, overdueDays);
            })
            .Where(x => x.BalanceAmount > 0m)
            .ToList();
    }

    private async Task<Tenant?> ResolveTenantAsync(CancellationToken cancellationToken)
    {
        var currentTenantId = currentTenantAccessor.GetTenantId();
        if (currentTenantId.HasValue)
        {
            return await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == currentTenantId.Value, cancellationToken);
        }

        return await dbContext.Tenants
            .AsNoTracking()
            .OrderBy(x => x.CompanyName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string FormatMoney(Tenant tenant, decimal amount)
    {
        var number = FormatValue(Math.Abs(amount), tenant.MoneyDecimalPlaces, tenant.MoneyDecimalSeparator, tenant.MoneyGroupSeparator);
        var sign = amount < 0 ? "-" : string.Empty;
        var symbol = string.IsNullOrWhiteSpace(tenant.CurrencySymbol) ? tenant.CurrencyCode : tenant.CurrencySymbol.Trim();

        return tenant.CurrencySymbolPosition == CurrencySymbolPosition.BeforeAmount
            ? $"{sign}{symbol}{number}"
            : $"{sign}{number} {symbol}";
    }

    private static string FormatCount(Tenant tenant, int value) =>
        FormatValue(value, 0, tenant.QuantityDecimalSeparator, tenant.QuantityGroupSeparator);

    private static string FormatValue(decimal value, int decimals, string decimalSeparator, string groupSeparator)
    {
        var format = (System.Globalization.NumberFormatInfo)System.Globalization.CultureInfo.InvariantCulture.NumberFormat.Clone();
        format.NumberDecimalDigits = Math.Clamp(decimals, 0, 6);
        format.NumberDecimalSeparator = string.IsNullOrEmpty(decimalSeparator) ? "," : decimalSeparator;
        format.NumberGroupSeparator = groupSeparator ?? " ";
        format.NumberGroupSizes = [3];
        return value.ToString("N", format);
    }

    private sealed record OpenBalanceItem(decimal BalanceAmount, int OverdueDays);
}
