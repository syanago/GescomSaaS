using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Domain.Exceptions;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Infrastructure.Services;

public class SettlementService(ApplicationDbContext dbContext) : ISettlementService
{
    public async Task<IReadOnlyList<OpenItemSummary>> GetOpenItemsAsync(Guid tenantId, PaymentDirection direction, CancellationToken cancellationToken = default)
    {
        var invoiceType = direction == PaymentDirection.Incoming
            ? CommercialDocumentType.SalesInvoice
            : CommercialDocumentType.PurchaseInvoice;

        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = await dbContext.CommercialDocuments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.PaymentAllocations)
            .Include(x => x.ReminderLogs)
            .Where(x => x.TenantId == tenantId && x.DocumentType == invoiceType && x.Status != CommercialDocumentStatus.Cancelled)
            .OrderBy(x => x.DueDate ?? x.DocumentDate)
            .ThenBy(x => x.Number)
            .Select(x => new
            {
                x.Id,
                x.Number,
                x.PartnerId,
                PartnerCode = x.Partner != null ? x.Partner.Code : null,
                PartnerName = x.Partner != null ? x.Partner.Name : "-",
                x.DocumentDate,
                x.DueDate,
                x.CurrencyCode,
                x.TotalIncludingTax,
                PaidAmount = x.PaymentAllocations.Sum(a => a.AllocatedAmount),
                x.Status,
                x.PaymentStatus,
                x.InDispute,
                x.PromiseToPayDate,
                LastReminder = x.ReminderLogs
                    .OrderByDescending(r => r.SentOnUtc)
                    .Select(r => new { r.ReminderLevel, r.SentOnUtc })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return items
            .Select(x =>
            {
                var balance = x.TotalIncludingTax - x.PaidAmount;
                var overdueDays = x.DueDate is DateOnly dueDate && balance > 0m && dueDate < now
                    ? now.DayNumber - dueDate.DayNumber
                    : 0;

                return new OpenItemSummary(
                    x.Id,
                    x.PartnerId,
                    x.PartnerCode,
                    x.Number,
                    x.PartnerName,
                    x.DocumentDate,
                    x.DueDate,
                    x.CurrencyCode,
                    x.TotalIncludingTax,
                    x.PaidAmount,
                    balance,
                    overdueDays,
                    x.Status,
                    x.PaymentStatus,
                    x.InDispute,
                    x.PromiseToPayDate,
                    x.LastReminder?.ReminderLevel,
                    x.LastReminder?.SentOnUtc);
            })
            .Where(x => x.BalanceAmount > 0m)
            .ToList();
    }

    public async Task<IReadOnlyList<PaymentHistoryItem>> GetPaymentsAsync(Guid tenantId, PaymentDirection? direction = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Payments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.Allocations)
            .Where(x => x.TenantId == tenantId);

        if (direction.HasValue)
        {
            query = query.Where(x => x.Direction == direction.Value);
        }

        return await query
            .OrderByDescending(x => x.PaymentDate)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Select(x => new PaymentHistoryItem(
                x.Id,
                x.PaymentDate,
                x.ReferenceNumber,
                x.PartnerId,
                x.Partner != null ? x.Partner.Code : null,
                x.Partner != null ? x.Partner.Name : "-",
                x.Direction,
                x.Type,
                x.Method,
                x.Amount - x.Allocations.Sum(a => a.AllocatedAmount) <= 0m
                    ? PaymentAllocationStatus.Allocated
                    : x.Allocations.Count > 0
                        ? PaymentAllocationStatus.PartiallyAllocated
                        : PaymentAllocationStatus.Unallocated,
                x.CurrencyCode,
                x.Amount,
                x.Allocations.Sum(a => a.AllocatedAmount),
                x.Amount - x.Allocations.Sum(a => a.AllocatedAmount),
                x.Allocations.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AvailablePaymentSummary>> GetAvailablePaymentsAsync(Guid tenantId, Guid partnerId, PaymentDirection direction, CancellationToken cancellationToken = default)
    {
        return await dbContext.Payments
            .AsNoTracking()
            .Include(x => x.Allocations)
            .Where(x => x.TenantId == tenantId && x.PartnerId == partnerId && x.Direction == direction)
            .Where(x => x.Amount - x.Allocations.Sum(a => a.AllocatedAmount) > 0m)
            .OrderByDescending(x => x.PaymentDate)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Select(x => new AvailablePaymentSummary(
                x.Id,
                x.PaymentDate,
                x.ReferenceNumber,
                x.Type,
                x.Method,
                x.CurrencyCode,
                x.Amount,
                x.Allocations.Sum(a => a.AllocatedAmount),
                x.Amount - x.Allocations.Sum(a => a.AllocatedAmount)))
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerAccountSummary?> GetCustomerAccountAsync(Guid tenantId, Guid partnerId, PaymentDirection direction, CancellationToken cancellationToken = default)
    {
        var tenant = await GetTenantAsync(tenantId, cancellationToken);
        var partner = await dbContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == partnerId && x.TenantId == tenantId, cancellationToken);

        if (partner is null)
        {
            return null;
        }

        var openItems = (await GetOpenItemsAsync(tenantId, direction, cancellationToken))
            .Where(x => x.PartnerId == partnerId)
            .ToList();

        var payments = (await GetPaymentsAsync(tenantId, direction, cancellationToken))
            .Where(x => x.PartnerId == partnerId)
            .Take(20)
            .ToList();

        var availablePayments = await GetAvailablePaymentsAsync(tenantId, partnerId, direction, cancellationToken);
        var reminderCount = await dbContext.ReminderLogs
            .AsNoTracking()
            .Include(x => x.CommercialDocument)
            .CountAsync(
                x => x.TenantId == tenantId &&
                     x.CommercialDocument != null &&
                     x.CommercialDocument.PartnerId == partnerId,
                cancellationToken);

        var openAmount = openItems.Sum(x => x.BalanceAmount);
        var overdueAmount = openItems.Where(x => x.OverdueDays > 0).Sum(x => x.BalanceAmount);
        var oldestOverdueDays = openItems.Count == 0 ? 0 : openItems.Max(x => x.OverdueDays);
        var hasOverdueDocuments = openItems.Any(x => x.OverdueDays > 0);
        var creditLimitExceeded = partner.CreditLimit > 0m && openAmount > partner.CreditLimit;
        var creditRemaining = partner.CreditLimit > 0m ? partner.CreditLimit - openAmount : 0m;
        var blockSalesOrder = (tenant.BlockSalesOrdersOnCreditLimit && creditLimitExceeded)
            || (tenant.BlockSalesOrdersOnOverdue && hasOverdueDocuments);
        var blockDelivery = (tenant.BlockDeliveriesOnCreditLimit && creditLimitExceeded)
            || (tenant.BlockDeliveriesOnOverdue && hasOverdueDocuments);
        var accountStatus = ResolveAccountStatus(blockSalesOrder, blockDelivery, creditLimitExceeded, hasOverdueDocuments);

        return new CustomerAccountSummary(
            partner.Id,
            partner.Code,
            partner.Name,
            accountStatus,
            partner.CreditLimit,
            creditRemaining,
            openAmount,
            overdueAmount,
            oldestOverdueDays,
            creditLimitExceeded,
            !blockSalesOrder,
            !blockDelivery,
            availablePayments.Where(x => x.Type == PaymentType.Deposit).Sum(x => x.AvailableAmount),
            availablePayments.Where(x => x.Type != PaymentType.Deposit).Sum(x => x.AvailableAmount),
            openItems.Count,
            openItems.Count(x => x.OverdueDays > 0),
            reminderCount,
            openItems,
            payments,
            availablePayments);
    }

    public async Task<IReadOnlyList<ReminderQueueItem>> GetReminderQueueAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await GetTenantAsync(tenantId, cancellationToken);
        var items = await GetOpenItemsAsync(tenantId, PaymentDirection.Incoming, cancellationToken);

        return items
            .Where(x => x.PartnerId.HasValue && x.OverdueDays >= tenant.ReminderFriendlyDelayDays)
            .Where(x => !x.InDispute)
            .Where(x => x.PromiseToPayDate is not DateOnly promiseToPayDate || promiseToPayDate < DateOnly.FromDateTime(DateTime.UtcNow))
            .Select(x => TryGetRecommendedReminder(x, tenant, out var level)
                ? new ReminderQueueItem(
                    x.DocumentId,
                    x.PartnerId!.Value,
                    x.PartnerCode ?? string.Empty,
                    x.PartnerName,
                    x.Number,
                    x.DocumentDate,
                    x.DueDate,
                    x.CurrencyCode,
                    x.BalanceAmount,
                    x.OverdueDays,
                    level,
                    x.LastReminderLevel,
                    x.LastReminderOnUtc,
                    x.InDispute,
                    x.PromiseToPayDate)
                : null)
            .Where(x => x is not null)
            .Cast<ReminderQueueItem>()
            .OrderByDescending(x => x.OverdueDays)
            .ThenBy(x => x.PartnerCode)
            .ThenBy(x => x.Number)
            .ToList();
    }

    public async Task RegisterPaymentAsync(Guid tenantId, PaymentRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await GetTenantAsync(tenantId, cancellationToken);
        var allowedMethods = PaymentMethodCatalog.DeserializeSelection(tenant.PaymentMethodsJson);
        if (!allowedMethods.Contains(request.Method))
        {
            throw new BusinessRuleException(
                "Le mode de reglement selectionne n'est pas autorise pour ce tenant.",
                errorCode: "PAYMENT_METHOD_NOT_ALLOWED");
        }

        if (request.Amount <= 0m)
        {
            throw new BusinessRuleException(
                "Le montant du reglement doit etre strictement positif.",
                errorCode: "PAYMENT_AMOUNT_INVALID");
        }

        var partner = await dbContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PartnerId && x.TenantId == tenantId, cancellationToken);

        if (partner is null)
        {
            throw new NotFoundException(nameof(BusinessPartner), request.PartnerId);
        }

        var payment = new Payment
        {
            TenantId = tenantId,
            PaymentDate = request.PaymentDate,
            Direction = request.Direction,
            Type = request.Type,
            Method = request.Method,
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber)
                ? $"{(request.Type == PaymentType.Deposit ? "ACP" : "REG")}-{DateTime.UtcNow:yyyyMMddHHmmss}"
                : request.ReferenceNumber.Trim(),
            CurrencyCode = tenant.CurrencyCode,
            Amount = request.Amount,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            PartnerId = request.PartnerId,
            SourceCommercialDocumentId = request.DocumentId
        };

        List<CommercialDocument> touchedDocuments = [];
        if (request.DocumentId.HasValue)
        {
            var document = await LoadAllocatableDocumentAsync(tenantId, request.DocumentId.Value, request.PartnerId, request.Direction, cancellationToken);
            var balance = document.TotalIncludingTax - document.PaymentAllocations.Sum(x => x.AllocatedAmount);
            if (request.Amount > balance)
            {
                throw new BusinessRuleException(
                    "Le montant du reglement depasse le solde restant du document.",
                    errorCode: "PAYMENT_AMOUNT_EXCEEDS_DOCUMENT_BALANCE");
            }

            AttachAllocation(payment, document, request.Amount, null);
            payment.CurrencyCode = document.CurrencyCode;
            touchedDocuments.Add(document);
        }
        else if (request.Type == PaymentType.Standard)
        {
            var effectiveMode = request.Direction == PaymentDirection.Incoming
                ? request.AllocationMode
                : PaymentAllocationMode.Manual;

            if (effectiveMode == PaymentAllocationMode.Manual)
            {
                throw new BusinessRuleException(
                    "Selectionnez une facture, utilisez l'affectation automatique ou enregistrez ce montant comme acompte.",
                    errorCode: "PAYMENT_TARGET_REQUIRED");
            }

            var openDocuments = await LoadAllocatableDocumentsAsync(tenantId, request.PartnerId, request.Direction, effectiveMode, cancellationToken);
            if (openDocuments.Count == 0)
            {
                throw new BusinessRuleException(
                    "Aucune facture ouverte n'est disponible pour affectation automatique.",
                    errorCode: "PAYMENT_NO_OPEN_INVOICE");
            }

            var remaining = request.Amount;
            foreach (var document in openDocuments)
            {
                var balance = document.TotalIncludingTax - document.PaymentAllocations.Sum(x => x.AllocatedAmount);
                if (balance <= 0m)
                {
                    continue;
                }

                var allocatedAmount = Math.Min(balance, remaining);
                if (allocatedAmount <= 0m)
                {
                    continue;
                }

                AttachAllocation(payment, document, allocatedAmount, "Affectation automatique");
                touchedDocuments.Add(document);
                remaining -= allocatedAmount;

                if (remaining <= 0m)
                {
                    break;
                }
            }

            if (touchedDocuments.Count == 0)
            {
                throw new BusinessRuleException(
                    "Aucune echeance compatible n'a pu etre affectee automatiquement.",
                    errorCode: "PAYMENT_NO_COMPATIBLE_DUE_DATE");
            }

            payment.CurrencyCode = touchedDocuments[0].CurrencyCode;
        }

        ApplyPaymentSnapshot(payment);
        dbContext.Payments.Add(payment);

        foreach (var document in touchedDocuments.DistinctBy(x => x.Id))
        {
            ApplyDocumentSnapshot(document);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AllocatePaymentAsync(Guid tenantId, PaymentManualAllocationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.AllocatedAmount <= 0m)
        {
            throw new BusinessRuleException(
                "Le montant a affecter doit etre strictement positif.",
                errorCode: "ALLOCATION_AMOUNT_INVALID");
        }

        var payment = await dbContext.Payments
            .Include(x => x.Allocations)
            .FirstOrDefaultAsync(x => x.Id == request.PaymentId && x.TenantId == tenantId, cancellationToken);

        if (payment is null)
        {
            throw new NotFoundException(nameof(Payment), request.PaymentId);
        }

        var document = await LoadAllocatableDocumentAsync(tenantId, request.CommercialDocumentId, payment.PartnerId, payment.Direction, cancellationToken);
        ApplyPaymentSnapshot(payment);
        var documentBalance = document.TotalIncludingTax - document.PaymentAllocations.Sum(x => x.AllocatedAmount);

        if (request.AllocatedAmount > payment.AvailableAmount)
        {
            throw new BusinessRuleException(
                "Le montant a affecter depasse le disponible du reglement.",
                errorCode: "ALLOCATION_EXCEEDS_AVAILABLE");
        }

        if (request.AllocatedAmount > documentBalance)
        {
            throw new BusinessRuleException(
                "Le montant a affecter depasse le solde restant de la facture.",
                errorCode: "ALLOCATION_EXCEEDS_INVOICE_BALANCE");
        }

        AttachAllocation(payment, document, request.AllocatedAmount, request.Notes);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RefreshPaymentAndDocumentSnapshotsAsync(payment.Id, document.Id, cancellationToken);
    }

    public async Task<DepositApplicationResult> ApplyAvailableDepositsAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.CommercialDocuments
            .Include(x => x.PaymentAllocations)
            .FirstOrDefaultAsync(
                x => x.Id == documentId &&
                     x.TenantId == tenantId &&
                     x.DocumentType == CommercialDocumentType.SalesInvoice,
                cancellationToken);

        if (document is null)
        {
            throw new NotFoundException(nameof(CommercialDocument), documentId);
        }

        ApplyDocumentSnapshot(document);
        if (document.BalanceAmount <= 0m)
        {
            throw new BusinessRuleException(
                "Cette facture est deja totalement reglee.",
                errorCode: "INVOICE_ALREADY_SETTLED");
        }

        var deposits = await dbContext.Payments
            .Include(x => x.Allocations)
            .Where(x =>
                x.TenantId == tenantId &&
                x.PartnerId == document.PartnerId &&
                x.Direction == PaymentDirection.Incoming &&
                x.Type == PaymentType.Deposit)
            .OrderBy(x => x.PaymentDate)
            .ThenBy(x => x.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        decimal appliedAmount = 0m;
        var paymentCount = 0;
        var remainingBalance = document.BalanceAmount;

        foreach (var deposit in deposits)
        {
            ApplyPaymentSnapshot(deposit);
            if (deposit.AvailableAmount <= 0m)
            {
                continue;
            }

            var amountToApply = Math.Min(deposit.AvailableAmount, remainingBalance);
            if (amountToApply <= 0m)
            {
                continue;
            }

            AttachAllocation(deposit, document, amountToApply, "Imputation automatique d'acompte");
            appliedAmount += amountToApply;
            paymentCount++;
            remainingBalance -= amountToApply;

            if (remainingBalance <= 0m)
            {
                break;
            }
        }

        if (appliedAmount <= 0m)
        {
            throw new BusinessRuleException(
                "Aucun acompte disponible n'a ete trouve pour ce client.",
                errorCode: "DEPOSIT_NONE_AVAILABLE");
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DepositApplicationResult(
            decimal.Round(appliedAmount, 2),
            paymentCount,
            decimal.Round(Math.Max(remainingBalance, 0m), 2));
    }

    public async Task<OfflinePaymentApplyResult> UpsertOfflinePaymentAsync(Guid tenantId, OfflinePaymentSyncItem item, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.ReferenceNumber))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["ReferenceNumber"] = new[] { "La reference du reglement est obligatoire pour la synchronisation." },
            });
        }

        if (string.IsNullOrWhiteSpace(item.PartnerCode))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["PartnerCode"] = new[] { "Le code tiers du reglement est obligatoire pour la synchronisation." },
            });
        }

        var referenceNumber = item.ReferenceNumber.Trim();
        var partnerCode = item.PartnerCode.Trim().ToUpperInvariant();
        var direction = ParseEnum(item.Direction, PaymentDirection.Incoming);
        var type = ParseEnum(item.Type, PaymentType.Standard);
        var method = ParseEnum(item.Method, PaymentMethod.BankTransfer);
        var allocationStatus = ParseEnum(item.AllocationStatus, PaymentAllocationStatus.Unallocated);

        var partner = await dbContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == partnerCode, cancellationToken);

        if (partner is null)
        {
            var ex = new NotFoundException(nameof(BusinessPartner), partnerCode);
            ex.Data["referenceNumber"] = referenceNumber;
            throw ex;
        }

        var payment = await dbContext.Payments
            .Include(x => x.Allocations)
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                     && x.ReferenceNumber == referenceNumber
                     && x.PartnerId == partner.Id
                     && x.Direction == direction,
                cancellationToken);

        var created = payment is null;
        var updated = false;
        if (payment is null)
        {
            payment = new Payment { TenantId = tenantId };
            dbContext.Payments.Add(payment);
        }

        var normalizedCurrency = string.IsNullOrWhiteSpace(item.CurrencyCode) ? "CAD" : item.CurrencyCode.Trim().ToUpperInvariant();
        var normalizedNotes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();

        updated |= ApplyChange(payment.ReferenceNumber, referenceNumber, value => payment.ReferenceNumber = value);
        updated |= ApplyChange(payment.PaymentDate, item.PaymentDate, value => payment.PaymentDate = value);
        updated |= ApplyChange(payment.Direction, direction, value => payment.Direction = value);
        updated |= ApplyChange(payment.Type, type, value => payment.Type = value);
        updated |= ApplyChange(payment.Method, method, value => payment.Method = value);
        updated |= ApplyChange(payment.AllocationStatus, allocationStatus, value => payment.AllocationStatus = value);
        updated |= ApplyChange(payment.CurrencyCode, normalizedCurrency, value => payment.CurrencyCode = value);
        updated |= ApplyChange(payment.Amount, item.Amount, value => payment.Amount = value);
        updated |= ApplyChange(payment.AllocatedAmount, item.AllocatedAmount, value => payment.AllocatedAmount = value);
        updated |= ApplyChange(payment.AvailableAmount, item.AvailableAmount, value => payment.AvailableAmount = value);
        updated |= ApplyChange(payment.Notes, normalizedNotes, value => payment.Notes = value);
        updated |= ApplyChange(payment.PartnerId, partner.Id, value => payment.PartnerId = value);

        Guid? sourceDocumentId = null;
        if (!string.IsNullOrWhiteSpace(item.SourceDocumentNumber))
        {
            var sourceNumber = item.SourceDocumentNumber.Trim().ToUpperInvariant();
            sourceDocumentId = await dbContext.CommercialDocuments
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Number == sourceNumber)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        updated |= ApplyChange(payment.SourceCommercialDocumentId, sourceDocumentId, value => payment.SourceCommercialDocumentId = value);

        await dbContext.SaveChangesAsync(cancellationToken);
        return new OfflinePaymentApplyResult(payment.Id, created, updated && !created);
    }

    public async Task ReplaceOfflineAllocationsAsync(Guid tenantId, Guid paymentId, IReadOnlyList<OfflinePaymentAllocationSyncItem> allocations, CancellationToken cancellationToken = default)
    {
        var payment = await dbContext.Payments
            .Include(x => x.Allocations)
            .FirstOrDefaultAsync(x => x.Id == paymentId && x.TenantId == tenantId, cancellationToken);

        if (payment is null)
        {
            throw new NotFoundException(nameof(Payment), paymentId);
        }

        var previousDocumentIds = payment.Allocations
            .Select(x => x.CommercialDocumentId)
            .Distinct()
            .ToArray();

        if (payment.Allocations.Count > 0)
        {
            dbContext.PaymentAllocations.RemoveRange(payment.Allocations.ToArray());
            payment.Allocations.Clear();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var documentId in previousDocumentIds)
        {
            var previousDocument = await dbContext.CommercialDocuments
                .Include(x => x.PaymentAllocations)
                .FirstOrDefaultAsync(x => x.Id == documentId && x.TenantId == tenantId, cancellationToken);

            if (previousDocument is null)
            {
                continue;
            }

            ApplyDocumentSnapshot(previousDocument);
        }

        if (previousDocumentIds.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        payment = await dbContext.Payments
            .Include(x => x.Allocations)
            .FirstAsync(x => x.Id == paymentId, cancellationToken);

        foreach (var allocationItem in allocations)
        {
            if (string.IsNullOrWhiteSpace(allocationItem.DocumentNumber) || allocationItem.AllocatedAmount <= 0m)
            {
                continue;
            }

            var documentNumber = allocationItem.DocumentNumber.Trim().ToUpperInvariant();
            var document = await dbContext.CommercialDocuments
                .Include(x => x.PaymentAllocations)
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Number == documentNumber, cancellationToken);

            if (document is null)
            {
                var notFound = new NotFoundException(nameof(CommercialDocument), documentNumber);
                notFound.Data["context"] = "offline-allocation";
                throw notFound;
            }

            if (document.PartnerId != payment.PartnerId)
            {
                throw new BusinessRuleException(
                    $"Le reglement {payment.ReferenceNumber} et la facture {documentNumber} ne concernent pas le meme tiers.",
                    errorCode: "PAYMENT_PARTNER_MISMATCH");
            }

            var expectedType = payment.Direction == PaymentDirection.Incoming
                ? CommercialDocumentType.SalesInvoice
                : CommercialDocumentType.PurchaseInvoice;

            if (document.DocumentType != expectedType)
            {
                throw new BusinessRuleException(
                    $"La piece {documentNumber} n'est pas compatible avec le reglement {payment.ReferenceNumber}.",
                    errorCode: "PAYMENT_DOCUMENT_INCOMPATIBLE");
            }

            var allocation = new PaymentAllocation
            {
                PaymentId = payment.Id,
                CommercialDocumentId = document.Id,
                AllocatedAmount = allocationItem.AllocatedAmount,
                AllocatedOnUtc = allocationItem.AllocatedOnUtc,
                Notes = string.IsNullOrWhiteSpace(allocationItem.Notes) ? null : allocationItem.Notes.Trim()
            };

            payment.Allocations.Add(allocation);
            document.PaymentAllocations.Add(allocation);
            ApplyDocumentSnapshot(document);
        }

        ApplyPaymentSnapshot(payment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RegisterReminderAsync(Guid tenantId, Guid documentId, ReminderLevel level, string? notes, CancellationToken cancellationToken = default)
    {
        var tenant = await GetTenantAsync(tenantId, cancellationToken);
        var document = await dbContext.CommercialDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == documentId &&
                     x.TenantId == tenantId &&
                     x.DocumentType == CommercialDocumentType.SalesInvoice,
                cancellationToken);

        if (document is null)
        {
            throw new BusinessRuleException(
                "La relance n'est disponible que sur une facture client.",
                errorCode: "REMINDER_REQUIRES_SALES_INVOICE");
        }

        dbContext.ReminderLogs.Add(new ReminderLog
        {
            TenantId = tenantId,
            CommercialDocumentId = documentId,
            ReminderLevel = level,
            Channel = "Manual",
            IsAutomatic = false,
            IsGrouped = false,
            NextActionDate = level switch
            {
                ReminderLevel.Friendly => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(Math.Max(tenant.ReminderFormalDelayDays - tenant.ReminderFriendlyDelayDays, 1)),
                ReminderLevel.Formal => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(Math.Max(tenant.ReminderFinalNoticeDelayDays - tenant.ReminderFormalDelayDays, 1)),
                _ => null
            },
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RegisterGroupedReminderAsync(Guid tenantId, Guid partnerId, string? notes, CancellationToken cancellationToken = default)
    {
        var queue = await GetReminderQueueAsync(tenantId, cancellationToken);
        var partnerItems = queue.Where(x => x.PartnerId == partnerId).ToList();
        if (partnerItems.Count == 0)
        {
            throw new BusinessRuleException(
                "Aucune relance groupee n'est disponible pour ce client.",
                errorCode: "REMINDER_NO_GROUPED_AVAILABLE");
        }

        foreach (var item in partnerItems)
        {
            dbContext.ReminderLogs.Add(new ReminderLog
            {
                TenantId = tenantId,
                CommercialDocumentId = item.DocumentId,
                ReminderLevel = item.RecommendedLevel,
                Channel = "Grouped",
                IsAutomatic = false,
                IsGrouped = true,
                NextActionDate = item.RecommendedLevel switch
                {
                    ReminderLevel.Friendly => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
                    ReminderLevel.Formal => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
                    _ => null
                },
                Notes = string.IsNullOrWhiteSpace(notes) ? "Relance groupee client" : notes.Trim()
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetDisputeStateAsync(Guid tenantId, Guid documentId, bool inDispute, string? notes, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.CommercialDocuments
            .Include(x => x.PaymentAllocations)
            .Include(x => x.ReminderLogs)
            .FirstOrDefaultAsync(
                x => x.Id == documentId &&
                     x.TenantId == tenantId &&
                     x.DocumentType == CommercialDocumentType.SalesInvoice,
                cancellationToken);

        if (document is null)
        {
            throw new NotFoundException(nameof(CommercialDocument), documentId);
        }

        document.InDispute = inDispute;
        if (inDispute)
        {
            document.PromiseToPayDate = null;
        }

        ApplyDocumentSnapshot(document);

        if (!string.IsNullOrWhiteSpace(notes))
        {
            dbContext.ReminderLogs.Add(new ReminderLog
            {
                TenantId = tenantId,
                CommercialDocumentId = document.Id,
                ReminderLevel = GetLastReminderLevelOrDefault(document),
                Channel = "Dispute",
                IsAutomatic = false,
                IsGrouped = false,
                Notes = notes.Trim()
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPromiseToPayAsync(Guid tenantId, Guid documentId, DateOnly? promiseToPayDate, string? notes, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.CommercialDocuments
            .Include(x => x.PaymentAllocations)
            .Include(x => x.ReminderLogs)
            .FirstOrDefaultAsync(
                x => x.Id == documentId &&
                     x.TenantId == tenantId &&
                     x.DocumentType == CommercialDocumentType.SalesInvoice,
                cancellationToken);

        if (document is null)
        {
            throw new NotFoundException(nameof(CommercialDocument), documentId);
        }

        document.PromiseToPayDate = promiseToPayDate;
        if (promiseToPayDate.HasValue)
        {
            document.InDispute = false;
        }

        ApplyDocumentSnapshot(document);

        if (promiseToPayDate.HasValue || !string.IsNullOrWhiteSpace(notes))
        {
            dbContext.ReminderLogs.Add(new ReminderLog
            {
                TenantId = tenantId,
                CommercialDocumentId = document.Id,
                ReminderLevel = GetLastReminderLevelOrDefault(document),
                Channel = "PromiseToPay",
                IsAutomatic = false,
                IsGrouped = false,
                NextActionDate = promiseToPayDate,
                Notes = promiseToPayDate.HasValue
                    ? $"Promesse de paiement au {promiseToPayDate:dd/MM/yyyy}{(string.IsNullOrWhiteSpace(notes) ? string.Empty : $" - {notes.Trim()}")}"
                    : notes?.Trim()
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureSalesDocumentAllowedAsync(Guid tenantId, Guid partnerId, CommercialDocumentType documentType, CancellationToken cancellationToken = default)
    {
        if (documentType is not CommercialDocumentType.SalesOrder and not CommercialDocumentType.DeliveryNote)
        {
            return;
        }

        var account = await GetCustomerAccountAsync(tenantId, partnerId, PaymentDirection.Incoming, cancellationToken);
        if (account is null)
        {
            throw new NotFoundException("CustomerAccount", partnerId);
        }

        List<string> reasons = [];
        if (documentType == CommercialDocumentType.SalesOrder)
        {
            if (!account.CanCreateSalesOrder && account.CreditLimitExceeded)
            {
                reasons.Add("plafond de credit depasse");
            }

            if (!account.CanCreateSalesOrder && account.OverdueDocumentCount > 0)
            {
                reasons.Add("presence d'impayes");
            }
        }

        if (documentType == CommercialDocumentType.DeliveryNote)
        {
            if (!account.CanCreateDelivery && account.CreditLimitExceeded)
            {
                reasons.Add("plafond de credit depasse");
            }

            if (!account.CanCreateDelivery && account.OverdueDocumentCount > 0)
            {
                reasons.Add("presence d'impayes");
            }
        }

        if (reasons.Count > 0)
        {
            throw new BusinessRuleException(
                $"Operation bloquee pour {account.PartnerCode} - {account.PartnerName} : {string.Join(", ", reasons)}.",
                errorCode: "CUSTOMER_ACCOUNT_BLOCKED");
        }
    }

    private async Task<Tenant> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
        ?? throw new NotFoundException(nameof(Tenant), tenantId);

    private static CustomerAccountStatus ResolveAccountStatus(bool blockSalesOrder, bool blockDelivery, bool creditLimitExceeded, bool hasOverdueDocuments)
    {
        if (blockSalesOrder)
        {
            return CustomerAccountStatus.BlockedForOrder;
        }

        if (blockDelivery)
        {
            return CustomerAccountStatus.BlockedForDelivery;
        }

        return creditLimitExceeded || hasOverdueDocuments
            ? CustomerAccountStatus.Watch
            : CustomerAccountStatus.Ok;
    }

    private static bool TryGetRecommendedReminder(OpenItemSummary item, Tenant tenant, out ReminderLevel level)
    {
        level = ReminderLevel.Friendly;
        if (item.OverdueDays < tenant.ReminderFriendlyDelayDays)
        {
            return false;
        }

        if (item.OverdueDays >= tenant.ReminderFinalNoticeDelayDays && item.LastReminderLevel != ReminderLevel.FinalNotice)
        {
            level = ReminderLevel.FinalNotice;
            return true;
        }

        if (item.OverdueDays >= tenant.ReminderFormalDelayDays &&
            item.LastReminderLevel is null or ReminderLevel.Friendly)
        {
            level = ReminderLevel.Formal;
            return true;
        }

        if (item.LastReminderLevel is null)
        {
            level = ReminderLevel.Friendly;
            return true;
        }

        return false;
    }

    private static ReminderLevel GetLastReminderLevelOrDefault(CommercialDocument document)
    {
        return document.ReminderLogs
            .OrderByDescending(x => x.SentOnUtc)
            .Select(x => x.ReminderLevel)
            .DefaultIfEmpty(ReminderLevel.Friendly)
            .First();
    }

    private async Task<CommercialDocument> LoadAllocatableDocumentAsync(Guid tenantId, Guid documentId, Guid partnerId, PaymentDirection direction, CancellationToken cancellationToken)
    {
        var document = await dbContext.CommercialDocuments
            .Include(x => x.PaymentAllocations)
            .FirstOrDefaultAsync(x => x.Id == documentId && x.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            throw new NotFoundException(nameof(CommercialDocument), documentId);
        }

        if (document.PartnerId != partnerId)
        {
            throw new BusinessRuleException(
                "Le reglement et la facture ne concernent pas le meme tiers.",
                errorCode: "PAYMENT_PARTNER_MISMATCH");
        }

        var expectedType = direction == PaymentDirection.Incoming
            ? CommercialDocumentType.SalesInvoice
            : CommercialDocumentType.PurchaseInvoice;

        if (document.DocumentType != expectedType)
        {
            throw new BusinessRuleException(
                "Le reglement et le document ne sont pas compatibles.",
                errorCode: "PAYMENT_DOCUMENT_INCOMPATIBLE");
        }

        return document;
    }

    private async Task<List<CommercialDocument>> LoadAllocatableDocumentsAsync(
        Guid tenantId,
        Guid partnerId,
        PaymentDirection direction,
        PaymentAllocationMode allocationMode,
        CancellationToken cancellationToken)
    {
        var expectedType = direction == PaymentDirection.Incoming
            ? CommercialDocumentType.SalesInvoice
            : CommercialDocumentType.PurchaseInvoice;

        var query = dbContext.CommercialDocuments
            .Include(x => x.PaymentAllocations)
            .Where(x =>
                x.TenantId == tenantId &&
                x.PartnerId == partnerId &&
                x.DocumentType == expectedType &&
                x.Status != CommercialDocumentStatus.Cancelled);

        query = allocationMode == PaymentAllocationMode.OldestDocumentDate
            ? query.OrderBy(x => x.DocumentDate).ThenBy(x => x.Number)
            : query.OrderBy(x => x.DueDate ?? x.DocumentDate).ThenBy(x => x.Number);

        return await query.ToListAsync(cancellationToken);
    }

    private static void AttachAllocation(Payment payment, CommercialDocument document, decimal amount, string? notes)
    {
        var allocation = new PaymentAllocation
        {
            Payment = payment,
            CommercialDocument = document,
            CommercialDocumentId = document.Id,
            AllocatedAmount = amount,
            AllocatedOnUtc = DateTime.UtcNow,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        payment.Allocations.Add(allocation);
        document.PaymentAllocations.Add(allocation);
        ApplyPaymentSnapshot(payment);
        ApplyDocumentSnapshot(document);
    }

    private async Task RefreshPaymentAndDocumentSnapshotsAsync(Guid paymentId, Guid documentId, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .Include(x => x.Allocations)
            .FirstAsync(x => x.Id == paymentId, cancellationToken);
        var document = await dbContext.CommercialDocuments
            .Include(x => x.PaymentAllocations)
            .FirstAsync(x => x.Id == documentId, cancellationToken);

        ApplyPaymentSnapshot(payment);
        ApplyDocumentSnapshot(document);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyPaymentSnapshot(Payment payment)
    {
        var allocated = payment.Allocations.Sum(x => x.AllocatedAmount);
        payment.AllocatedAmount = allocated;
        payment.AvailableAmount = payment.Amount - allocated;
        payment.AllocationStatus = payment.AvailableAmount <= 0m
            ? PaymentAllocationStatus.Allocated
            : allocated > 0m
                ? PaymentAllocationStatus.PartiallyAllocated
                : PaymentAllocationStatus.Unallocated;
    }

    private static void ApplyDocumentSnapshot(CommercialDocument document)
    {
        var paidAmount = document.PaymentAllocations.Sum(x => x.AllocatedAmount);
        var balance = document.TotalIncludingTax - paidAmount;

        document.PaidAmount = paidAmount;
        document.BalanceAmount = balance;

        if (document.InDispute)
        {
            document.PaymentStatus = CommercialPaymentStatus.InDispute;
            return;
        }

        if (document.PromiseToPayDate.HasValue)
        {
            document.PaymentStatus = CommercialPaymentStatus.PromiseToPay;
            return;
        }

        if (balance <= 0m)
        {
            document.PaymentStatus = CommercialPaymentStatus.Paid;
            document.Status = CommercialDocumentStatus.Completed;
            return;
        }

        if (paidAmount > 0m)
        {
            document.PaymentStatus = CommercialPaymentStatus.PartiallyPaid;
            document.Status = CommercialDocumentStatus.PartiallyProcessed;
            return;
        }

        if (document.DueDate is DateOnly dueDate && dueDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            document.PaymentStatus = CommercialPaymentStatus.Overdue;
            document.Status = CommercialDocumentStatus.Open;
            return;
        }

        document.PaymentStatus = CommercialPaymentStatus.Unpaid;
        if (document.Status == CommercialDocumentStatus.Completed)
        {
            document.Status = CommercialDocumentStatus.Open;
        }
    }

    private static TEnum ParseEnum<TEnum>(string rawValue, TEnum fallback)
        where TEnum : struct
        => Enum.TryParse<TEnum>(rawValue, true, out var parsed) ? parsed : fallback;

    private static bool ApplyChange<T>(T currentValue, T nextValue, Action<T> apply)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, nextValue))
        {
            return false;
        }

        apply(nextValue);
        return true;
    }
}
