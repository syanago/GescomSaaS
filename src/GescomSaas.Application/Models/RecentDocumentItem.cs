using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record RecentDocumentItem(
    string Number,
    CommercialDocumentType DocumentType,
    string PartnerName,
    DateOnly DocumentDate,
    CommercialDocumentStatus Status,
    decimal TotalIncludingTax,
    string CurrencyCode);
