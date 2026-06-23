namespace GescomSaas.Application.Models;

public sealed record OfflineSyncDashboard(
    Guid TenantId,
    string TenantName,
    string NodeMode,
    string DatabaseProvider,
    bool IsOfflineEnabled,
    bool IsManualTriggerRequired,
    bool CanPushToCentral,
    bool CanPullFromCentral,
    string LocalNodeId,
    string CentralBaseUrl,
    string DatabaseTarget,
    OfflineSyncStateSnapshot State,
    IReadOnlyList<string> LocalCapabilities,
    IReadOnlyList<string> CentralOnlyCapabilities,
    IReadOnlyList<OfflineSyncHistoryItem> History,
    IReadOnlyList<OfflineSyncConflictItem> Conflicts,
    IReadOnlyList<string> Warnings);

public sealed record OfflineSyncStateSnapshot(
    DateTime? LastPushRequestedOnUtc,
    DateTime? LastPullRequestedOnUtc,
    string LastPushStatus,
    string LastPullStatus,
    string LastMessage);

public sealed record OfflineSyncHistoryItem(
    DateTime OccurredOnUtc,
    string Direction,
    string Status,
    string TriggeredBy,
    string Message,
    IReadOnlyList<OfflineSyncModuleSummary> Modules,
    IReadOnlyList<string> Notes);

public sealed record OfflineSyncModuleSummary(
    string Name,
    string Status,
    string Summary,
    IReadOnlyList<string> Notes);

public sealed record OfflineSyncConflictItem(
    Guid Id,
    DateTime OccurredOnUtc,
    string Direction,
    string ModuleName,
    string Severity,
    string Status,
    string Summary,
    IReadOnlyList<string> Notes,
    string? ResolvedBy,
    DateTime? ResolvedOnUtc,
    string? ResolutionNote);

public sealed record OfflineSyncExecutionResult(
    bool Succeeded,
    string Direction,
    DateTime RequestedOnUtc,
    string Message);

public sealed record OfflineProductSyncItem(
    string Sku,
    string Label,
    string? Description,
    string ProductType,
    string UnitOfMeasure,
    bool TrackStock,
    string StockValuationMethod,
    string StockIdentityTrackingMode,
    bool IsActive,
    string? ProductCategoryCode,
    string? ProductCategoryLabel,
    string? TaxCodeCode,
    string? TaxCodeLabel,
    decimal? TaxRate,
    decimal PurchasePrice,
    decimal SalesPrice,
    DateTime? UpdatedOnUtc);

public sealed record OfflineProductPushRequest(
    Guid TenantId,
    string NodeId,
    IReadOnlyList<OfflineProductSyncItem> Products);

public sealed record OfflineProductPushResponse(
    int ReceivedCount,
    int CreatedCount,
    int UpdatedCount,
    int UnchangedCount,
    IReadOnlyList<string> Notes);

public sealed record OfflineProductPullResponse(
    Guid TenantId,
    string NodeId,
    DateTime GeneratedOnUtc,
    IReadOnlyList<OfflineProductSyncItem> Products);

public sealed record OfflineAddressSyncItem(
    string? Recipient,
    string? StreetLine1,
    string? StreetLine2,
    string? PostalCode,
    string? City,
    string? State,
    string? Country);

public sealed record OfflinePaymentTermSyncItem(
    string Code,
    string Label,
    int DueInDays);

public sealed record OfflineProductCategorySyncItem(
    string Code,
    string Label,
    string StockValuationMethod,
    string StockIdentityTrackingMode);

public sealed record OfflineTaxCodeSyncItem(
    string Code,
    string Label,
    decimal Rate);

public sealed record OfflineWarehouseSyncItem(
    string Code,
    string Label,
    bool IsDefault);

public sealed record OfflineBusinessPartnerSyncItem(
    string Code,
    string Name,
    string PartnerType,
    string? Email,
    string? PhoneNumber,
    string? VatNumber,
    decimal CreditLimit,
    bool IsActive,
    string? PaymentTermCode,
    OfflineAddressSyncItem BillingAddress,
    OfflineAddressSyncItem ShippingAddress,
    DateTime? UpdatedOnUtc);

public sealed record OfflineDocumentSequenceSyncItem(
    string DocumentType,
    string Mode,
    string Prefix,
    int NumberLength,
    int NextValue);

public sealed record OfflineReferenceNumberingSettingSyncItem(
    string Scope,
    string Mode,
    string Prefix,
    int NumberLength,
    int NextValue);

public sealed record OfflineJournalAccountSyncItem(
    string Code,
    string Label,
    string? CounterpartAccountCode);

public sealed record OfflineReferenceDataPullResponse(
    Guid TenantId,
    string NodeId,
    DateTime GeneratedOnUtc,
    IReadOnlyList<OfflinePaymentTermSyncItem> PaymentTerms,
    IReadOnlyList<OfflineProductCategorySyncItem> ProductCategories,
    IReadOnlyList<OfflineTaxCodeSyncItem> TaxCodes,
    IReadOnlyList<OfflineWarehouseSyncItem> Warehouses,
    IReadOnlyList<OfflineBusinessPartnerSyncItem> Partners,
    IReadOnlyList<OfflineDocumentSequenceSyncItem> DocumentSequences,
    IReadOnlyList<OfflineReferenceNumberingSettingSyncItem> ReferenceNumberingSettings,
    IReadOnlyList<OfflineJournalAccountSyncItem> JournalAccounts);

public sealed record OfflineCommercialDocumentLineSyncItem(
    string? ProductSku,
    string Description,
    decimal Quantity,
    decimal UnitPriceExcludingTax,
    decimal DiscountRate,
    decimal TaxRate,
    string? LotNumber,
    string? SerialNumber,
    DateOnly? ExpirationDate);

public sealed record OfflineCommercialDocumentSyncItem(
    string Number,
    string DocumentType,
    string Status,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    string CurrencyCode,
    string PartnerCode,
    string? WarehouseCode,
    string? Notes,
    string PaymentStatus,
    decimal PaidAmount,
    decimal BalanceAmount,
    bool InDispute,
    DateOnly? PromiseToPayDate,
    string? SourceDocumentNumber,
    IReadOnlyList<OfflineCommercialDocumentLineSyncItem> Lines,
    DateTime? UpdatedOnUtc);

public sealed record OfflineCommercialDocumentPushRequest(
    Guid TenantId,
    string NodeId,
    IReadOnlyList<OfflineCommercialDocumentSyncItem> Documents);

public sealed record OfflineCommercialDocumentPushResponse(
    int ReceivedCount,
    int CreatedCount,
    int UpdatedCount,
    int UnchangedCount,
    int SkippedCount,
    IReadOnlyList<string> Notes);

public sealed record OfflineCommercialDocumentPullResponse(
    Guid TenantId,
    string NodeId,
    DateTime GeneratedOnUtc,
    IReadOnlyList<OfflineCommercialDocumentSyncItem> Documents);

public sealed record OfflineStockDocumentLineSyncItem(
    string? ProductSku,
    string Description,
    decimal Quantity,
    decimal UnitCost,
    string? LotNumber,
    string? SerialNumber,
    DateOnly? ExpirationDate);

public sealed record OfflineStockDocumentSyncItem(
    string Number,
    string DocumentType,
    string Status,
    DateOnly DocumentDate,
    string? SourceWarehouseCode,
    string? DestinationWarehouseCode,
    string? Notes,
    DateTime? PostedOnUtc,
    IReadOnlyList<OfflineStockDocumentLineSyncItem> Lines,
    DateTime? UpdatedOnUtc);

public sealed record OfflineStockDocumentPushRequest(
    Guid TenantId,
    string NodeId,
    IReadOnlyList<OfflineStockDocumentSyncItem> Documents);

public sealed record OfflineStockDocumentPushResponse(
    int ReceivedCount,
    int CreatedCount,
    int UpdatedCount,
    int PostedCount,
    int UnchangedCount,
    int SkippedCount,
    IReadOnlyList<string> Notes);

public sealed record OfflineStockDocumentPullResponse(
    Guid TenantId,
    string NodeId,
    DateTime GeneratedOnUtc,
    IReadOnlyList<OfflineStockDocumentSyncItem> Documents);

public sealed record OfflinePaymentAllocationSyncItem(
    string DocumentNumber,
    decimal AllocatedAmount,
    DateTime AllocatedOnUtc,
    string? Notes);

public sealed record OfflinePaymentSyncItem(
    DateOnly PaymentDate,
    string Direction,
    string Type,
    string Method,
    string AllocationStatus,
    string ReferenceNumber,
    string CurrencyCode,
    decimal Amount,
    decimal AllocatedAmount,
    decimal AvailableAmount,
    string? Notes,
    string PartnerCode,
    string? SourceDocumentNumber,
    IReadOnlyList<OfflinePaymentAllocationSyncItem> Allocations,
    DateTime? UpdatedOnUtc);

public sealed record OfflinePaymentPushRequest(
    Guid TenantId,
    string NodeId,
    IReadOnlyList<OfflinePaymentSyncItem> Payments);

public sealed record OfflinePaymentPushResponse(
    int ReceivedCount,
    int CreatedCount,
    int UpdatedCount,
    int UnchangedCount,
    int AllocationRefreshCount,
    int SkippedCount,
    IReadOnlyList<string> Notes);

public sealed record OfflinePaymentPullResponse(
    Guid TenantId,
    string NodeId,
    DateTime GeneratedOnUtc,
    IReadOnlyList<OfflinePaymentSyncItem> Payments);

public sealed record OfflinePaymentApplyResult(Guid PaymentId, bool Created, bool Updated);

public sealed record OfflineNodeBootstrapRequest(
    string TenantSlug,
    string AdminEmail,
    string AdminPassword,
    string? AdminFirstName,
    string? AdminLastName);

public sealed record OfflineNodeBootstrapResult(
    bool Succeeded,
    string Message,
    Guid? TenantId,
    string? TenantName,
    string? LocalNodeId,
    string? AdminEmail);

public sealed record OfflineTenantBootstrapPackage(
    Guid TenantId,
    string TenantSlug,
    string TenantName,
    string CompanyLegalName,
    string PrimaryContactEmail,
    string PhoneNumber,
    string AddressLine1,
    string AddressLine2,
    string PostalCode,
    string City,
    string State,
    string CountryCode,
    string CurrencyCode,
    string CashCurrencyCode,
    string CurrencySymbol,
    string CurrencySymbolPosition,
    string MoneyDecimalSeparator,
    string MoneyGroupSeparator,
    int MoneyDecimalPlaces,
    string QuantityDecimalSeparator,
    string QuantityGroupSeparator,
    int QuantityDecimalPlaces,
    string PaymentMethodsJson,
    string PartnerLookupMode,
    string IncomingPaymentAllocationMode,
    int ReminderFriendlyDelayDays,
    int ReminderFormalDelayDays,
    int ReminderFinalNoticeDelayDays,
    bool BlockSalesOrdersOnCreditLimit,
    bool BlockSalesOrdersOnOverdue,
    bool BlockDeliveriesOnCreditLimit,
    bool BlockDeliveriesOnOverdue,
    bool AllowNegativeStock,
    string DefaultStockValuationMethod,
    string VisualTheme,
    bool IsActive,
    DateTime GeneratedOnUtc,
    string NodeId);
