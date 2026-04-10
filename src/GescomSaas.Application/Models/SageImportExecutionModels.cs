using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record SageImportExecutionRequest(
    Guid TenantId,
    bool DryRun,
    string SourceServer,
    string SourceDatabase,
    ExternalSqlAuthenticationMode AuthenticationMode,
    string SourceUserName,
    string SourcePassword,
    SageImportMode ImportMode,
    SageImportScopeSelection Scope,
    SageImportFilterSelection Filters,
    SageImportMappingSelection Mapping,
    SageImportExecutionSelection Execution);

public sealed record SageImportScopeSelection(
    bool ImportCustomers,
    bool ImportSuppliers,
    bool ImportProducts,
    bool ImportProductCategories,
    bool ImportTaxCodes,
    bool ImportPaymentTerms,
    bool ImportPriceLists,
    bool ImportWarehouses,
    bool ImportOpeningStock,
    bool ImportSalesDocuments,
    bool ImportPurchaseDocuments,
    bool ImportOpenBalances);

public sealed record SageImportFilterSelection(
    DateTime? DateFrom,
    DateTime? DateTo,
    string CustomerCodeFrom,
    string CustomerCodeTo,
    string SupplierCodeFrom,
    string SupplierCodeTo,
    string ProductCodeFrom,
    string ProductCodeTo,
    string IncludedDocumentTypes,
    string IncludedWarehouses,
    string IncludedFamilies,
    SageImportDocumentTypeSelection DocumentTypes,
    bool ExcludeClosedDocuments,
    bool ImportOnlyActiveRecords);

public sealed record SageImportDocumentTypeSelection(
    bool SalesQuote,
    bool SalesOrder,
    bool DeliveryNote,
    bool SalesInvoice,
    bool SalesCreditNote,
    bool PurchaseRequest,
    bool PurchaseOrder,
    bool GoodsReceipt,
    bool PurchaseInvoice,
    bool SupplierCreditNote);

public sealed record SageImportMappingSelection(
    string CustomerPrefix,
    string SupplierPrefix,
    string ProductPrefix,
    string WarehouseFallbackCode,
    string DefaultSalesTaxCode,
    string DefaultPurchaseTaxCode,
    string DefaultPaymentTermCode,
    SageExistingRecordPolicy ExistingRecordPolicy,
    SageMissingReferencePolicy MissingReferencePolicy,
    SageDocumentNumberPolicy DocumentNumberPolicy,
    SageImportSchemaMappingSelection SchemaMapping);

public sealed record SageImportExecutionSelection(
    bool StopOnFirstError,
    bool UseStagingArea,
    bool RecalculateTotalsInLigCom,
    bool PreserveSageDocumentDates,
    bool CreateActivityJournal);

public sealed record SageImportSchemaMappingSelection(
    SageImportModuleMappingSelection Partners,
    SageImportModuleMappingSelection ProductCategories,
    SageImportModuleMappingSelection TaxCodes,
    SageImportModuleMappingSelection PaymentTerms,
    SageImportModuleMappingSelection Warehouses,
    SageImportModuleMappingSelection Products,
    SageImportModuleMappingSelection PriceLists,
    SageImportModuleMappingSelection Stock,
    SageImportModuleMappingSelection DocumentHeaders,
    SageImportModuleMappingSelection DocumentLines);

public sealed record SageImportModuleMappingSelection(
    string TableName,
    string FieldMap);

public sealed record SageImportExecutionReport(
    bool Success,
    bool DryRun,
    string SourceServer,
    string SourceDatabase,
    int TotalImported,
    int TotalUpdated,
    int TotalSkipped,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<SageImportModuleReport> Modules);

public sealed record SageImportModuleReport(
    string ModuleName,
    string Status,
    string SourceTable,
    int Imported,
    int Updated,
    int Skipped,
    string Summary,
    IReadOnlyList<string> Notes);
