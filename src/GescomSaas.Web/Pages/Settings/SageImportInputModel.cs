using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.Settings;

public class SageImportInputModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions PortableJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [Display(Name = "Activer le profil d'import Sage SQL")]
    public bool SageImportEnabled { get; set; }

    [Display(Name = "Serveur SQL Sage")]
    public string SageSqlServerName { get; set; } = string.Empty;

    [Display(Name = "Base source Sage")]
    public string SageSqlDatabaseName { get; set; } = string.Empty;

    [Display(Name = "Societe / dossier Sage")]
    public string SageCompanyCode { get; set; } = string.Empty;

    [Display(Name = "Authentification source")]
    public ExternalSqlAuthenticationMode SageSqlAuthenticationMode { get; set; } = ExternalSqlAuthenticationMode.Windows;

    [Display(Name = "Utilisateur SQL")]
    public string SageSqlUserName { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe SQL")]
    public string SageSqlPassword { get; set; } = string.Empty;

    [Display(Name = "Mode de transfert")]
    public SageImportMode SageImportMode { get; set; } = SageImportMode.Partial;

    public SageImportScopeOptions Scope { get; set; } = new();
    public SageImportFilterOptions Filters { get; set; } = new();
    public SageImportMappingOptions Mapping { get; set; } = new();
    public SageImportExecutionOptions Execution { get; set; } = new();
    public SageImportSchemaMappingOptions SchemaMapping { get; set; } = new();

    public static SageImportInputModel FromEntity(Tenant tenant)
    {
        var mappingEnvelope = DeserializeOrDefault<SageImportMappingEnvelope>(tenant.SageImportMappingJson);

        return new SageImportInputModel
        {
            SageImportEnabled = tenant.SageImportEnabled,
            SageSqlServerName = tenant.SageSqlServerName,
            SageSqlDatabaseName = tenant.SageSqlDatabaseName,
            SageCompanyCode = tenant.SageCompanyCode,
            SageSqlAuthenticationMode = tenant.SageSqlAuthenticationMode,
            SageSqlUserName = tenant.SageSqlUserName,
            SageSqlPassword = tenant.SageSqlPassword,
            SageImportMode = tenant.SageImportMode,
            Scope = DeserializeOrDefault<SageImportScopeOptions>(tenant.SageImportScopeJson),
            Filters = DeserializeOrDefault<SageImportFilterOptions>(tenant.SageImportFilterJson),
            Mapping = new SageImportMappingOptions
            {
                CustomerPrefix = mappingEnvelope.CustomerPrefix,
                SupplierPrefix = mappingEnvelope.SupplierPrefix,
                ProductPrefix = mappingEnvelope.ProductPrefix,
                WarehouseFallbackCode = mappingEnvelope.WarehouseFallbackCode,
                DefaultSalesTaxCode = mappingEnvelope.DefaultSalesTaxCode,
                DefaultPurchaseTaxCode = mappingEnvelope.DefaultPurchaseTaxCode,
                DefaultPaymentTermCode = mappingEnvelope.DefaultPaymentTermCode,
                ExistingRecordPolicy = mappingEnvelope.ExistingRecordPolicy,
                MissingReferencePolicy = mappingEnvelope.MissingReferencePolicy,
                DocumentNumberPolicy = mappingEnvelope.DocumentNumberPolicy
            },
            Execution = new SageImportExecutionOptions
            {
                DryRunOnly = mappingEnvelope.DryRunOnly,
                StopOnFirstError = mappingEnvelope.StopOnFirstError,
                UseStagingArea = mappingEnvelope.UseStagingArea,
                RecalculateTotalsInLigCom = mappingEnvelope.RecalculateTotalsInLigCom,
                PreserveSageDocumentDates = mappingEnvelope.PreserveSageDocumentDates,
                CreateActivityJournal = mappingEnvelope.CreateActivityJournal
            },
            SchemaMapping = mappingEnvelope.SchemaMapping ?? new SageImportSchemaMappingOptions()
        };
    }

    public void ApplyTo(Tenant tenant)
    {
        tenant.SageImportEnabled = SageImportEnabled;
        tenant.SageSqlServerName = SageSqlServerName.Trim();
        tenant.SageSqlDatabaseName = SageSqlDatabaseName.Trim();
        tenant.SageCompanyCode = SageCompanyCode.Trim();
        tenant.SageSqlAuthenticationMode = SageSqlAuthenticationMode;
        tenant.SageSqlUserName = SageSqlUserName.Trim();
        tenant.SageSqlPassword = SageSqlPassword.Trim();
        tenant.SageImportMode = SageImportMode;
        tenant.SageImportScopeJson = JsonSerializer.Serialize(Scope, JsonOptions);
        tenant.SageImportFilterJson = JsonSerializer.Serialize(Filters, JsonOptions);
        tenant.SageImportMappingJson = JsonSerializer.Serialize(new SageImportMappingEnvelope
        {
            CustomerPrefix = Mapping.CustomerPrefix.Trim(),
            SupplierPrefix = Mapping.SupplierPrefix.Trim(),
            ProductPrefix = Mapping.ProductPrefix.Trim(),
            WarehouseFallbackCode = Mapping.WarehouseFallbackCode.Trim(),
            DefaultSalesTaxCode = Mapping.DefaultSalesTaxCode.Trim(),
            DefaultPurchaseTaxCode = Mapping.DefaultPurchaseTaxCode.Trim(),
            DefaultPaymentTermCode = Mapping.DefaultPaymentTermCode.Trim(),
            ExistingRecordPolicy = Mapping.ExistingRecordPolicy,
            MissingReferencePolicy = Mapping.MissingReferencePolicy,
            DocumentNumberPolicy = Mapping.DocumentNumberPolicy,
            DryRunOnly = Execution.DryRunOnly,
            StopOnFirstError = Execution.StopOnFirstError,
            UseStagingArea = Execution.UseStagingArea,
            RecalculateTotalsInLigCom = Execution.RecalculateTotalsInLigCom,
            PreserveSageDocumentDates = Execution.PreserveSageDocumentDates,
            CreateActivityJournal = Execution.CreateActivityJournal,
            SchemaMapping = SchemaMapping
        }, JsonOptions);
    }

    public IReadOnlyList<string> SelectedModules()
    {
        var items = new List<string>();
        Add(items, Scope.ImportCustomers, "Clients");
        Add(items, Scope.ImportSuppliers, "Fournisseurs");
        Add(items, Scope.ImportProducts, "Articles");
        Add(items, Scope.ImportProductCategories, "Familles");
        Add(items, Scope.ImportTaxCodes, "Taxes");
        Add(items, Scope.ImportPaymentTerms, "Conditions de paiement");
        Add(items, Scope.ImportPriceLists, "Listes de prix");
        Add(items, Scope.ImportWarehouses, "Depots");
        Add(items, Scope.ImportOpeningStock, "Stock initial");
        Add(items, Scope.ImportSalesDocuments, "Documents de vente");
        Add(items, Scope.ImportPurchaseDocuments, "Documents d'achat");
        Add(items, Scope.ImportOpenBalances, "Encours et soldes ouverts");
        return items;
    }

    public IReadOnlyList<string> ActiveFilters()
    {
        var items = new List<string>();
        Add(items, Filters.DateFrom.HasValue || Filters.DateTo.HasValue, BuildDateFilterLabel());
        Add(items, !string.IsNullOrWhiteSpace(Filters.CustomerCodeFrom) || !string.IsNullOrWhiteSpace(Filters.CustomerCodeTo), BuildRangeLabel("Clients", Filters.CustomerCodeFrom, Filters.CustomerCodeTo));
        Add(items, !string.IsNullOrWhiteSpace(Filters.SupplierCodeFrom) || !string.IsNullOrWhiteSpace(Filters.SupplierCodeTo), BuildRangeLabel("Fournisseurs", Filters.SupplierCodeFrom, Filters.SupplierCodeTo));
        Add(items, !string.IsNullOrWhiteSpace(Filters.ProductCodeFrom) || !string.IsNullOrWhiteSpace(Filters.ProductCodeTo), BuildRangeLabel("Articles", Filters.ProductCodeFrom, Filters.ProductCodeTo));
        Add(items, !string.IsNullOrWhiteSpace(Filters.IncludedWarehouses), $"Depots cibles : {Filters.IncludedWarehouses.Trim()}");
        Add(items, !string.IsNullOrWhiteSpace(Filters.IncludedFamilies), $"Familles cibles : {Filters.IncludedFamilies.Trim()}");
        Add(items, !string.IsNullOrWhiteSpace(Filters.IncludedDocumentTypes), $"Types de pieces : {Filters.IncludedDocumentTypes.Trim()}");
        Add(items, Filters.DocumentTypes.SelectedTypes().Any(), $"Types LigCom : {string.Join(", ", Filters.DocumentTypes.SelectedTypes())}");
        Add(items, Filters.ExcludeClosedDocuments, "Pieces cloturees exclues");
        Add(items, Filters.ImportOnlyActiveRecords, "Fiches actives uniquement");
        return items;
    }

    public IReadOnlyList<string> ExecutionHighlights()
    {
        var items = new List<string>
        {
            $"Mode {FormatImportMode(SageImportMode)}",
            $"Existants : {FormatExistingRecordPolicy(Mapping.ExistingRecordPolicy)}",
            $"References manquantes : {FormatMissingReferencePolicy(Mapping.MissingReferencePolicy)}",
            $"Numerotation : {FormatDocumentNumberPolicy(Mapping.DocumentNumberPolicy)}"
        };

        Add(items, Execution.DryRunOnly, "Simulation sans ecriture");
        Add(items, Execution.UseStagingArea, "Passage prealable par zone tampon");
        Add(items, Execution.StopOnFirstError, "Arret au premier ecart");
        Add(items, Execution.RecalculateTotalsInLigCom, "Recalcul des totaux dans LigCom");
        Add(items, Execution.CreateActivityJournal, "Journal d'import a generer");
        return items;
    }

    public string SuggestedConnectionString() =>
        SageSqlAuthenticationMode == ExternalSqlAuthenticationMode.Windows
            ? $"Server={SageSqlServerName};Database={SageSqlDatabaseName};Trusted_Connection=True;TrustServerCertificate=True"
            : $"Server={SageSqlServerName};Database={SageSqlDatabaseName};User ID={SageSqlUserName};Password=********;TrustServerCertificate=True";

    public string ToPortableJson()
    {
        return JsonSerializer.Serialize(this, PortableJsonOptions);
    }

    public static bool TryParsePortableJson(string json, out SageImportInputModel model, out string error)
    {
        model = new SageImportInputModel();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Le contenu JSON est vide.";
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<SageImportInputModel>(json, PortableJsonOptions);
            if (payload is null)
            {
                error = "Le JSON ne contient aucun profil exploitable.";
                return false;
            }

            model = payload;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private string BuildDateFilterLabel()
    {
        var fromLabel = Filters.DateFrom?.ToString("dd/MM/yyyy") ?? "...";
        var toLabel = Filters.DateTo?.ToString("dd/MM/yyyy") ?? "...";
        return $"Periode : {fromLabel} -> {toLabel}";
    }

    private static string BuildRangeLabel(string label, string from, string to)
    {
        var start = string.IsNullOrWhiteSpace(from) ? "..." : from.Trim();
        var end = string.IsNullOrWhiteSpace(to) ? "..." : to.Trim();
        return $"{label} : {start} -> {end}";
    }

    private static string FormatImportMode(SageImportMode mode) => mode switch
    {
        SageImportMode.Complete => "complet",
        SageImportMode.Partial => "partiel cible",
        SageImportMode.Delta => "delta incremental",
        _ => mode.ToString()
    };

    private static string FormatExistingRecordPolicy(SageExistingRecordPolicy mode) => mode switch
    {
        SageExistingRecordPolicy.UpdateByCode => "mise a jour par code",
        SageExistingRecordPolicy.SkipExisting => "ignorer les existants",
        SageExistingRecordPolicy.PrefixAndCreate => "dupliquer avec prefixe",
        _ => mode.ToString()
    };

    private static string FormatMissingReferencePolicy(SageMissingReferencePolicy mode) => mode switch
    {
        SageMissingReferencePolicy.CreateMissing => "creer ce qui manque",
        SageMissingReferencePolicy.SkipDependentRecords => "ignorer les enregistrements dependants",
        SageMissingReferencePolicy.BlockTransfer => "bloquer le transfert",
        _ => mode.ToString()
    };

    private static string FormatDocumentNumberPolicy(SageDocumentNumberPolicy mode) => mode switch
    {
        SageDocumentNumberPolicy.PreserveSourceNumber => "conserver Sage",
        SageDocumentNumberPolicy.RenumberInLigCom => "renumeroter LigCom",
        SageDocumentNumberPolicy.PreserveWithPrefixIfDuplicate => "conserver sinon prefixer",
        _ => mode.ToString()
    };

    private static T DeserializeOrDefault<T>(string? json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new T();
        }

        try
        {
            var payload = JsonSerializer.Deserialize<T>(json, JsonOptions);
            if (payload is not null)
            {
                return payload;
            }
        }
        catch
        {
        }

        return new T();
    }

    private static void Add(List<string> target, bool condition, string? value)
    {
        if (condition && !string.IsNullOrWhiteSpace(value))
        {
            target.Add(value);
        }
    }
}

public class SageImportScopeOptions
{
    public bool ImportCustomers { get; set; } = true;
    public bool ImportSuppliers { get; set; } = true;
    public bool ImportProducts { get; set; } = true;
    public bool ImportProductCategories { get; set; } = true;
    public bool ImportTaxCodes { get; set; } = true;
    public bool ImportPaymentTerms { get; set; } = true;
    public bool ImportPriceLists { get; set; }
    public bool ImportWarehouses { get; set; } = true;
    public bool ImportOpeningStock { get; set; }
    public bool ImportSalesDocuments { get; set; }
    public bool ImportPurchaseDocuments { get; set; }
    public bool ImportOpenBalances { get; set; }
}

public class SageImportFilterOptions
{
    [Display(Name = "Date source debut")]
    [DataType(DataType.Date)]
    public DateTime? DateFrom { get; set; }

    [Display(Name = "Date source fin")]
    [DataType(DataType.Date)]
    public DateTime? DateTo { get; set; }

    [Display(Name = "Code client debut")]
    public string CustomerCodeFrom { get; set; } = string.Empty;

    [Display(Name = "Code client fin")]
    public string CustomerCodeTo { get; set; } = string.Empty;

    [Display(Name = "Code fournisseur debut")]
    public string SupplierCodeFrom { get; set; } = string.Empty;

    [Display(Name = "Code fournisseur fin")]
    public string SupplierCodeTo { get; set; } = string.Empty;

    [Display(Name = "Code article debut")]
    public string ProductCodeFrom { get; set; } = string.Empty;

    [Display(Name = "Code article fin")]
    public string ProductCodeTo { get; set; } = string.Empty;

    [Display(Name = "Types de pieces inclus")]
    public string IncludedDocumentTypes { get; set; } = string.Empty;

    public SageImportDocumentTypeOptions DocumentTypes { get; set; } = new();

    [Display(Name = "Depots cibles")]
    public string IncludedWarehouses { get; set; } = string.Empty;

    [Display(Name = "Familles cibles")]
    public string IncludedFamilies { get; set; } = string.Empty;

    [Display(Name = "Ignorer les pieces cloturees")]
    public bool ExcludeClosedDocuments { get; set; } = true;

    [Display(Name = "Importer seulement les fiches actives")]
    public bool ImportOnlyActiveRecords { get; set; } = true;
}

public class SageImportDocumentTypeOptions
{
    [Display(Name = "Devis client")]
    public bool SalesQuote { get; set; } = true;

    [Display(Name = "Commande client")]
    public bool SalesOrder { get; set; } = true;

    [Display(Name = "Bon de livraison")]
    public bool DeliveryNote { get; set; } = true;

    [Display(Name = "Facture client")]
    public bool SalesInvoice { get; set; } = true;

    [Display(Name = "Avoir client")]
    public bool SalesCreditNote { get; set; } = true;

    [Display(Name = "Demande d'achat")]
    public bool PurchaseRequest { get; set; } = true;

    [Display(Name = "Commande fournisseur")]
    public bool PurchaseOrder { get; set; } = true;

    [Display(Name = "Reception")]
    public bool GoodsReceipt { get; set; } = true;

    [Display(Name = "Facture fournisseur")]
    public bool PurchaseInvoice { get; set; } = true;

    [Display(Name = "Avoir fournisseur")]
    public bool SupplierCreditNote { get; set; } = true;

    public IReadOnlyList<string> SelectedTypes()
    {
        var items = new List<string>();
        Add(items, SalesQuote, "Devis");
        Add(items, SalesOrder, "Commande client");
        Add(items, DeliveryNote, "BL");
        Add(items, SalesInvoice, "Facture client");
        Add(items, SalesCreditNote, "Avoir client");
        Add(items, PurchaseRequest, "Demande achat");
        Add(items, PurchaseOrder, "Commande fournisseur");
        Add(items, GoodsReceipt, "Reception");
        Add(items, PurchaseInvoice, "Facture fournisseur");
        Add(items, SupplierCreditNote, "Avoir fournisseur");
        return items;
    }

    private static void Add(List<string> target, bool condition, string value)
    {
        if (condition)
        {
            target.Add(value);
        }
    }
}

public class SageImportMappingOptions
{
    [Display(Name = "Prefixe clients")]
    public string CustomerPrefix { get; set; } = string.Empty;

    [Display(Name = "Prefixe fournisseurs")]
    public string SupplierPrefix { get; set; } = string.Empty;

    [Display(Name = "Prefixe articles")]
    public string ProductPrefix { get; set; } = string.Empty;

    [Display(Name = "Depot de repli LigCom")]
    public string WarehouseFallbackCode { get; set; } = string.Empty;

    [Display(Name = "Taxe vente par defaut")]
    public string DefaultSalesTaxCode { get; set; } = string.Empty;

    [Display(Name = "Taxe achat par defaut")]
    public string DefaultPurchaseTaxCode { get; set; } = string.Empty;

    [Display(Name = "Condition de paiement par defaut")]
    public string DefaultPaymentTermCode { get; set; } = string.Empty;

    [Display(Name = "Traitement des fiches existantes")]
    public SageExistingRecordPolicy ExistingRecordPolicy { get; set; } = SageExistingRecordPolicy.UpdateByCode;

    [Display(Name = "References manquantes")]
    public SageMissingReferencePolicy MissingReferencePolicy { get; set; } = SageMissingReferencePolicy.CreateMissing;

    [Display(Name = "Numerotation des pieces")]
    public SageDocumentNumberPolicy DocumentNumberPolicy { get; set; } = SageDocumentNumberPolicy.PreserveWithPrefixIfDuplicate;
}

public class SageImportExecutionOptions
{
    [Display(Name = "Simulation sans ecriture")]
    public bool DryRunOnly { get; set; } = true;

    [Display(Name = "Arreter au premier ecart")]
    public bool StopOnFirstError { get; set; }

    [Display(Name = "Passer par une zone tampon")]
    public bool UseStagingArea { get; set; } = true;

    [Display(Name = "Recalculer les totaux dans LigCom")]
    public bool RecalculateTotalsInLigCom { get; set; } = true;

    [Display(Name = "Conserver les dates document Sage")]
    public bool PreserveSageDocumentDates { get; set; } = true;

    [Display(Name = "Generer un journal de transfert")]
    public bool CreateActivityJournal { get; set; } = true;
}

public class SageImportMappingEnvelope : SageImportMappingOptions
{
    public bool DryRunOnly { get; set; } = true;
    public bool StopOnFirstError { get; set; }
    public bool UseStagingArea { get; set; } = true;
    public bool RecalculateTotalsInLigCom { get; set; } = true;
    public bool PreserveSageDocumentDates { get; set; } = true;
    public bool CreateActivityJournal { get; set; } = true;
    public SageImportSchemaMappingOptions? SchemaMapping { get; set; }
}
