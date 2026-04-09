using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
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
                PartnerName = x.Partner != null ? x.Partner.Name : "-",
                x.DocumentDate,
                x.DueDate,
                x.CurrencyCode,
                x.TotalIncludingTax,
                PaidAmount = x.PaymentAllocations.Sum(a => a.AllocatedAmount),
                x.Status,
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
                var overdueDays = x.DueDate.HasValue && balance > 0m && x.DueDate.Value < now
                    ? now.DayNumber - x.DueDate.Value.DayNumber
                    : 0;

                return new OpenItemSummary(
                    x.Id,
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
                x.Partner != null ? x.Partner.Name : "-",
                x.Direction,
                x.Method,
                x.CurrencyCode,
                x.Amount,
                x.Allocations.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task RegisterPaymentAsync(Guid tenantId, PaymentRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.CommercialDocuments
            .Include(x => x.Partner)
            .Include(x => x.PaymentAllocations)
            .FirstOrDefaultAsync(x => x.Id == request.DocumentId && x.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            throw new InvalidOperationException("Document de reglement introuvable.");
        }

        var direction = document.DocumentType switch
        {
            CommercialDocumentType.SalesInvoice => PaymentDirection.Incoming,
            CommercialDocumentType.PurchaseInvoice => PaymentDirection.Outgoing,
            _ => throw new InvalidOperationException("Les reglements ne sont autorises que sur les factures.")
        };

        if (request.Amount <= 0m)
        {
            throw new InvalidOperationException("Le montant du reglement doit etre strictement positif.");
        }

        var alreadyPaid = document.PaymentAllocations.Sum(x => x.AllocatedAmount);
        var balance = document.TotalIncludingTax - alreadyPaid;
        if (request.Amount > balance)
        {
            throw new InvalidOperationException("Le montant du reglement depasse le solde restant du document.");
        }

        var payment = new Payment
        {
            TenantId = tenantId,
            PaymentDate = request.PaymentDate,
            Direction = direction,
            Method = request.Method,
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber)
                ? $"REG-{DateTime.UtcNow:yyyyMMddHHmmss}"
                : request.ReferenceNumber.Trim(),
            CurrencyCode = document.CurrencyCode,
            Amount = request.Amount,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            PartnerId = document.PartnerId,
            Allocations =
            [
                new PaymentAllocation
                {
                    CommercialDocumentId = document.Id,
                    AllocatedAmount = request.Amount
                }
            ]
        };

        dbContext.Payments.Add(payment);

        var newBalance = balance - request.Amount;
        document.Status = newBalance <= 0m
            ? CommercialDocumentStatus.Completed
            : CommercialDocumentStatus.PartiallyProcessed;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RegisterReminderAsync(Guid tenantId, Guid documentId, ReminderLevel level, string? notes, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.CommercialDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == documentId &&
                     x.TenantId == tenantId &&
                     x.DocumentType == CommercialDocumentType.SalesInvoice,
                cancellationToken);

        if (document is null)
        {
            throw new InvalidOperationException("La relance n'est disponible que sur une facture client.");
        }

        dbContext.ReminderLogs.Add(new ReminderLog
        {
            TenantId = tenantId,
            CommercialDocumentId = documentId,
            ReminderLevel = level,
            Channel = "Manual",
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
