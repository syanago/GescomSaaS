using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record PlatformInvoiceSummaryItem(
    Guid InvoiceId,
    string InvoiceNumber,
    string TenantName,
    DateOnly IssueDate,
    DateOnly DueDate,
    PlatformInvoiceStatus Status,
    decimal TotalIncludingTax,
    string CurrencyCode);
