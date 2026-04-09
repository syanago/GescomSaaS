using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;

namespace GescomSaas.Infrastructure.Services;

public class PlatformAdministrationService(
    ApplicationDbContext dbContext,
    PlatformNotificationEmailService notificationEmailService,
    ILogger<PlatformAdministrationService> logger) : IPlatformAdministrationService
{
    private const decimal QuotaWarningThreshold = 0.8m;

    public async Task<PlatformAdminDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var summaries = await GetTenantSummariesAsync(cancellationToken);
        await SynchronizeQuotaNotificationsAsync(summaries, cancellationToken);
        var notifications = await GetOpenQuotaNotificationsAsync(summaries, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var recentInvoices = await dbContext.PlatformInvoices
            .AsNoTracking()
            .Include(x => x.Tenant)
            .OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Take(6)
            .Select(x => new PlatformInvoiceSummaryItem(
                x.Id,
                x.InvoiceNumber,
                x.Tenant != null ? x.Tenant.CompanyName : "-",
                x.IssueDate,
                x.DueDate,
                x.Status == PlatformInvoiceStatus.Issued && x.DueDate < today ? PlatformInvoiceStatus.Overdue : x.Status,
                x.TotalIncludingTax,
                x.CurrencyCode))
            .ToListAsync(cancellationToken);

        return new PlatformAdminDashboardSnapshot(
            summaries.Count,
            summaries.Count(x => x.IsActive),
            summaries.Count(x => x.SubscriptionStatus == SubscriptionStatus.Trial),
            summaries.Where(x => x.IsActive && x.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trial).Sum(x => x.RecurringCharge),
            recentInvoices.Count(x => x.Status == PlatformInvoiceStatus.Overdue),
            summaries.Count(x => x.QuotaAlertCount > 0),
            notifications.Count(x => x.Severity == PlatformNotificationSeverity.Warning),
            notifications.Count(x => x.Severity == PlatformNotificationSeverity.Critical),
            notifications.Take(8).ToList(),
            summaries.Where(x => x.QuotaAlertCount > 0).OrderByDescending(x => x.QuotaAlertCount).ThenBy(x => x.CompanyName).Take(6).ToList(),
            recentInvoices);
    }

    public async Task<IReadOnlyList<TenantAdminSummary>> GetTenantSummariesAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

        var tenants = await dbContext.Tenants
            .AsNoTracking()
            .OrderBy(x => x.CompanyName)
            .Select(x => new
            {
                x.Id,
                x.CompanyName,
                x.Slug,
                x.PrimaryContactEmail,
                x.IsActive,
                LatestSubscription = x.Subscriptions
                    .OrderByDescending(s => s.StartsOn)
                    .Select(s => new
                    {
                        s.Id,
                        s.Status,
                        s.StartsOn,
                        s.EndsOn,
                        s.NextBillingDate,
                        s.MonthlyPriceOverride,
                        s.MaxUsersOverride,
                        s.MaxCustomersOverride,
                        s.MaxSuppliersOverride,
                        s.MaxProductsOverride,
                        s.MaxWarehousesOverride,
                        s.MaxMonthlyDocumentsOverride,
                        PlanLabel = s.SubscriptionPlan != null ? s.SubscriptionPlan.Label : "Plan non defini",
                        PlanMonthlyPrice = s.SubscriptionPlan != null ? s.SubscriptionPlan.MonthlyPrice : 0m,
                        PlanMaxUsers = s.SubscriptionPlan != null ? s.SubscriptionPlan.MaxUsers : 0,
                        PlanMaxCustomers = s.SubscriptionPlan != null ? s.SubscriptionPlan.MaxCustomers : 0,
                        PlanMaxSuppliers = s.SubscriptionPlan != null ? s.SubscriptionPlan.MaxSuppliers : 0,
                        PlanMaxProducts = s.SubscriptionPlan != null ? s.SubscriptionPlan.MaxProducts : 0,
                        PlanMaxWarehouses = s.SubscriptionPlan != null ? s.SubscriptionPlan.MaxWarehouses : 0,
                        PlanMaxMonthlyDocuments = s.SubscriptionPlan != null ? s.SubscriptionPlan.MaxMonthlyDocuments : 0
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var userCounts = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.TenantId.HasValue)
            .GroupBy(x => x.TenantId!.Value)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var customerCounts = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(x => x.IsActive && (x.PartnerType == BusinessPartnerType.Customer || x.PartnerType == BusinessPartnerType.Both || x.PartnerType == BusinessPartnerType.Prospect))
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var supplierCounts = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(x => x.IsActive && (x.PartnerType == BusinessPartnerType.Supplier || x.PartnerType == BusinessPartnerType.Both))
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var productCounts = await dbContext.Products
            .AsNoTracking()
            .Where(x => x.IsActive)
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var warehouseCounts = await dbContext.Warehouses
            .AsNoTracking()
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var monthlyDocumentCounts = await dbContext.CommercialDocuments
            .AsNoTracking()
            .Where(x => x.DocumentDate >= monthStart && x.DocumentDate <= monthEnd && x.Status != CommercialDocumentStatus.Cancelled)
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        return tenants
            .Select(tenant =>
            {
                var subscription = tenant.LatestSubscription;
                var userCount = userCounts.GetValueOrDefault(tenant.Id);
                var customerCount = customerCounts.GetValueOrDefault(tenant.Id);
                var supplierCount = supplierCounts.GetValueOrDefault(tenant.Id);
                var productCount = productCounts.GetValueOrDefault(tenant.Id);
                var warehouseCount = warehouseCounts.GetValueOrDefault(tenant.Id);
                var documentCount = monthlyDocumentCounts.GetValueOrDefault(tenant.Id);

                var maxUsers = subscription?.MaxUsersOverride ?? subscription?.PlanMaxUsers ?? 0;
                var maxCustomers = subscription?.MaxCustomersOverride ?? subscription?.PlanMaxCustomers ?? 0;
                var maxSuppliers = subscription?.MaxSuppliersOverride ?? subscription?.PlanMaxSuppliers ?? 0;
                var maxProducts = subscription?.MaxProductsOverride ?? subscription?.PlanMaxProducts ?? 0;
                var maxWarehouses = subscription?.MaxWarehousesOverride ?? subscription?.PlanMaxWarehouses ?? 0;
                var maxMonthlyDocuments = subscription?.MaxMonthlyDocumentsOverride ?? subscription?.PlanMaxMonthlyDocuments ?? 0;

                var quotas = new[]
                {
                    new QuotaUsageItem("Utilisateurs", userCount, maxUsers, userCount > maxUsers),
                    new QuotaUsageItem("Clients", customerCount, maxCustomers, customerCount > maxCustomers),
                    new QuotaUsageItem("Fournisseurs", supplierCount, maxSuppliers, supplierCount > maxSuppliers),
                    new QuotaUsageItem("Articles", productCount, maxProducts, productCount > maxProducts),
                    new QuotaUsageItem("Depots", warehouseCount, maxWarehouses, warehouseCount > maxWarehouses),
                    new QuotaUsageItem("Documents du mois", documentCount, maxMonthlyDocuments, documentCount > maxMonthlyDocuments)
                };

                return new TenantAdminSummary(
                    tenant.Id,
                    tenant.CompanyName,
                    tenant.Slug,
                    tenant.PrimaryContactEmail,
                    tenant.IsActive,
                    subscription?.PlanLabel ?? "Sans abonnement",
                    subscription?.Status ?? SubscriptionStatus.Trial,
                    subscription?.StartsOn ?? today,
                    subscription?.EndsOn,
                    subscription?.NextBillingDate,
                    subscription?.MonthlyPriceOverride ?? subscription?.PlanMonthlyPrice ?? 0m,
                    userCount,
                    customerCount,
                    supplierCount,
                    productCount,
                    warehouseCount,
                    documentCount,
                    quotas.Count(x => GetQuotaSeverity(x).HasValue),
                    quotas);
            })
            .ToList();
    }

    public async Task<PlatformInvoice> GeneratePlatformInvoiceAsync(Guid tenantId, DateOnly issueDate, DateOnly dueDate, CancellationToken cancellationToken = default)
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

        if (subscription is null || subscription.SubscriptionPlan is null)
        {
            throw new InvalidOperationException("Aucun abonnement exploitable n'est defini pour ce tenant.");
        }

        var periodStart = new DateOnly(issueDate.Year, issueDate.Month, 1);
        var periodEnd = new DateOnly(issueDate.Year, issueDate.Month, DateTime.DaysInMonth(issueDate.Year, issueDate.Month));

        var existingForPeriod = await dbContext.PlatformInvoices
            .AsNoTracking()
            .AnyAsync(
                x => x.TenantId == tenantId &&
                     x.PeriodStart == periodStart &&
                     x.PeriodEnd == periodEnd &&
                     x.Status != PlatformInvoiceStatus.Cancelled,
                cancellationToken);

        if (existingForPeriod)
        {
            throw new InvalidOperationException("Une facture plateforme existe deja pour cette periode.");
        }

        var summaries = await GetTenantSummariesAsync(cancellationToken);
        var summary = summaries.FirstOrDefault(x => x.TenantId == tenantId);
        if (summary is null)
        {
            throw new InvalidOperationException("Impossible de calculer les usages du tenant.");
        }

        var invoice = new PlatformInvoice
        {
            TenantId = tenantId,
            TenantSubscriptionId = subscription.Id,
            InvoiceNumber = await GetNextInvoiceNumberAsync(issueDate.Year, cancellationToken),
            IssueDate = issueDate,
            DueDate = dueDate,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = PlatformInvoiceStatus.Issued,
            CurrencyCode = tenant.CurrencyCode
        };

        invoice.Lines.Add(new PlatformInvoiceLine
        {
            Description = $"Abonnement SaaS {subscription.SubscriptionPlan.Label} - {periodStart:MM/yyyy}",
            Quantity = 1m,
            UnitPriceExcludingTax = subscription.MonthlyPriceOverride ?? subscription.SubscriptionPlan.MonthlyPrice,
            TaxRate = 0m
        });

        AddOverageLine(invoice, "Utilisateurs supplementaires", summary.UserCount, summary.Quotas.First(x => x.Label == "Utilisateurs").Limit, subscription.SubscriptionPlan.OverageUserPrice);
        AddOverageLine(invoice, "Articles supplementaires", summary.ProductCount, summary.Quotas.First(x => x.Label == "Articles").Limit, subscription.SubscriptionPlan.OverageProductPrice);
        AddOverageLine(invoice, "Documents supplementaires du mois", summary.MonthlyDocumentCount, summary.Quotas.First(x => x.Label == "Documents du mois").Limit, subscription.SubscriptionPlan.OverageDocumentPrice);

        RecalculateInvoiceTotals(invoice);
        dbContext.PlatformInvoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    public void RecalculateInvoiceTotals(PlatformInvoice invoice)
    {
        foreach (var line in invoice.Lines)
        {
            line.LineTotalExcludingTax = decimal.Round(line.Quantity * line.UnitPriceExcludingTax, 2);
            line.LineTaxAmount = decimal.Round(line.LineTotalExcludingTax * (line.TaxRate / 100m), 2);
            line.LineTotalIncludingTax = line.LineTotalExcludingTax + line.LineTaxAmount;
        }

        invoice.TotalExcludingTax = invoice.Lines.Sum(x => x.LineTotalExcludingTax);
        invoice.TotalTax = invoice.Lines.Sum(x => x.LineTaxAmount);
        invoice.TotalIncludingTax = invoice.Lines.Sum(x => x.LineTotalIncludingTax);
    }

    public async Task AcknowledgeQuotaNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.TenantQuotaNotifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && !x.IsResolved, cancellationToken);

        if (notification is null)
        {
            throw new InvalidOperationException("Notification introuvable.");
        }

        notification.IsAcknowledged = true;
        notification.AcknowledgedOnUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GetNextInvoiceNumberAsync(int year, CancellationToken cancellationToken)
    {
        var prefix = $"SAS-{year}-";
        var lastNumber = await dbContext.PlatformInvoices
            .AsNoTracking()
            .Where(x => x.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(x => x.InvoiceNumber)
            .Select(x => x.InvoiceNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var nextValue = 1;
        if (!string.IsNullOrWhiteSpace(lastNumber) && int.TryParse(lastNumber[prefix.Length..], out var parsed))
        {
            nextValue = parsed + 1;
        }

        return $"{prefix}{nextValue:0000}";
    }

    private async Task SynchronizeQuotaNotificationsAsync(IReadOnlyList<TenantAdminSummary> summaries, CancellationToken cancellationToken)
    {
        var existingNotifications = await dbContext.TenantQuotaNotifications
            .Where(x => !x.IsResolved)
            .ToListAsync(cancellationToken);

        var existingByKey = existingNotifications.ToDictionary(
            x => BuildNotificationKey(x.TenantId, x.QuotaLabel),
            StringComparer.OrdinalIgnoreCase);

        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var hasChanges = false;
        var notificationsToEmail = new List<(TenantQuotaNotification Notification, TenantAdminSummary Summary, QuotaUsageItem Quota, PlatformNotificationSeverity Severity)>();

        foreach (var summary in summaries)
        {
            foreach (var quota in summary.Quotas)
            {
                var severity = GetQuotaSeverity(quota);
                if (!severity.HasValue)
                {
                    continue;
                }

                var key = BuildNotificationKey(summary.TenantId, quota.Label);
                activeKeys.Add(key);
                var title = BuildNotificationTitle(quota, severity.Value);
                var message = BuildNotificationMessage(summary, quota, severity.Value);

                if (existingByKey.TryGetValue(key, out var existing))
                {
                    var metricsChanged = existing.Used != quota.Used || existing.Limit != quota.Limit;
                    var severityChanged = existing.Severity != severity.Value;
                    var contentChanged = existing.Title != title || existing.Message != message;

                    if (severityChanged)
                    {
                        existing.Severity = severity.Value;
                        existing.IsAcknowledged = false;
                        existing.AcknowledgedOnUtc = null;
                        existing.LastEmailSentOnUtc = null;
                        notificationsToEmail.Add((existing, summary, quota, severity.Value));
                        hasChanges = true;
                    }

                    if (metricsChanged)
                    {
                        existing.Used = quota.Used;
                        existing.Limit = quota.Limit;
                        existing.IsAcknowledged = false;
                        existing.AcknowledgedOnUtc = null;
                        existing.LastTriggeredOnUtc = now;
                        hasChanges = true;
                    }

                    if (contentChanged)
                    {
                        existing.Title = title;
                        existing.Message = message;
                        hasChanges = true;
                    }

                    if (existing.LastEmailSentOnUtc is null && notificationsToEmail.All(x => x.Notification.Id != existing.Id))
                    {
                        notificationsToEmail.Add((existing, summary, quota, severity.Value));
                    }
                }
                else
                {
                    var notification = new TenantQuotaNotification
                    {
                        TenantId = summary.TenantId,
                        QuotaLabel = quota.Label,
                        Severity = severity.Value,
                        Title = title,
                        Message = message,
                        Used = quota.Used,
                        Limit = quota.Limit,
                        LastTriggeredOnUtc = now
                    };

                    dbContext.TenantQuotaNotifications.Add(notification);
                    notificationsToEmail.Add((notification, summary, quota, severity.Value));

                    hasChanges = true;
                }
            }
        }

        foreach (var notification in existingNotifications.Where(x => !activeKeys.Contains(BuildNotificationKey(x.TenantId, x.QuotaLabel))))
        {
            notification.IsResolved = true;
            notification.ResolvedOnUtc = now;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (notificationsToEmail.Count > 0)
        {
            await SendQuotaNotificationEmailsAsync(notificationsToEmail, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<QuotaNotificationItem>> GetOpenQuotaNotificationsAsync(IReadOnlyList<TenantAdminSummary> summaries, CancellationToken cancellationToken)
    {
        var summaryLookup = summaries.ToDictionary(x => x.TenantId);

        var notifications = await dbContext.TenantQuotaNotifications
            .AsNoTracking()
            .Include(x => x.Tenant)
            .Where(x => !x.IsResolved && !x.IsAcknowledged)
            .OrderByDescending(x => x.Severity)
            .ThenByDescending(x => x.LastTriggeredOnUtc)
            .ToListAsync(cancellationToken);

        return notifications
            .Select(x =>
            {
                var summary = summaryLookup.GetValueOrDefault(x.TenantId);

                return new QuotaNotificationItem(
                    x.Id,
                    x.TenantId,
                    x.Tenant?.CompanyName ?? "Tenant inconnu",
                    summary?.PlanName ?? "Sans abonnement",
                    x.QuotaLabel,
                    x.Severity,
                    x.Title,
                    x.Message,
                    x.Used,
                    x.Limit,
                    x.LastTriggeredOnUtc);
            })
            .ToList();
    }

    private static PlatformNotificationSeverity? GetQuotaSeverity(QuotaUsageItem quota)
    {
        if (quota.Limit <= 0)
        {
            return quota.Used > 0 ? PlatformNotificationSeverity.Critical : null;
        }

        if (quota.Used > quota.Limit)
        {
            return PlatformNotificationSeverity.Critical;
        }

        var warningThreshold = (int)Math.Ceiling(quota.Limit * QuotaWarningThreshold);
        warningThreshold = Math.Max(1, warningThreshold);

        return quota.Used >= warningThreshold
            ? PlatformNotificationSeverity.Warning
            : null;
    }

    private static string BuildNotificationKey(Guid tenantId, string quotaLabel) => $"{tenantId:N}:{quotaLabel}";

    private static string BuildNotificationTitle(QuotaUsageItem quota, PlatformNotificationSeverity severity) =>
        severity == PlatformNotificationSeverity.Critical
            ? $"Quota {quota.Label.ToLowerInvariant()} depasse"
            : $"Quota {quota.Label.ToLowerInvariant()} proche de la limite";

    private static string BuildNotificationMessage(TenantAdminSummary tenant, QuotaUsageItem quota, PlatformNotificationSeverity severity)
    {
        return severity == PlatformNotificationSeverity.Critical
            ? $"Le tenant {tenant.CompanyName} depasse le quota {quota.Label.ToLowerInvariant()} ({quota.Used}/{quota.Limit}). Les actions liees a ce quota peuvent maintenant etre bloquees tant que le plan n'est pas ajuste."
            : $"Le tenant {tenant.CompanyName} approche la limite du quota {quota.Label.ToLowerInvariant()} ({quota.Used}/{quota.Limit}). Anticipez une mise a niveau avant les prochains blocages.";
    }

    private async Task SendQuotaNotificationEmailsAsync(
        IReadOnlyCollection<(TenantQuotaNotification Notification, TenantAdminSummary Summary, QuotaUsageItem Quota, PlatformNotificationSeverity Severity)> notificationsToEmail,
        CancellationToken cancellationToken)
    {
        if (!notificationEmailService.IsConfigured)
        {
            return;
        }

        var platformAdminEmails = await GetPlatformAdminEmailsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var hasChanges = false;

        foreach (var item in notificationsToEmail.Where(x => x.Notification.LastEmailSentOnUtc is null))
        {
            var recipients = BuildNotificationRecipients(item.Summary, platformAdminEmails);
            if (recipients.Count == 0)
            {
                continue;
            }

            var subject = BuildNotificationEmailSubject(item.Summary, item.Quota, item.Severity);
            var body = BuildNotificationEmailBody(item.Summary, item.Quota, item.Severity);

            try
            {
                var sent = await notificationEmailService.TrySendAsync(recipients, subject, body, cancellationToken);
                if (!sent)
                {
                    continue;
                }

                item.Notification.LastEmailSentOnUtc = now;
                hasChanges = true;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Impossible d'envoyer l'e-mail de notification de quota pour le tenant {TenantId} et le quota {QuotaLabel}.",
                    item.Summary.TenantId,
                    item.Quota.Label);
            }
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<IReadOnlyList<string>> GetPlatformAdminEmailsAsync(CancellationToken cancellationToken)
    {
        return await (
            from user in dbContext.Users
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where role.Name == "PlatformAdmin" && !string.IsNullOrWhiteSpace(user.Email)
            select user.Email!)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyList<string> BuildNotificationRecipients(TenantAdminSummary summary, IReadOnlyList<string> platformAdminEmails)
    {
        var recipients = new List<string>();
        if (!string.IsNullOrWhiteSpace(summary.PrimaryContactEmail))
        {
            recipients.Add(summary.PrimaryContactEmail);
        }

        recipients.AddRange(platformAdminEmails);
        return recipients
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildNotificationEmailSubject(TenantAdminSummary summary, QuotaUsageItem quota, PlatformNotificationSeverity severity) =>
        severity == PlatformNotificationSeverity.Critical
            ? $"[Gescom SaaS] Quota {quota.Label.ToLowerInvariant()} depasse pour {summary.CompanyName}"
            : $"[Gescom SaaS] Quota {quota.Label.ToLowerInvariant()} proche de la limite pour {summary.CompanyName}";

    private static string BuildNotificationEmailBody(TenantAdminSummary summary, QuotaUsageItem quota, PlatformNotificationSeverity severity)
    {
        var stateLine = severity == PlatformNotificationSeverity.Critical
            ? "Le quota est maintenant depasse et certaines actions peuvent etre bloquees."
            : "Le quota approche de la limite et un blocage peut survenir prochainement.";

        return
            $"Tenant : {summary.CompanyName}{Environment.NewLine}" +
            $"Plan : {summary.PlanName}{Environment.NewLine}" +
            $"Quota : {quota.Label}{Environment.NewLine}" +
            $"Usage : {quota.Used}/{quota.Limit}{Environment.NewLine}" +
            $"{Environment.NewLine}" +
            $"{stateLine}{Environment.NewLine}" +
            $"Action recommandee : passez le tenant sur un plan superieur dans SaaS Admin > Tenants, ou demandez a l'administrateur plateforme de mettre a niveau l'abonnement.{Environment.NewLine}" +
            $"{Environment.NewLine}" +
            $"Message : {BuildNotificationMessage(summary, quota, severity)}";
    }

    private static void AddOverageLine(PlatformInvoice invoice, string description, int used, int limit, decimal unitPrice)
    {
        if (unitPrice <= 0m || used <= limit)
        {
            return;
        }

        invoice.Lines.Add(new PlatformInvoiceLine
        {
            Description = description,
            Quantity = used - limit,
            UnitPriceExcludingTax = unitPrice,
            TaxRate = 0m
        });
    }
}
