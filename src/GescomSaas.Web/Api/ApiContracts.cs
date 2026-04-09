using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Api;

public sealed record ApiLoginRequest(string Email, string Password, bool UseCookies = false);

public sealed record ApiContextResponse(
    string UserId,
    string Email,
    string DisplayName,
    Guid TenantId,
    string TenantName,
    string CurrencyCode,
    IReadOnlyList<string> Roles,
    IReadOnlyList<ApiQuotaResponse> Quotas,
    int ExceededQuotaCount);

public sealed record ApiQuotaResponse(
    string Label,
    int Used,
    int Limit,
    int Remaining,
    bool IsExceeded);

public sealed record ApiAddressRequest(
    string? Recipient,
    string? StreetLine1,
    string? StreetLine2,
    string? PostalCode,
    string? City,
    string? State,
    string? Country)
{
    public Address ToEntity() =>
        new()
        {
            Recipient = Recipient?.Trim(),
            StreetLine1 = StreetLine1?.Trim(),
            StreetLine2 = StreetLine2?.Trim(),
            PostalCode = PostalCode?.Trim(),
            City = City?.Trim(),
            State = State?.Trim(),
            Country = Country?.Trim()
        };
}

public sealed record ApiAddressResponse(
    string? Recipient,
    string? StreetLine1,
    string? StreetLine2,
    string? PostalCode,
    string? City,
    string? State,
    string? Country);

public sealed record ApiPartnerRequest(
    string Code,
    string Name,
    BusinessPartnerType PartnerType,
    string? Email,
    string? PhoneNumber,
    string? VatNumber,
    decimal CreditLimit,
    bool IsActive,
    Guid? PaymentTermId,
    ApiAddressRequest BillingAddress,
    ApiAddressRequest ShippingAddress);

public sealed record ApiPartnerResponse(
    Guid Id,
    string Code,
    string Name,
    BusinessPartnerType PartnerType,
    string? Email,
    string? PhoneNumber,
    string? VatNumber,
    decimal CreditLimit,
    bool IsActive,
    Guid? PaymentTermId,
    string? PaymentTermLabel,
    ApiAddressResponse BillingAddress,
    ApiAddressResponse ShippingAddress);

public sealed record ApiProductRequest(
    string Sku,
    string Label,
    string? Description,
    ProductType ProductType,
    string UnitOfMeasure,
    bool TrackStock,
    bool IsActive,
    Guid? ProductCategoryId,
    Guid? TaxCodeId,
    decimal PurchasePrice,
    decimal SalesPrice);

public sealed record ApiProductResponse(
    Guid Id,
    string Sku,
    string Label,
    string? Description,
    ProductType ProductType,
    string UnitOfMeasure,
    bool TrackStock,
    bool IsActive,
    Guid? ProductCategoryId,
    string? ProductCategoryLabel,
    Guid? TaxCodeId,
    string? TaxCodeLabel,
    decimal PurchasePrice,
    decimal SalesPrice);

public sealed record ApiWarehouseResponse(Guid Id, string Code, string Label, bool IsDefault);

public sealed record ApiDocumentLineRequest(
    Guid? ProductId,
    string Description,
    decimal Quantity,
    decimal UnitPriceExcludingTax,
    decimal DiscountRate,
    decimal TaxRate);

public sealed record ApiDocumentCreateRequest(
    CommercialDocumentType DocumentType,
    Guid PartnerId,
    Guid? WarehouseId,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    string? CurrencyCode,
    CommercialDocumentStatus Status,
    string? Notes,
    IReadOnlyList<ApiDocumentLineRequest> Lines);

public sealed record ApiDocumentUpdateRequest(
    Guid PartnerId,
    Guid? WarehouseId,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    string? CurrencyCode,
    CommercialDocumentStatus Status,
    string? Notes,
    IReadOnlyList<ApiDocumentLineRequest> Lines);

public sealed record ApiDocumentTransformRequest(CommercialDocumentType TargetDocumentType);

public sealed record ApiDocumentListItem(
    Guid Id,
    string Number,
    CommercialDocumentType DocumentType,
    CommercialDocumentStatus Status,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    string PartnerName,
    string? SourceNumber,
    decimal TotalIncludingTax,
    string CurrencyCode);

public sealed record ApiDocumentLineResponse(
    Guid Id,
    Guid? ProductId,
    string? ProductCode,
    string Description,
    decimal Quantity,
    decimal UnitPriceExcludingTax,
    decimal DiscountRate,
    decimal TaxRate,
    decimal LineTotalExcludingTax,
    decimal LineTaxAmount,
    decimal LineTotalIncludingTax);

public sealed record ApiDocumentResponse(
    Guid Id,
    string Number,
    CommercialDocumentType DocumentType,
    CommercialDocumentStatus Status,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    string CurrencyCode,
    Guid PartnerId,
    string PartnerName,
    Guid? WarehouseId,
    string? WarehouseLabel,
    Guid? SourceDocumentId,
    string? SourceDocumentNumber,
    string? Notes,
    decimal TotalExcludingTax,
    decimal TotalTax,
    decimal TotalIncludingTax,
    IReadOnlyList<ApiDocumentLineResponse> Lines);

public sealed record ApiPaymentRequest(
    Guid DocumentId,
    DateOnly PaymentDate,
    decimal Amount,
    PaymentMethod Method,
    string? ReferenceNumber,
    string? Notes);

public sealed record ApiStockAdjustmentRequest(
    Guid ProductId,
    Guid WarehouseId,
    DateOnly MovementDate,
    StockMovementType MovementType,
    decimal Quantity,
    decimal UnitCost,
    string? ReferenceNumber);
