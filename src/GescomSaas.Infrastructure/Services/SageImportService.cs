using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Infrastructure.Services;

public class SageImportService(ApplicationDbContext dbContext) : ISageImportService
{
    public async Task<SageImportExecutionReport> ExecuteAsync(SageImportExecutionRequest request, CancellationToken cancellationToken = default)
    {
        List<string> warnings = [];
        List<SageImportModuleReport> moduleReports = [];
        var state = await ImportState.CreateAsync(dbContext, request.TenantId, cancellationToken);

        if (request.Execution.UseStagingArea)
        {
            warnings.Add("La zone tampon est pour l'instant logique : l'import s'exécute en direct mais le rapport conserve les écarts détectés.");
        }

        await using var sourceConnection = new SqlConnection(BuildConnectionString(request));
        await sourceConnection.OpenAsync(cancellationToken);
        var schema = await ReadSchemaAsync(sourceConnection, cancellationToken);

        await using var transaction = request.DryRun ? null : await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var success = true;

        success &= await RunModuleAsync(() => ImportPartnersAsync(sourceConnection, schema, request, state, cancellationToken), moduleReports, warnings, request.Execution.StopOnFirstError, () => request.Scope.ImportCustomers || request.Scope.ImportSuppliers);
        success &= await RunModuleAsync(() => ImportCategoriesAsync(sourceConnection, schema, request, state, cancellationToken), moduleReports, warnings, request.Execution.StopOnFirstError, () => request.Scope.ImportProductCategories);
        success &= await RunModuleAsync(() => ImportTaxCodesAsync(sourceConnection, schema, request, state, cancellationToken), moduleReports, warnings, request.Execution.StopOnFirstError, () => request.Scope.ImportTaxCodes);
        success &= await RunModuleAsync(() => ImportPaymentTermsAsync(sourceConnection, schema, request, state, cancellationToken), moduleReports, warnings, request.Execution.StopOnFirstError, () => request.Scope.ImportPaymentTerms);
        success &= await RunModuleAsync(() => ImportJournalAccountsAsync(sourceConnection, schema, request, state, cancellationToken), moduleReports, warnings, request.Execution.StopOnFirstError, () => true);
        success &= await RunModuleAsync(() => ImportWarehousesAsync(sourceConnection, schema, request, state, cancellationToken), moduleReports, warnings, request.Execution.StopOnFirstError, () => request.Scope.ImportWarehouses);
        success &= await RunModuleAsync(() => ImportProductsAsync(sourceConnection, schema, request, state, cancellationToken), moduleReports, warnings, request.Execution.StopOnFirstError, () => request.Scope.ImportProducts);
        success &= await RunModuleAsync(() => ImportPriceListsAsync(sourceConnection, schema, request, state, cancellationToken), moduleReports, warnings, request.Execution.StopOnFirstError, () => request.Scope.ImportPriceLists);

        success &= await RunModuleAsync(() => ImportOpeningStockAsync(sourceConnection, schema, request, state, cancellationToken), moduleReports, warnings, request.Execution.StopOnFirstError, () => request.Scope.ImportOpeningStock);
        success &= await RunModuleAsync(() => ImportCommercialDocumentsAsync(sourceConnection, schema, request, state, isPurchase: false, cancellationToken), moduleReports, warnings, request.Execution.StopOnFirstError, () => request.Scope.ImportSalesDocuments);
        success &= await RunModuleAsync(() => ImportCommercialDocumentsAsync(sourceConnection, schema, request, state, isPurchase: true, cancellationToken), moduleReports, warnings, request.Execution.StopOnFirstError, () => request.Scope.ImportPurchaseDocuments);

        success &= await RunModuleAsync(() => ImportOpenBalancesAsync(sourceConnection, schema, request, state, cancellationToken), moduleReports, warnings, request.Execution.StopOnFirstError, () => request.Scope.ImportOpenBalances);

        if (request.DryRun)
        {
            foreach (var entry in dbContext.ChangeTracker.Entries().ToArray())
            {
                entry.State = EntityState.Detached;
            }

            dbContext.SageImportRuns.Add(BuildRunEntity(request, success, moduleReports, warnings));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            dbContext.SageImportRuns.Add(BuildRunEntity(request, success, moduleReports, warnings));
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }

        return new SageImportExecutionReport(
            success,
            request.DryRun,
            request.SourceServer,
            request.SourceDatabase,
            moduleReports.Sum(x => x.Imported),
            moduleReports.Sum(x => x.Updated),
            moduleReports.Sum(x => x.Skipped),
            warnings,
            moduleReports);
    }

    private static Domain.Entities.SaaS.SageImportRun BuildRunEntity(
        SageImportExecutionRequest request,
        bool success,
        IReadOnlyList<SageImportModuleReport> moduleReports,
        IReadOnlyList<string> warnings)
    {
        return new Domain.Entities.SaaS.SageImportRun
        {
            TenantId = request.TenantId,
            IsDryRun = request.DryRun,
            IsSuccessful = success,
            SourceServer = request.SourceServer,
            SourceDatabase = request.SourceDatabase,
            ImportMode = request.ImportMode,
            TotalImported = moduleReports.Sum(x => x.Imported),
            TotalUpdated = moduleReports.Sum(x => x.Updated),
            TotalSkipped = moduleReports.Sum(x => x.Skipped),
            WarningSummary = string.Join(" | ", warnings.Take(10)),
            Modules = moduleReports.Select(x => new Domain.Entities.SaaS.SageImportRunModule
            {
                ModuleName = x.ModuleName,
                Status = x.Status,
                SourceTable = x.SourceTable,
                Imported = x.Imported,
                Updated = x.Updated,
                Skipped = x.Skipped,
                Summary = x.Summary,
                NoteSummary = string.Join(" | ", x.Notes.Take(10))
            }).ToList()
        };
    }

    private static async Task<bool> RunModuleAsync(
        Func<Task<SageImportModuleReport>> action,
        List<SageImportModuleReport> reports,
        List<string> warnings,
        bool stopOnFirstError,
        Func<bool> shouldRun)
    {
        if (!shouldRun())
        {
            return true;
        }

        try
        {
            reports.Add(await action());
            return true;
        }
        catch (Exception ex)
        {
            reports.Add(new SageImportModuleReport("Module", "Erreur", "-", 0, 0, 0, ex.Message, [ex.Message]));
            warnings.Add(ex.Message);
            if (stopOnFirstError)
            {
                throw;
            }
            return false;
        }
    }

    private static void AddDeferredReport(List<SageImportModuleReport> reports, bool selected, string moduleName)
    {
        if (!selected)
        {
            return;
        }

        reports.Add(new SageImportModuleReport(
            moduleName,
            "Differe",
            "-",
            0,
            0,
            0,
            "Le module est pris en compte dans le profil mais son moteur d'import detaille n'est pas encore active dans cette premiere version.",
            ["Module prevu pour la prochaine etape du moteur Sage vers LigCom."]));
    }

    private async Task<SageImportModuleReport> ImportPartnersAsync(
        SqlConnection sourceConnection,
        IReadOnlyList<SourceTableInfo> schema,
        SageImportExecutionRequest request,
        ImportState state,
        CancellationToken cancellationToken)
    {
        var moduleMap = ParseMapping(request.Mapping.SchemaMapping.Partners);
        var candidate = FindBestTable(schema,
            ["F_COMPTET", "CLIENT", "CLIENTS", "FOURNISSEUR", "FOURNISSEURS", "F_CLIENT", "F_FOURNISSEUR"],
            ["CT_NUM", "CT_INTITULE", "CT_EMAIL", "CT_TELEPHONE"],
            request.Mapping.SchemaMapping.Partners.TableName,
            moduleMap.Values);

        if (candidate is null)
        {
            return MissingModule("Tiers", "Aucune table tiers evidente n'a ete detectee.");
        }

        var rows = await ReadRowsAsync(sourceConnection, candidate.TableName, cancellationToken);
        var imported = 0;
        var updated = 0;
        var skipped = 0;
        List<string> notes = [];
        var bothTypes = request.Scope.ImportCustomers && request.Scope.ImportSuppliers;
        var targetType = bothTypes ? BusinessPartnerType.Both : request.Scope.ImportSuppliers ? BusinessPartnerType.Supplier : BusinessPartnerType.Customer;

        foreach (var row in rows)
        {
            var sourceCode = GetMappedString(row, moduleMap, "Code", "CT_NUM", "CODE", "CODETIERS", "NUMERO", "ID");
            var sourceName = GetMappedString(row, moduleMap, "Label", "CT_INTITULE", "INTITULE", "LIBELLE", "NOM", "RAISONSOCIALE");
            if (string.IsNullOrWhiteSpace(sourceCode) || string.IsNullOrWhiteSpace(sourceName))
            {
                skipped++;
                continue;
            }

            if (request.Filters.ImportOnlyActiveRecords && IsInactive(row))
            {
                skipped++;
                continue;
            }

            if (state.Partners.TryGetValue(sourceCode, out var existing))
            {
                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.SkipExisting)
                {
                    skipped++;
                    continue;
                }

                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.PrefixAndCreate)
                {
                    var prefixedCode = EnsureUniqueCode(state.PartnerCodes, BuildPrefixedCode(sourceCode, targetType == BusinessPartnerType.Supplier ? request.Mapping.SupplierPrefix : request.Mapping.CustomerPrefix));
                    var entity = BuildPartner(prefixedCode, sourceName, targetType, row, request.TenantId, existing.PaymentTermId);
                    dbContext.BusinessPartners.Add(entity);
                    state.Partners[prefixedCode] = entity;
                    imported++;
                    continue;
                }

                ApplyPartner(existing, sourceName, targetType, row);
                updated++;
                continue;
            }

            var paymentTermId = ResolvePaymentTerm(state, GetMappedString(row, moduleMap, "PaymentTerm", "RG_CODE", "PAYMENTTERM", "CODE_REGLEMENT"), request);
            var partner = BuildPartner(sourceCode, sourceName, targetType, row, request.TenantId, paymentTermId);
            dbContext.BusinessPartners.Add(partner);
            state.Partners[sourceCode] = partner;
            state.PartnerCodes.Add(sourceCode);
            imported++;
        }

        notes.Add(bothTypes
            ? "Inference LigCom : la table tiers detectee sert aux clients et fournisseurs. Les fiches importees sont marquees `Both`."
            : "Les tiers ont ete classes selon le perimetre demande dans le profil.");

        return new SageImportModuleReport("Tiers", "Traite", candidate.TableName, imported, updated, skipped, "Import des clients et fournisseurs depuis la table tiers detectee.", notes);
    }

    private async Task<SageImportModuleReport> ImportCategoriesAsync(SqlConnection sourceConnection, IReadOnlyList<SourceTableInfo> schema, SageImportExecutionRequest request, ImportState state, CancellationToken cancellationToken)
    {
        var moduleMap = ParseMapping(request.Mapping.SchemaMapping.ProductCategories);
        var candidate = FindBestTable(schema, ["F_FAMILLE", "FAMILLE", "F_ARTFAM"], ["FA_CODEFAMILLE", "FA_INTITULE", "FA_LIBELLE"], request.Mapping.SchemaMapping.ProductCategories.TableName, moduleMap.Values);
        if (candidate is null)
        {
            return MissingModule("Familles", "Aucune table famille article evidente n'a ete detectee.");
        }

        var rows = await ReadRowsAsync(sourceConnection, candidate.TableName, cancellationToken);
        var imported = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var row in rows)
        {
            var code = GetMappedString(row, moduleMap, "Code", "FA_CODEFAMILLE", "CODE", "FAMILLE", "FA_CODE");
            var label = GetMappedString(row, moduleMap, "Label", "FA_INTITULE", "FA_LIBELLE", "INTITULE", "LIBELLE");
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(label))
            {
                skipped++;
                continue;
            }

            if (state.Categories.TryGetValue(code, out var existing))
            {
                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.SkipExisting)
                {
                    skipped++;
                    continue;
                }

                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.PrefixAndCreate)
                {
                    code = EnsureUniqueCode(state.CategoryCodes, BuildPrefixedCode(code, request.Mapping.ProductPrefix));
                    var created = new ProductCategory { TenantId = request.TenantId, Code = code, Label = label };
                    dbContext.ProductCategories.Add(created);
                    state.Categories[code] = created;
                    imported++;
                    continue;
                }

                existing.Label = label;
                updated++;
                continue;
            }

            var category = new ProductCategory { TenantId = request.TenantId, Code = code, Label = label };
            dbContext.ProductCategories.Add(category);
            state.Categories[code] = category;
            state.CategoryCodes.Add(code);
            imported++;
        }

        return new SageImportModuleReport("Familles", "Traite", candidate.TableName, imported, updated, skipped, "Familles articles preparees pour LigCom.", []);
    }

    private async Task<SageImportModuleReport> ImportTaxCodesAsync(SqlConnection sourceConnection, IReadOnlyList<SourceTableInfo> schema, SageImportExecutionRequest request, ImportState state, CancellationToken cancellationToken)
    {
        var moduleMap = ParseMapping(request.Mapping.SchemaMapping.TaxCodes);
        var candidate = FindBestTable(schema, ["F_TAXE", "TAXE", "TAXES", "TAUXTVA"], ["TA_CODE", "TA_TAUX", "TA_INTITULE"], request.Mapping.SchemaMapping.TaxCodes.TableName, moduleMap.Values);
        if (candidate is null)
        {
            return MissingModule("Taxes", "Aucune table taxe evidente n'a ete detectee.");
        }

        var rows = await ReadRowsAsync(sourceConnection, candidate.TableName, cancellationToken);
        var imported = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var row in rows)
        {
            var code = GetMappedString(row, moduleMap, "Code", "TA_CODE", "CODE", "TAXE");
            var label = GetMappedString(row, moduleMap, "Label", "TA_INTITULE", "LIBELLE", "INTITULE");
            var rate = GetMappedDecimal(row, moduleMap, "Rate", "TA_TAUX", "TAUX", "RATE");
            if (string.IsNullOrWhiteSpace(code))
            {
                skipped++;
                continue;
            }

            label = string.IsNullOrWhiteSpace(label) ? code : label;
            if (state.TaxCodes.TryGetValue(code, out var existing))
            {
                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.SkipExisting)
                {
                    skipped++;
                    continue;
                }

                existing.Label = label;
                existing.Rate = rate;
                updated++;
                continue;
            }

            var taxCode = new TaxCode { TenantId = request.TenantId, Code = code, Label = label, Rate = rate };
            dbContext.TaxCodes.Add(taxCode);
            state.TaxCodes[code] = taxCode;
            imported++;
        }

        return new SageImportModuleReport("Taxes", "Traite", candidate.TableName, imported, updated, skipped, "Taxes detectees et preparees pour LigCom.", []);
    }

    private async Task<SageImportModuleReport> ImportPaymentTermsAsync(SqlConnection sourceConnection, IReadOnlyList<SourceTableInfo> schema, SageImportExecutionRequest request, ImportState state, CancellationToken cancellationToken)
    {
        var moduleMap = ParseMapping(request.Mapping.SchemaMapping.PaymentTerms);
        var candidate = FindBestTable(schema, ["F_REGLEMENT", "P_REGLEMENT", "REGLEMENT"], ["RG_CODE", "RG_LIBELLE", "RG_NBJOUR"], request.Mapping.SchemaMapping.PaymentTerms.TableName, moduleMap.Values);
        if (candidate is null)
        {
            return MissingModule("Conditions de paiement", "Aucune table de reglement evidente n'a ete detectee.");
        }

        var rows = await ReadRowsAsync(sourceConnection, candidate.TableName, cancellationToken);
        var imported = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var row in rows)
        {
            var code = GetMappedString(row, moduleMap, "Code", "RG_CODE", "CODE", "REGLEMENT");
            var label = GetMappedString(row, moduleMap, "Label", "RG_LIBELLE", "LIBELLE", "INTITULE");
            var dueInDays = GetMappedInt(row, moduleMap, "DueInDays", "RG_NBJOUR", "NBJOUR", "DUEINDAYS");
            if (string.IsNullOrWhiteSpace(code))
            {
                skipped++;
                continue;
            }

            label = string.IsNullOrWhiteSpace(label) ? code : label;
            if (state.PaymentTerms.TryGetValue(code, out var existing))
            {
                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.SkipExisting)
                {
                    skipped++;
                    continue;
                }

                existing.Label = label;
                existing.DueInDays = dueInDays;
                updated++;
                continue;
            }

            var paymentTerm = new PaymentTerm { TenantId = request.TenantId, Code = code, Label = label, DueInDays = dueInDays };
            dbContext.PaymentTerms.Add(paymentTerm);
            state.PaymentTerms[code] = paymentTerm;
            imported++;
        }

        return new SageImportModuleReport("Conditions de paiement", "Traite", candidate.TableName, imported, updated, skipped, "Conditions de paiement preparees pour LigCom.", []);
    }

    private async Task<SageImportModuleReport> ImportJournalAccountsAsync(
        SqlConnection sourceConnection,
        IReadOnlyList<SourceTableInfo> schema,
        SageImportExecutionRequest request,
        ImportState state,
        CancellationToken cancellationToken)
    {
        var candidate = FindBestTable(
            schema,
            ["F_JOURNAUX", "JOURNAUX", "JOURNAL", "F_JOURNAL"],
            ["JO_NUM", "JO_INTITULE", "CG_NUM", "JO_TYPE", "JO_Contrepartie"]);

        if (candidate is null)
        {
            return MissingModule("Comptes journaux", "Aucune table journaux evidente n'a ete detectee.");
        }

        var rows = await ReadRowsAsync(sourceConnection, candidate.TableName, cancellationToken);
        var imported = 0;
        var updated = 0;
        var skipped = 0;
        List<string> notes = [];

        foreach (var row in rows)
        {
            var code = GetString(row, "JO_NUM", "CODE", "JOURNAL", "CODEJOURNAL");
            if (string.IsNullOrWhiteSpace(code))
            {
                skipped++;
                notes.Add("Journal ignore : code journal absent dans la source Sage.");
                continue;
            }

            var label = GetFirstNonEmpty(
                GetString(row, "JO_INTITULE", "INTITULE", "LIBELLE", "LABEL"),
                code) ?? code;

            var journalType = GetInt(row, "JO_TYPE", "TYPEJOURNAL", "TYPE", "NATURE");
            var journalKind = GetFirstNonEmpty(
                GetString(row, "JO_TYPE", "JO_NATURE", "TYPE", "NATURE", "JOURNALTYPE", "TYPEJOURNAL"),
                label,
                code);

            if (!IsTreasuryJournal(journalType, code, label, journalKind))
            {
                skipped++;
                notes.Add($"Journal `{code}` ignore : type Sage `{journalType}` hors tresorerie.");
                continue;
            }

            var counterpartAccountCode = GetFirstNonEmpty(
                GetString(row, "CG_NUM", "CG_NUMCONTREPARTIE", "CG_NUMCP", "COMPTECONTREPARTIE", "JO_COMPTE", "JO_COMPTECP", "COMPTE", "COMPTEGENERAL", "COMPTECONTREPARTIECAISSE", "COMPTECONTREPARTIEBANQUE"));

            if (state.JournalAccounts.TryGetValue(code, out var existing))
            {
                var labelChanged = !string.Equals(existing.Label, label, StringComparison.Ordinal);
                var counterpartChanged = false;
                if (!string.IsNullOrWhiteSpace(counterpartAccountCode)
                    && !string.Equals(existing.CounterpartAccountCode, counterpartAccountCode, StringComparison.OrdinalIgnoreCase))
                {
                    counterpartChanged = true;
                }

                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.SkipExisting)
                {
                    if (counterpartChanged)
                    {
                        existing.CounterpartAccountCode = counterpartAccountCode;
                        updated++;
                        notes.Add($"Journal `{code}` existant : contrepartie synchronisee vers `{counterpartAccountCode}`.");
                    }
                    else
                    {
                        skipped++;
                        notes.Add(string.IsNullOrWhiteSpace(counterpartAccountCode)
                            ? $"Journal `{code}` ignore : aucune contrepartie exploitable n'a ete trouvee cote Sage."
                            : $"Journal `{code}` ignore : contrepartie deja a jour (`{existing.CounterpartAccountCode ?? "-"}`).");
                    }

                    continue;
                }

                if (!labelChanged && !counterpartChanged)
                {
                    skipped++;
                    notes.Add(string.IsNullOrWhiteSpace(counterpartAccountCode)
                        ? $"Journal `{code}` inchange : aucune contrepartie exploitable n'a ete trouvee cote Sage."
                        : $"Journal `{code}` inchange : contrepartie deja a jour (`{existing.CounterpartAccountCode ?? counterpartAccountCode}`).");
                    continue;
                }

                existing.Label = label;
                if (!string.IsNullOrWhiteSpace(counterpartAccountCode))
                {
                    existing.CounterpartAccountCode = counterpartAccountCode;
                }

                updated++;
                notes.Add(counterpartChanged
                    ? $"Journal `{code}` mis a jour : contrepartie `{counterpartAccountCode}`."
                    : $"Journal `{code}` mis a jour : libelle synchronise.");
                continue;
            }

            if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.PrefixAndCreate)
            {
                code = EnsureUniqueCode(state.JournalAccountCodes, BuildPrefixedCode(code, request.Mapping.ProductPrefix));
            }

            var journalAccount = new JournalAccount
            {
                TenantId = request.TenantId,
                Code = code,
                Label = label,
                CounterpartAccountCode = counterpartAccountCode
            };

            dbContext.JournalAccounts.Add(journalAccount);
            state.JournalAccounts[code] = journalAccount;
            state.JournalAccountCodes.Add(code);
            imported++;
            notes.Add(string.IsNullOrWhiteSpace(counterpartAccountCode)
                ? $"Journal `{code}` importe sans contrepartie source exploitable."
                : $"Journal `{code}` importe avec contrepartie `{counterpartAccountCode}`.");
        }

        return new SageImportModuleReport(
            "Journaux de tresorerie",
            "Traite",
            candidate.TableName,
            imported,
            updated,
            skipped,
            "Les journaux de tresorerie Sage (`JO_Type = 2`) sont repris avec synchronisation des comptes de contrepartie lus dans `CG_Num` quand un code existe deja dans LigCom.",
            notes.Distinct().Take(20).ToArray());
    }

    private async Task<SageImportModuleReport> ImportWarehousesAsync(SqlConnection sourceConnection, IReadOnlyList<SourceTableInfo> schema, SageImportExecutionRequest request, ImportState state, CancellationToken cancellationToken)
    {
        var moduleMap = ParseMapping(request.Mapping.SchemaMapping.Warehouses);
        var candidate = FindBestTable(schema, ["F_DEPOT", "DEPOT", "DEPOTS", "MAGASIN"], ["DE_NO", "DE_INTITULE", "DE_LIBELLE"], request.Mapping.SchemaMapping.Warehouses.TableName, moduleMap.Values);
        if (candidate is null)
        {
            return MissingModule("Depots", "Aucune table depot evidente n'a ete detectee.");
        }

        var rows = await ReadRowsAsync(sourceConnection, candidate.TableName, cancellationToken);
        var imported = 0;
        var updated = 0;
        var skipped = 0;
        var isFirst = state.Warehouses.Count == 0;

        foreach (var row in rows)
        {
            var code = GetMappedString(row, moduleMap, "Code", "DE_NO", "CODE", "DEPOT");
            var label = GetMappedString(row, moduleMap, "Label", "DE_INTITULE", "DE_LIBELLE", "INTITULE", "LIBELLE");
            if (string.IsNullOrWhiteSpace(code))
            {
                skipped++;
                continue;
            }

            label = string.IsNullOrWhiteSpace(label) ? code : label;
            if (state.Warehouses.TryGetValue(code, out var existing))
            {
                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.SkipExisting)
                {
                    skipped++;
                    continue;
                }

                existing.Label = label;
                updated++;
                continue;
            }

            var warehouse = new Warehouse { TenantId = request.TenantId, Code = code, Label = label, IsDefault = isFirst && imported == 0 };
            dbContext.Warehouses.Add(warehouse);
            state.Warehouses[code] = warehouse;
            imported++;
        }

        return new SageImportModuleReport("Depots", "Traite", candidate.TableName, imported, updated, skipped, "Depots prepares pour LigCom.", []);
    }

    private async Task<SageImportModuleReport> ImportProductsAsync(SqlConnection sourceConnection, IReadOnlyList<SourceTableInfo> schema, SageImportExecutionRequest request, ImportState state, CancellationToken cancellationToken)
    {
        var moduleMap = ParseMapping(request.Mapping.SchemaMapping.Products);
        var candidate = FindBestTable(schema, ["F_ARTICLE", "ARTICLE", "ARTICLES"], ["AR_REF", "AR_DESIGN", "AR_PRIXVEN", "AR_PRIXACH"], request.Mapping.SchemaMapping.Products.TableName, moduleMap.Values);
        if (candidate is null)
        {
            return MissingModule("Articles", "Aucune table article evidente n'a ete detectee.");
        }

        var rows = await ReadRowsAsync(sourceConnection, candidate.TableName, cancellationToken);
        var imported = 0;
        var updated = 0;
        var skipped = 0;
        List<string> notes = [];

        foreach (var row in rows)
        {
            var sourceCode = GetMappedString(row, moduleMap, "Code", "AR_REF", "CODE", "ARTICLE", "SKU");
            var label = GetMappedString(row, moduleMap, "Label", "AR_DESIGN", "LIBELLE", "INTITULE", "DESIGNATION");
            if (string.IsNullOrWhiteSpace(sourceCode) || string.IsNullOrWhiteSpace(label))
            {
                skipped++;
                continue;
            }

            if (!InRange(sourceCode, request.Filters.ProductCodeFrom, request.Filters.ProductCodeTo))
            {
                skipped++;
                continue;
            }

            if (request.Filters.ImportOnlyActiveRecords && IsInactive(row))
            {
                skipped++;
                continue;
            }

            var targetCode = sourceCode;
            if (state.Products.TryGetValue(sourceCode, out var existing))
            {
                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.SkipExisting)
                {
                    skipped++;
                    continue;
                }

                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.PrefixAndCreate)
                {
                    targetCode = EnsureUniqueCode(state.ProductCodes, BuildPrefixedCode(sourceCode, request.Mapping.ProductPrefix));
                }
                else
                {
                    ApplyProduct(existing, label, row, ResolveCategoryId(state, GetString(row, "FA_CODEFAMILLE", "FAMILLE"), request, notes), ResolveTaxCodeId(state, GetString(row, "TA_CODE", "TAXE"), request, notes));
                    updated++;
                    state.ProductCodeMap[sourceCode] = existing.Sku;
                    continue;
                }
            }

            var categoryId = ResolveCategoryId(state, GetMappedString(row, moduleMap, "CategoryCode", "FA_CODEFAMILLE", "FAMILLE"), request, notes);
            var taxId = ResolveTaxCodeId(state, GetMappedString(row, moduleMap, "TaxCode", "TA_CODE", "TAXE"), request, notes);
            var product = new Product
            {
                TenantId = request.TenantId,
                Sku = targetCode,
                Label = label,
                Description = GetMappedString(row, moduleMap, "Description", "AR_DESIGN2", "DESCRIPTION", "DESCRIPTIF"),
                PurchasePrice = GetMappedDecimal(row, moduleMap, "PurchasePrice", "AR_PRIXACH", "PRIXACHAT", "PA_HT"),
                SalesPrice = GetMappedDecimal(row, moduleMap, "SalesPrice", "AR_PRIXVEN", "PRIXVENTE", "PV_HT"),
                UnitOfMeasure = EmptyAsDefault(GetMappedString(row, moduleMap, "Unit", "AR_UNITEVEN", "UNITE", "UN"), "UN", 10),
                ProductType = InferProductType(row),
                TrackStock = InferTrackStock(row),
                IsActive = !IsInactive(row),
                ProductCategoryId = categoryId,
                TaxCodeId = taxId
            };

            dbContext.Products.Add(product);
            state.Products[targetCode] = product;
            state.ProductCodes.Add(targetCode);
            state.ProductCodeMap[sourceCode] = targetCode;
            imported++;
        }

        return new SageImportModuleReport("Articles", "Traite", candidate.TableName, imported, updated, skipped, "Articles repris depuis la base Sage avec resolution des familles et taxes.", notes.Distinct().ToArray());
    }

    private async Task<SageImportModuleReport> ImportPriceListsAsync(SqlConnection sourceConnection, IReadOnlyList<SourceTableInfo> schema, SageImportExecutionRequest request, ImportState state, CancellationToken cancellationToken)
    {
        var moduleMap = ParseMapping(request.Mapping.SchemaMapping.PriceLists);
        var candidate = FindBestTable(schema, ["F_TARIF", "TARIF", "TARIFS", "ARTTARIF"], ["AR_REF", "PRIX", "TARIF"], request.Mapping.SchemaMapping.PriceLists.TableName, moduleMap.Values);
        if (candidate is null)
        {
            return MissingModule("Listes de prix", "Aucune table tarif evidente n'a ete detectee.");
        }

        var rows = await ReadRowsAsync(sourceConnection, candidate.TableName, cancellationToken);
        var imported = 0;
        var updated = 0;
        var skipped = 0;
        List<string> notes = [];
        const string fallbackPriceListCode = "SAGE-IMPORT";
        var priceListCode = GetFirstNonEmpty(rows.Select(x => GetMappedString(x, moduleMap, "Code", "TL_CODE", "TA_CODE", "TARIF", "CODETARIF")).ToArray()) ?? fallbackPriceListCode;
        var priceListLabel = GetFirstNonEmpty(rows.Select(x => GetMappedString(x, moduleMap, "Label", "TL_LIBELLE", "TA_LIBELLE", "LIBELLE")).ToArray()) ?? "Tarif importe Sage";

        if (!state.PriceLists.TryGetValue(priceListCode, out var priceList))
        {
            priceList = new PriceList
            {
                TenantId = request.TenantId,
                Code = priceListCode,
                Label = priceListLabel,
                CurrencyCode = "CAD",
                IsDefault = state.PriceLists.Count == 0
            };
            dbContext.PriceLists.Add(priceList);
            state.PriceLists[priceListCode] = priceList;
            imported++;
        }

        foreach (var row in rows)
        {
            var sourceProductCode = GetMappedString(row, moduleMap, "ProductCode", "AR_REF", "ARTICLE", "CODEARTICLE");
            if (string.IsNullOrWhiteSpace(sourceProductCode))
            {
                skipped++;
                continue;
            }

            var targetProductCode = ResolveTargetProductCode(state, sourceProductCode, request.Mapping.ProductPrefix);
            if (string.IsNullOrWhiteSpace(targetProductCode) || !state.Products.TryGetValue(targetProductCode, out var product))
            {
                skipped++;
                notes.Add($"Article `{sourceProductCode}` introuvable dans LigCom pour la ligne tarif.");
                continue;
            }

            var unitPrice = GetMappedDecimal(row, moduleMap, "UnitPrice", "PRIX", "TARIF", "PU", "PV_HT");
            var line = priceList.Lines.FirstOrDefault(x => x.ProductId == product.Id);
            if (line is null)
            {
                line = new PriceListLine
                {
                    PriceList = priceList,
                    Product = product,
                    ProductId = product.Id,
                    UnitPrice = unitPrice
                };
                dbContext.PriceListLines.Add(line);
                imported++;
            }
            else
            {
                line.UnitPrice = unitPrice;
                updated++;
            }
        }

        return new SageImportModuleReport("Listes de prix", "Traite", candidate.TableName, imported, updated, skipped, "Tarif source detecte et aligne sur les articles importes.", notes.Distinct().ToArray());
    }

    private async Task<SageImportModuleReport> ImportCommercialDocumentsAsync(
        SqlConnection sourceConnection,
        IReadOnlyList<SourceTableInfo> schema,
        SageImportExecutionRequest request,
        ImportState state,
        bool isPurchase,
        CancellationToken cancellationToken)
    {
        var headerMap = ParseMapping(request.Mapping.SchemaMapping.DocumentHeaders);
        var lineMap = ParseMapping(request.Mapping.SchemaMapping.DocumentLines);
        var headerTable = FindBestTable(schema, ["F_DOCENTETE", "DOCENTETE", "F_DOCUMENT", "DOCUMENT"], ["DO_PIECE", "DO_DATE", "DO_TIERS", "DO_TYPE"], request.Mapping.SchemaMapping.DocumentHeaders.TableName, headerMap.Values);
        var lineTable = FindBestTable(schema, ["F_DOCLIGNE", "DOCLIGNE", "F_DOCLIG", "DOCUMENTLIGNE"], ["DO_PIECE", "AR_REF", "DL_QTE", "DL_PUHT"], request.Mapping.SchemaMapping.DocumentLines.TableName, lineMap.Values);

        if (headerTable is null || lineTable is null)
        {
            return MissingModule(isPurchase ? "Documents d'achat" : "Documents de vente", "Les tables d'entete et de lignes documentaires n'ont pas ete detectees de maniere fiable.");
        }

        var headerRows = await ReadRowsAsync(sourceConnection, headerTable.TableName, cancellationToken);
        var lineRows = await ReadRowsAsync(sourceConnection, lineTable.TableName, cancellationToken);
        var linesByPiece = lineRows
            .GroupBy(row => GetString(row, "DO_PIECE", "PIECE", "DOCNUM", "NUMERO"))
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var imported = 0;
        var updated = 0;
        var skipped = 0;
        List<string> notes = [];
        var selectedTypes = GetSelectedDocumentTypes(request.Filters.DocumentTypes, isPurchase);

        foreach (var header in headerRows)
        {
            var sourceNumber = GetMappedString(header, headerMap, "Number", "DO_PIECE", "PIECE", "DOCNUM", "NUMERO");
            if (string.IsNullOrWhiteSpace(sourceNumber))
            {
                skipped++;
                continue;
            }

            var documentDate = GetMappedDate(header, headerMap, "Date", "DO_DATE", "DATE", "DATEDOC");
            if (documentDate.HasValue)
            {
                if (request.Filters.DateFrom.HasValue && documentDate.Value < DateOnly.FromDateTime(request.Filters.DateFrom.Value))
                {
                    skipped++;
                    continue;
                }

                if (request.Filters.DateTo.HasValue && documentDate.Value > DateOnly.FromDateTime(request.Filters.DateTo.Value))
                {
                    skipped++;
                    continue;
                }
            }

            var rawType = GetMappedString(header, headerMap, "Type", "DO_TYPE", "TYPE", "TYPEDOC");
            if (!MatchesDocumentTypeCode(rawType, request.Filters.IncludedDocumentTypes))
            {
                skipped++;
                continue;
            }

            var mappedType = MapDocumentType(header, isPurchase);
            if (mappedType is null || !selectedTypes.Contains(mappedType.Value))
            {
                skipped++;
                continue;
            }

            var partnerCode = GetMappedString(header, headerMap, "PartnerCode", "DO_TIERS", "CT_NUM", "TIERS", "CODETIERS");
            if (string.IsNullOrWhiteSpace(partnerCode) || !state.Partners.TryGetValue(partnerCode, out var partner))
            {
                skipped++;
                notes.Add($"Piece `{sourceNumber}` ignoree : tiers `{partnerCode}` absent ou non importe.");
                continue;
            }

            if (!PartnerMatchesFlow(partner, isPurchase))
            {
                skipped++;
                continue;
            }

            var number = request.Mapping.DocumentNumberPolicy == SageDocumentNumberPolicy.RenumberInLigCom
                ? BuildImportedNumber(mappedType.Value, sourceNumber)
                : sourceNumber;

            if (state.Documents.TryGetValue(number, out var existingDocument))
            {
                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.SkipExisting)
                {
                    skipped++;
                    continue;
                }

                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.PrefixAndCreate)
                {
                    number = EnsureUniqueCode(state.DocumentNumbers, BuildImportedNumber(mappedType.Value, sourceNumber));
                }
                else
                {
                    existingDocument.DocumentDate = documentDate ?? existingDocument.DocumentDate;
                    existingDocument.DueDate = GetMappedDate(header, headerMap, "DueDate", "DO_ECHEANCE", "DATEECHEANCE", "ECHEANCE");
                    existingDocument.Notes = GetMappedString(header, headerMap, "Notes", "DO_REFCOM", "COMMENTAIRE", "NOTES");
                    existingDocument.WarehouseId = ResolveWarehouseId(state, header, request);
                    existingDocument.CurrencyCode = EmptyAsDefault(GetMappedString(header, headerMap, "Currency", "DO_DEVISE", "DEVISE", "CURRENCY"), existingDocument.CurrencyCode, 3);
                    dbContext.CommercialDocumentLines.RemoveRange(existingDocument.Lines);
                    existingDocument.Lines.Clear();
                    PopulateDocumentLines(existingDocument, linesByPiece, sourceNumber, state, notes, lineMap);
                    ComputeTotals(existingDocument);
                    updated++;
                    continue;
                }
            }

            var document = new CommercialDocument
            {
                TenantId = request.TenantId,
                DocumentType = mappedType.Value,
                Status = MapDocumentStatus(header),
                Number = number,
                DocumentDate = documentDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    DueDate = GetMappedDate(header, headerMap, "DueDate", "DO_ECHEANCE", "DATEECHEANCE", "ECHEANCE"),
                    CurrencyCode = EmptyAsDefault(GetMappedString(header, headerMap, "Currency", "DO_DEVISE", "DEVISE", "CURRENCY"), "CAD", 3),
                    Notes = GetMappedString(header, headerMap, "Notes", "DO_REFCOM", "COMMENTAIRE", "NOTES"),
                    PartnerId = partner.Id,
                    WarehouseId = ResolveWarehouseId(state, header, request)
                };

                PopulateDocumentLines(document, linesByPiece, sourceNumber, state, notes, lineMap);
                ComputeTotals(document);

            if (document.Lines.Count == 0)
            {
                skipped++;
                notes.Add($"Piece `{sourceNumber}` ignoree : aucune ligne exploitable n'a ete detectee.");
                continue;
            }

            dbContext.CommercialDocuments.Add(document);
            state.Documents[number] = document;
            state.DocumentNumbers.Add(number);
            imported++;
        }

        return new SageImportModuleReport(
            isPurchase ? "Documents d'achat" : "Documents de vente",
            "Traite",
            $"{headerTable.TableName} + {lineTable.TableName}",
            imported,
            updated,
            skipped,
            "Import des pieces filtrees par periode et type, a partir des entetes et lignes detectes dans la base Sage.",
            notes.Distinct().Take(20).ToArray());
    }

    private async Task<SageImportModuleReport> ImportOpeningStockAsync(
        SqlConnection sourceConnection,
        IReadOnlyList<SourceTableInfo> schema,
        SageImportExecutionRequest request,
        ImportState state,
        CancellationToken cancellationToken)
    {
        var moduleMap = ParseMapping(request.Mapping.SchemaMapping.Stock);
        var candidate = FindBestTable(
            schema,
            ["F_STOCK", "STOCK", "MOUVSTOCK", "F_MOUVSTOCK", "MOUVEMENTSTOCK"],
            ["AR_REF", "DE_NO", "QTE", "COUT", "CMP", "PRIXACH"],
            request.Mapping.SchemaMapping.Stock.TableName,
            moduleMap.Values);

        if (candidate is null)
        {
            return MissingModule("Stock initial", "Aucune table stock ou mouvement de stock evidente n'a ete detectee.");
        }

        var rows = await ReadRowsAsync(sourceConnection, candidate.TableName, cancellationToken);
        var grouped = new Dictionary<string, OpeningStockAggregate>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var productCode = GetMappedString(row, moduleMap, "ProductCode", "AR_REF", "ARTICLE", "CODEARTICLE", "SKU");
            if (string.IsNullOrWhiteSpace(productCode))
            {
                continue;
            }

            if (!InRange(productCode, request.Filters.ProductCodeFrom, request.Filters.ProductCodeTo))
            {
                continue;
            }

            var movementDate = GetMappedDate(row, moduleMap, "MovementDate", "MS_DATE", "DO_DATE", "DATE", "DATEMVT");
            if (movementDate.HasValue)
            {
                if (request.Filters.DateFrom.HasValue && movementDate.Value < DateOnly.FromDateTime(request.Filters.DateFrom.Value))
                {
                    continue;
                }

                if (request.Filters.DateTo.HasValue && movementDate.Value > DateOnly.FromDateTime(request.Filters.DateTo.Value))
                {
                    continue;
                }
            }

            var warehouseCode = GetMappedString(row, moduleMap, "WarehouseCode", "DE_NO", "DEPOT", "MAGASIN");
            if (string.IsNullOrWhiteSpace(warehouseCode))
            {
                warehouseCode = request.Mapping.WarehouseFallbackCode;
            }

            if (!WarehouseAllowed(warehouseCode, request.Filters.IncludedWarehouses))
            {
                continue;
            }

            var quantity = GetMappedDecimal(row, moduleMap, "Quantity", "QTE", "QTESTOCK", "MS_QTE", "AS_QTE", "ST_QTE");
            var unitCost = GetMappedDecimal(row, moduleMap, "UnitCost", "COUT", "CMP", "PRIXACH", "PA_HT", "CUMP");
            if (quantity == 0m)
            {
                continue;
            }

            var key = $"{productCode}|{warehouseCode}";
            if (!grouped.TryGetValue(key, out var aggregate))
            {
                aggregate = new OpeningStockAggregate(productCode, warehouseCode);
                grouped[key] = aggregate;
            }

            aggregate.Quantity += quantity;
            if (unitCost > 0m)
            {
                aggregate.UnitCost = unitCost;
            }
            if (movementDate.HasValue && (!aggregate.MovementDate.HasValue || movementDate.Value > aggregate.MovementDate.Value))
            {
                aggregate.MovementDate = movementDate;
            }
        }

        var imported = 0;
        var updated = 0;
        var skipped = 0;
        List<string> notes = [];

        foreach (var aggregate in grouped.Values.OrderBy(x => x.ProductCode).ThenBy(x => x.WarehouseCode))
        {
            if (aggregate.Quantity <= 0m)
            {
                skipped++;
                continue;
            }

            var targetProductCode = ResolveTargetProductCode(state, aggregate.ProductCode, request.Mapping.ProductPrefix);
            if (string.IsNullOrWhiteSpace(targetProductCode) || !state.Products.TryGetValue(targetProductCode, out var product))
            {
                skipped++;
                notes.Add($"Stock `{aggregate.ProductCode}` ignore : article absent de LigCom.");
                continue;
            }

            var warehouseId = ResolveWarehouseId(state, aggregate.WarehouseCode, request.Mapping.WarehouseFallbackCode);
            if (!warehouseId.HasValue)
            {
                skipped++;
                notes.Add($"Stock `{aggregate.ProductCode}` ignore : depot `{aggregate.WarehouseCode}` introuvable.");
                continue;
            }

            var reference = $"SAGE-OPEN-{aggregate.ProductCode}-{aggregate.WarehouseCode}";
            var existingMovement = state.StockMovements.FirstOrDefault(x => string.Equals(x.ReferenceNumber, reference, StringComparison.OrdinalIgnoreCase));
            if (existingMovement is not null)
            {
                if (request.Mapping.ExistingRecordPolicy == SageExistingRecordPolicy.SkipExisting)
                {
                    skipped++;
                    continue;
                }

                existingMovement.Quantity = aggregate.Quantity;
                existingMovement.UnitCost = aggregate.UnitCost > 0m ? aggregate.UnitCost : product.PurchasePrice;
                existingMovement.MovementDate = aggregate.MovementDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                existingMovement.WarehouseId = warehouseId.Value;
                existingMovement.ProductId = product.Id;
                updated++;
                continue;
            }

            var movement = new StockMovement
            {
                TenantId = request.TenantId,
                ProductId = product.Id,
                WarehouseId = warehouseId.Value,
                MovementDate = aggregate.MovementDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                MovementType = StockMovementType.OpeningBalance,
                Quantity = aggregate.Quantity,
                UnitCost = aggregate.UnitCost > 0m ? aggregate.UnitCost : product.PurchasePrice,
                ReferenceNumber = reference
            };

            dbContext.StockMovements.Add(movement);
            state.StockMovements.Add(movement);
            imported++;
        }

        return new SageImportModuleReport(
            "Stock initial",
            "Traite",
            candidate.TableName,
            imported,
            updated,
            skipped,
            "Les quantites source ont ete converties en mouvements LigCom de type OpeningBalance.",
            notes.Distinct().Take(20).ToArray());
    }

    private async Task<SageImportModuleReport> ImportOpenBalancesAsync(
        SqlConnection sourceConnection,
        IReadOnlyList<SourceTableInfo> schema,
        SageImportExecutionRequest request,
        ImportState state,
        CancellationToken cancellationToken)
    {
        var headerTable = FindBestTable(schema, ["F_DOCENTETE", "DOCENTETE", "F_DOCUMENT", "DOCUMENT"], ["DO_PIECE", "DO_DATE", "DO_TIERS", "DO_TYPE"]);
        var lineTable = FindBestTable(schema, ["F_DOCLIGNE", "DOCLIGNE", "F_DOCLIG", "DOCUMENTLIGNE"], ["DO_PIECE", "AR_REF", "DL_QTE", "DL_PUHT"]);

        if (headerTable is null)
        {
            return MissingModule("Encours et soldes ouverts", "Aucune table d'entete documentaire n'a ete detectee pour reconstituer les soldes ouverts.");
        }

        var headerRows = await ReadRowsAsync(sourceConnection, headerTable.TableName, cancellationToken);
        var linesByPiece = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase);
        if (lineTable is not null)
        {
            var lineRows = await ReadRowsAsync(sourceConnection, lineTable.TableName, cancellationToken);
            linesByPiece = lineRows
                .GroupBy(row => GetString(row, "DO_PIECE", "PIECE", "DOCNUM", "NUMERO"))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        }

        var imported = 0;
        var updated = 0;
        var skipped = 0;
        List<string> notes = [];

        foreach (var header in headerRows)
        {
            var mappedType = MapDocumentType(header, isPurchase: false);
            var purchaseMappedType = MapDocumentType(header, isPurchase: true);

            var documentType = mappedType is CommercialDocumentType.SalesInvoice ? mappedType
                : purchaseMappedType is CommercialDocumentType.PurchaseInvoice ? purchaseMappedType
                : null;

            if (documentType is null)
            {
                skipped++;
                continue;
            }

            if (documentType == CommercialDocumentType.SalesInvoice && !request.Filters.DocumentTypes.SalesInvoice)
            {
                skipped++;
                continue;
            }

            if (documentType == CommercialDocumentType.PurchaseInvoice && !request.Filters.DocumentTypes.PurchaseInvoice)
            {
                skipped++;
                continue;
            }

            var sourceNumber = GetString(header, "DO_PIECE", "PIECE", "DOCNUM", "NUMERO");
            if (string.IsNullOrWhiteSpace(sourceNumber))
            {
                skipped++;
                continue;
            }

            var documentDate = GetDate(header, "DO_DATE", "DATE", "DATEDOC");
            if (documentDate.HasValue)
            {
                if (request.Filters.DateFrom.HasValue && documentDate.Value < DateOnly.FromDateTime(request.Filters.DateFrom.Value))
                {
                    skipped++;
                    continue;
                }

                if (request.Filters.DateTo.HasValue && documentDate.Value > DateOnly.FromDateTime(request.Filters.DateTo.Value))
                {
                    skipped++;
                    continue;
                }
            }

            var totalAmount = GetDecimal(header, "DO_TTC", "TOTALTTC", "NETAPAYER", "MONTANTTTC", "TOTAL");
            if (totalAmount <= 0m)
            {
                skipped++;
                continue;
            }

            var paidAmount = GetDecimal(header, "DO_REGLE", "MONTANTREGLE", "REGLE", "PAID");
            var balanceAmount = GetDecimal(header, "DO_SOLDE", "SOLDE", "RESTEAPAYER", "DUE");
            if (balanceAmount <= 0m)
            {
                balanceAmount = totalAmount - paidAmount;
            }

            if (balanceAmount <= 0m)
            {
                skipped++;
                continue;
            }

            var partnerCode = GetString(header, "DO_TIERS", "CT_NUM", "TIERS", "CODETIERS");
            if (string.IsNullOrWhiteSpace(partnerCode) || !state.Partners.TryGetValue(partnerCode, out var partner))
            {
                skipped++;
                notes.Add($"Encours `{sourceNumber}` ignore : tiers `{partnerCode}` absent ou non importe.");
                continue;
            }

            var importNumber = state.Documents.ContainsKey(sourceNumber) ? sourceNumber : $"SOPEN-{sourceNumber}";
            if (!state.Documents.TryGetValue(importNumber, out var document))
            {
                document = new CommercialDocument
                {
                    TenantId = request.TenantId,
                    DocumentType = documentType.Value,
                    Status = CommercialDocumentStatus.Open,
                    Number = importNumber,
                    DocumentDate = documentDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    DueDate = GetDate(header, "DO_ECHEANCE", "DATEECHEANCE", "ECHEANCE"),
                    CurrencyCode = EmptyAsDefault(GetString(header, "DO_DEVISE", "DEVISE", "CURRENCY"), "CAD", 3),
                    Notes = "Solde ouvert importe depuis Sage",
                    PartnerId = partner.Id,
                    WarehouseId = ResolveWarehouseId(state, header, request)
                };

                if (lineTable is not null && linesByPiece.ContainsKey(sourceNumber))
                {
                    PopulateDocumentLines(document, linesByPiece, sourceNumber, state, notes, ParseMapping(request.Mapping.SchemaMapping.DocumentLines));
                    ComputeTotals(document);
                }

                if (document.Lines.Count == 0)
                {
                    document.Lines.Add(new CommercialDocumentLine
                    {
                        Description = "Solde ouvert importe Sage",
                        Quantity = 1m,
                        UnitPriceExcludingTax = totalAmount,
                        DiscountRate = 0m,
                        TaxRate = 0m,
                        LineTotalExcludingTax = totalAmount,
                        LineTaxAmount = 0m,
                        LineTotalIncludingTax = totalAmount
                    });
                    document.TotalExcludingTax = totalAmount;
                    document.TotalTax = 0m;
                    document.TotalIncludingTax = totalAmount;
                }

                dbContext.CommercialDocuments.Add(document);
                state.Documents[importNumber] = document;
                state.DocumentNumbers.Add(importNumber);
                imported++;
            }
            else
            {
                document.TotalIncludingTax = totalAmount;
                if (document.TotalExcludingTax == 0m)
                {
                    document.TotalExcludingTax = totalAmount;
                }
                updated++;
            }

            var syntheticPaid = decimal.Round(totalAmount - balanceAmount, 2);
            if (syntheticPaid > 0m)
            {
                var paymentRef = $"SAGE-REG-{document.Number}";
                var payment = state.Payments.FirstOrDefault(x => string.Equals(x.ReferenceNumber, paymentRef, StringComparison.OrdinalIgnoreCase));
                if (payment is null)
                {
                    payment = new Payment
                    {
                        TenantId = request.TenantId,
                        PaymentDate = document.DocumentDate,
                        Direction = documentType == CommercialDocumentType.SalesInvoice ? PaymentDirection.Incoming : PaymentDirection.Outgoing,
                        Method = PaymentMethod.Other,
                        ReferenceNumber = paymentRef,
                        CurrencyCode = document.CurrencyCode,
                        Amount = syntheticPaid,
                        Notes = "Reglement partiel synthetique importe depuis Sage",
                        PartnerId = document.PartnerId
                    };
                    payment.Allocations.Add(new PaymentAllocation
                    {
                        CommercialDocumentId = document.Id,
                        CommercialDocument = document,
                        AllocatedAmount = syntheticPaid
                    });
                    dbContext.Payments.Add(payment);
                    state.Payments.Add(payment);
                }
                else
                {
                    payment.Amount = syntheticPaid;
                    var allocation = payment.Allocations.FirstOrDefault(x => x.CommercialDocumentId == document.Id);
                    if (allocation is null)
                    {
                        payment.Allocations.Add(new PaymentAllocation
                        {
                            CommercialDocumentId = document.Id,
                            CommercialDocument = document,
                            AllocatedAmount = syntheticPaid
                        });
                    }
                    else
                    {
                        allocation.AllocatedAmount = syntheticPaid;
                    }
                }

                document.Status = balanceAmount <= 0m ? CommercialDocumentStatus.Completed : CommercialDocumentStatus.PartiallyProcessed;
            }
            else
            {
                document.Status = CommercialDocumentStatus.Open;
            }
        }

        return new SageImportModuleReport(
            "Encours et soldes ouverts",
            "Traite",
            headerTable.TableName,
            imported,
            updated,
            skipped,
            "Les soldes ouverts Sage sont recrees comme factures ouvertes LigCom, avec reglements synthetiques quand une partie est deja reglee.",
            notes.Distinct().Take(20).ToArray());
    }

    private static SageImportModuleReport MissingModule(string moduleName, string message)
    {
        return new SageImportModuleReport(moduleName, "Non detecte", "-", 0, 0, 0, message, [message]);
    }

    private static string BuildConnectionString(SageImportExecutionRequest request)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = request.SourceServer,
            InitialCatalog = request.SourceDatabase,
            TrustServerCertificate = true,
            Encrypt = true,
            ConnectTimeout = 5
        };

        if (request.AuthenticationMode == ExternalSqlAuthenticationMode.Windows)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = request.SourceUserName;
            builder.Password = request.SourcePassword;
        }

        return builder.ConnectionString;
    }

    private static async Task<List<SourceTableInfo>> ReadSchemaAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, SourceTableInfo>(StringComparer.OrdinalIgnoreCase);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT t.name AS TableName, c.name AS ColumnName
            FROM sys.tables AS t
            INNER JOIN sys.columns AS c ON c.object_id = t.object_id
            WHERE t.is_ms_shipped = 0
            ORDER BY t.name, c.column_id;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var tableName = reader.GetString(0);
            var columnName = reader.GetString(1);
            if (!result.TryGetValue(tableName, out var table))
            {
                table = new SourceTableInfo(tableName);
                result.Add(tableName, table);
            }
            table.Columns.Add(columnName);
        }

        return result.Values.ToList();
    }

    private static SourceTableInfo? FindBestTable(IReadOnlyList<SourceTableInfo> schema, IReadOnlyList<string> tableHints, IReadOnlyList<string> columnHints, string preferredTable = "", IEnumerable<string[]>? additionalColumnHints = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredTable))
        {
            var exact = schema.FirstOrDefault(x => string.Equals(x.TableName, preferredTable.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }
        }

        var allColumnHints = columnHints
            .Concat((additionalColumnHints ?? []).SelectMany(x => x))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return schema
            .Select(table => new { Table = table, Score = ScoreTable(table, tableHints, allColumnHints) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Table.TableName)
            .Select(x => x.Table)
            .FirstOrDefault();
    }

    private static int ScoreTable(SourceTableInfo table, IReadOnlyList<string> tableHints, IReadOnlyList<string> columnHints)
    {
        var score = 0;
        foreach (var hint in tableHints)
        {
            if (string.Equals(table.TableName, hint, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
            else if (Normalize(table.TableName).Contains(Normalize(hint), StringComparison.OrdinalIgnoreCase))
            {
                score += 45;
            }
        }

        foreach (var column in table.Columns)
        {
            if (columnHints.Any(hint => Normalize(column).Contains(Normalize(hint), StringComparison.OrdinalIgnoreCase)))
            {
                score += 10;
            }
        }

        return score;
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        List<Dictionary<string, object?>> rows = [];
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM [{tableName}]";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }
        return rows;
    }

    private static BusinessPartner BuildPartner(string code, string name, BusinessPartnerType type, Dictionary<string, object?> row, Guid tenantId, Guid? paymentTermId)
    {
        return new BusinessPartner
        {
            TenantId = tenantId,
            Code = code,
            Name = name,
            PartnerType = type,
            Email = GetString(row, "CT_EMAIL", "EMAIL", "MAIL"),
            PhoneNumber = GetString(row, "CT_TELEPHONE", "TEL", "PHONE"),
            VatNumber = GetString(row, "CT_IDENTIFIANT", "VAT", "TVA", "SIREN"),
            IsActive = !IsInactive(row),
            PaymentTermId = paymentTermId,
            BillingAddress = BuildAddress(row),
            ShippingAddress = BuildAddress(row)
        };
    }

    private static void ApplyPartner(BusinessPartner partner, string name, BusinessPartnerType type, Dictionary<string, object?> row)
    {
        partner.Name = name;
        partner.PartnerType = partner.PartnerType == type ? partner.PartnerType : BusinessPartnerType.Both;
        partner.Email = GetString(row, "CT_EMAIL", "EMAIL", "MAIL");
        partner.PhoneNumber = GetString(row, "CT_TELEPHONE", "TEL", "PHONE");
        partner.VatNumber = GetString(row, "CT_IDENTIFIANT", "VAT", "TVA", "SIREN");
        partner.IsActive = !IsInactive(row);
        partner.BillingAddress = BuildAddress(row);
        partner.ShippingAddress = BuildAddress(row);
    }

    private static Address BuildAddress(Dictionary<string, object?> row)
    {
        return new Address
        {
            Recipient = GetString(row, "CT_CONTACT", "CONTACT"),
            StreetLine1 = GetString(row, "CT_ADRESSE", "ADRESSE1", "RUE"),
            StreetLine2 = GetString(row, "CT_COMPLEMENT", "ADRESSE2", "COMPLEMENT"),
            PostalCode = GetString(row, "CT_CODEPOSTAL", "CP", "POSTALCODE"),
            City = GetString(row, "CT_VILLE", "VILLE", "CITY"),
            Country = GetString(row, "CT_PAYS", "PAYS", "COUNTRY")
        };
    }

    private static void ApplyProduct(Product product, string label, Dictionary<string, object?> row, Guid? categoryId, Guid? taxId)
    {
        product.Label = label;
        product.Description = GetString(row, "AR_DESIGN2", "DESCRIPTION", "DESCRIPTIF");
        product.PurchasePrice = GetDecimal(row, "AR_PRIXACH", "PRIXACHAT", "PA_HT");
        product.SalesPrice = GetDecimal(row, "AR_PRIXVEN", "PRIXVENTE", "PV_HT");
        product.UnitOfMeasure = EmptyAsDefault(GetString(row, "AR_UNITEVEN", "UNITE", "UN"), "UN", 10);
        product.ProductType = InferProductType(row);
        product.TrackStock = InferTrackStock(row);
        product.IsActive = !IsInactive(row);
        product.ProductCategoryId = categoryId;
        product.TaxCodeId = taxId;
    }

    private static Dictionary<string, string[]> ParseMapping(SageImportModuleMappingSelection mapping)
    {
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(mapping.FieldMap))
        {
            return result;
        }

        var lines = mapping.FieldMap
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var tokens = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (tokens.Length != 2 || string.IsNullOrWhiteSpace(tokens[0]) || string.IsNullOrWhiteSpace(tokens[1]))
            {
                continue;
            }

            var aliases = tokens[1]
                .Split(['|', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            if (aliases.Length > 0)
            {
                result[tokens[0]] = aliases;
            }
        }

        return result;
    }

    private static string GetMappedString(Dictionary<string, object?> row, Dictionary<string, string[]> mapping, string logicalField, params string[] fallbackHints)
    {
        if (mapping.TryGetValue(logicalField, out var aliases) && aliases.Length > 0)
        {
            var mapped = GetString(row, aliases);
            if (!string.IsNullOrWhiteSpace(mapped))
            {
                return mapped;
            }
        }

        return GetString(row, fallbackHints);
    }

    private static decimal GetMappedDecimal(Dictionary<string, object?> row, Dictionary<string, string[]> mapping, string logicalField, params string[] fallbackHints)
    {
        if (mapping.TryGetValue(logicalField, out var aliases) && aliases.Length > 0)
        {
            var mapped = GetDecimal(row, aliases);
            if (mapped != 0m)
            {
                return mapped;
            }
        }

        return GetDecimal(row, fallbackHints);
    }

    private static int GetMappedInt(Dictionary<string, object?> row, Dictionary<string, string[]> mapping, string logicalField, params string[] fallbackHints)
    {
        if (mapping.TryGetValue(logicalField, out var aliases) && aliases.Length > 0)
        {
            var mapped = GetInt(row, aliases);
            if (mapped != 0)
            {
                return mapped;
            }
        }

        return GetInt(row, fallbackHints);
    }

    private static ProductType InferProductType(Dictionary<string, object?> row)
    {
        var label = GetString(row, "AR_TYPE", "TYPE", "NATURE");
        return label.Contains("SER", StringComparison.OrdinalIgnoreCase) || label.Contains("SERVICE", StringComparison.OrdinalIgnoreCase)
            ? ProductType.Service
            : ProductType.StockItem;
    }

    private static bool InferTrackStock(Dictionary<string, object?> row)
    {
        var trackValue = GetString(row, "AR_SUIVISTOCK", "SUIVISTOCK", "GESTSTOCK");
        if (string.IsNullOrWhiteSpace(trackValue))
        {
            return true;
        }

        return !trackValue.Equals("0", StringComparison.OrdinalIgnoreCase)
            && !trackValue.Equals("FALSE", StringComparison.OrdinalIgnoreCase)
            && !trackValue.Equals("NON", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<CommercialDocumentType> GetSelectedDocumentTypes(SageImportDocumentTypeSelection selection, bool isPurchase)
    {
        var result = new HashSet<CommercialDocumentType>();
        if (isPurchase)
        {
            if (selection.PurchaseRequest) result.Add(CommercialDocumentType.PurchaseRequest);
            if (selection.PurchaseOrder) result.Add(CommercialDocumentType.PurchaseOrder);
            if (selection.GoodsReceipt) result.Add(CommercialDocumentType.GoodsReceipt);
            if (selection.PurchaseInvoice) result.Add(CommercialDocumentType.PurchaseInvoice);
            if (selection.SupplierCreditNote) result.Add(CommercialDocumentType.SupplierCreditNote);
        }
        else
        {
            if (selection.SalesQuote) result.Add(CommercialDocumentType.SalesQuote);
            if (selection.SalesOrder) result.Add(CommercialDocumentType.SalesOrder);
            if (selection.DeliveryNote) result.Add(CommercialDocumentType.DeliveryNote);
            if (selection.SalesInvoice) result.Add(CommercialDocumentType.SalesInvoice);
            if (selection.SalesCreditNote) result.Add(CommercialDocumentType.SalesCreditNote);
        }

        return result;
    }

    private static bool MatchesDocumentTypeCode(string rawType, string includedDocumentTypes)
    {
        if (string.IsNullOrWhiteSpace(includedDocumentTypes))
        {
            return true;
        }

        var filters = includedDocumentTypes
            .Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return filters.Any(filter => Normalize(rawType).Contains(Normalize(filter), StringComparison.OrdinalIgnoreCase));
    }

    private static CommercialDocumentType? MapDocumentType(Dictionary<string, object?> header, bool isPurchase)
    {
        var rawType = GetString(header, "DO_TYPE", "TYPE", "TYPEDOC", "NATURE");
        var normalized = Normalize(rawType);

        if (normalized.Contains("DEV")) return CommercialDocumentType.SalesQuote;
        if (normalized.Contains("BL") || normalized.Contains("LIV")) return isPurchase ? CommercialDocumentType.GoodsReceipt : CommercialDocumentType.DeliveryNote;
        if (normalized.Contains("AVO")) return isPurchase ? CommercialDocumentType.SupplierCreditNote : CommercialDocumentType.SalesCreditNote;
        if (normalized.Contains("FAC")) return isPurchase ? CommercialDocumentType.PurchaseInvoice : CommercialDocumentType.SalesInvoice;
        if (normalized.Contains("CMD")) return isPurchase ? CommercialDocumentType.PurchaseOrder : CommercialDocumentType.SalesOrder;
        if (normalized.Contains("REC")) return CommercialDocumentType.GoodsReceipt;
        if (normalized.Contains("DEM") || normalized.Contains("DA")) return CommercialDocumentType.PurchaseRequest;

        return isPurchase ? CommercialDocumentType.PurchaseOrder : CommercialDocumentType.SalesOrder;
    }

    private static CommercialDocumentStatus MapDocumentStatus(Dictionary<string, object?> header)
    {
        var rawStatus = Normalize(GetString(header, "DO_STATUT", "STATUT", "STATUS"));
        if (rawStatus.Contains("ANNUL")) return CommercialDocumentStatus.Cancelled;
        if (rawStatus.Contains("CLOT") || rawStatus.Contains("TERM") || rawStatus.Contains("SOLDE")) return CommercialDocumentStatus.Completed;
        if (rawStatus.Contains("PART")) return CommercialDocumentStatus.PartiallyProcessed;
        if (rawStatus.Contains("BROU") || rawStatus.Contains("DRAFT")) return CommercialDocumentStatus.Draft;
        return CommercialDocumentStatus.Open;
    }

    private static bool IsPurchasePartner(BusinessPartner partner)
    {
        return partner.PartnerType is BusinessPartnerType.Supplier or BusinessPartnerType.Both;
    }

    private static bool PartnerMatchesFlow(BusinessPartner partner, bool isPurchase)
    {
        return isPurchase
            ? partner.PartnerType is BusinessPartnerType.Supplier or BusinessPartnerType.Both
            : partner.PartnerType is BusinessPartnerType.Customer or BusinessPartnerType.Both or BusinessPartnerType.Prospect;
    }

    private static string BuildImportedNumber(CommercialDocumentType type, string sourceNumber)
    {
        var prefix = type switch
        {
            CommercialDocumentType.SalesQuote => "SDEV-",
            CommercialDocumentType.SalesOrder => "SCMD-",
            CommercialDocumentType.DeliveryNote => "SBL-",
            CommercialDocumentType.SalesInvoice => "SFAC-",
            CommercialDocumentType.SalesCreditNote => "SAVO-",
            CommercialDocumentType.PurchaseRequest => "SDA-",
            CommercialDocumentType.PurchaseOrder => "SACH-",
            CommercialDocumentType.GoodsReceipt => "SREC-",
            CommercialDocumentType.PurchaseInvoice => "SFAF-",
            CommercialDocumentType.SupplierCreditNote => "SAVF-",
            _ => "SDOC-"
        };

        return $"{prefix}{sourceNumber}";
    }

    private static Guid? ResolveWarehouseId(ImportState state, Dictionary<string, object?> row, SageImportExecutionRequest request)
    {
        var sourceCode = GetString(row, "DE_NO", "DEPOT", "MAGASIN");
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            sourceCode = request.Mapping.WarehouseFallbackCode;
        }

        return !string.IsNullOrWhiteSpace(sourceCode) && state.Warehouses.TryGetValue(sourceCode, out var warehouse)
            ? warehouse.Id
            : null;
    }

    private static Guid? ResolveWarehouseId(ImportState state, string warehouseCode, string fallbackWarehouseCode)
    {
        var resolvedCode = string.IsNullOrWhiteSpace(warehouseCode) ? fallbackWarehouseCode : warehouseCode;
        return !string.IsNullOrWhiteSpace(resolvedCode) && state.Warehouses.TryGetValue(resolvedCode, out var warehouse)
            ? warehouse.Id
            : null;
    }

    private static bool WarehouseAllowed(string warehouseCode, string includedWarehouses)
    {
        if (string.IsNullOrWhiteSpace(includedWarehouses))
        {
            return true;
        }

        var filters = includedWarehouses
            .Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return filters.Any(filter => string.Equals(filter, warehouseCode, StringComparison.OrdinalIgnoreCase));
    }

    private static void PopulateDocumentLines(CommercialDocument document, IReadOnlyDictionary<string, List<Dictionary<string, object?>>> linesByPiece, string sourceNumber, ImportState state, List<string> notes, Dictionary<string, string[]> lineMap)
    {
        if (!linesByPiece.TryGetValue(sourceNumber, out var sourceLines))
        {
            return;
        }

        foreach (var row in sourceLines)
        {
            var sourceProductCode = GetMappedString(row, lineMap, "ProductCode", "AR_REF", "ARTICLE", "CODEARTICLE");
            Guid? productId = null;
            if (!string.IsNullOrWhiteSpace(sourceProductCode))
            {
                var targetCode = ResolveTargetProductCode(state, sourceProductCode, string.Empty);
                if (!string.IsNullOrWhiteSpace(targetCode) && state.Products.TryGetValue(targetCode, out var product))
                {
                    productId = product.Id;
                }
                else
                {
                    notes.Add($"Ligne `{sourceNumber}` : article `{sourceProductCode}` non rapproche dans LigCom.");
                }
            }

            var description = GetMappedString(row, lineMap, "Description", "DL_DESIGN", "DESIGNATION", "LIBELLE", "AR_DESIGN");
            if (string.IsNullOrWhiteSpace(description))
            {
                description = sourceProductCode;
            }

            var quantity = GetMappedDecimal(row, lineMap, "Quantity", "DL_QTE", "QTE", "QUANTITE");
            var unitPrice = GetMappedDecimal(row, lineMap, "UnitPrice", "DL_PUHT", "PUHT", "PRIX", "PU");
            var discount = GetMappedDecimal(row, lineMap, "DiscountRate", "DL_REMISE", "REMISE", "DISCOUNT");
            var taxRate = GetMappedDecimal(row, lineMap, "TaxRate", "DL_TAUXTAXE", "TAUXTVA", "TAXE", "TVA");

            document.Lines.Add(new CommercialDocumentLine
            {
                ProductId = productId,
                Description = string.IsNullOrWhiteSpace(description) ? "Ligne importee Sage" : description,
                Quantity = quantity == 0m ? 1m : quantity,
                UnitPriceExcludingTax = unitPrice,
                DiscountRate = discount,
                TaxRate = taxRate
            });
        }
    }

    private static void ComputeTotals(CommercialDocument document)
    {
        foreach (var line in document.Lines)
        {
            var baseTotal = decimal.Round(line.Quantity * line.UnitPriceExcludingTax, 2);
            var discounted = decimal.Round(baseTotal * (1 - (line.DiscountRate / 100m)), 2);
            var tax = decimal.Round(discounted * (line.TaxRate / 100m), 2);
            line.LineTotalExcludingTax = discounted;
            line.LineTaxAmount = tax;
            line.LineTotalIncludingTax = discounted + tax;
        }

        document.TotalExcludingTax = document.Lines.Sum(x => x.LineTotalExcludingTax);
        document.TotalTax = document.Lines.Sum(x => x.LineTaxAmount);
        document.TotalIncludingTax = document.Lines.Sum(x => x.LineTotalIncludingTax);
    }

    private static DateOnly? GetDate(Dictionary<string, object?> row, params string[] hints)
    {
        foreach (var hint in hints)
        {
            var key = row.Keys.FirstOrDefault(x => Normalize(x).Contains(Normalize(hint), StringComparison.OrdinalIgnoreCase));
            if (key is null || row[key] is null)
            {
                continue;
            }

            try
            {
                var value = Convert.ToDateTime(row[key]);
                return DateOnly.FromDateTime(value);
            }
            catch
            {
            }
        }

        return null;
    }

    private static DateOnly? GetMappedDate(Dictionary<string, object?> row, Dictionary<string, string[]> mapping, string logicalField, params string[] fallbackHints)
    {
        if (mapping.TryGetValue(logicalField, out var aliases) && aliases.Length > 0)
        {
            var mapped = GetDate(row, aliases);
            if (mapped.HasValue)
            {
                return mapped;
            }
        }

        return GetDate(row, fallbackHints);
    }

    private Guid? ResolveCategoryId(ImportState state, string sourceCode, SageImportExecutionRequest request, List<string> notes)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return null;
        }

        if (state.Categories.TryGetValue(sourceCode, out var category))
        {
            return category.Id;
        }

        if (request.Mapping.MissingReferencePolicy == SageMissingReferencePolicy.CreateMissing)
        {
            category = new ProductCategory { TenantId = request.TenantId, Code = sourceCode.Trim(), Label = sourceCode.Trim() };
            dbContext.ProductCategories.Add(category);
            state.Categories[sourceCode] = category;
            state.CategoryCodes.Add(sourceCode);
            notes.Add($"Famille `{sourceCode}` creee automatiquement car absente de LigCom.");
            return category.Id;
        }

        notes.Add($"Famille `{sourceCode}` introuvable.");
        return null;
    }

    private Guid? ResolveTaxCodeId(ImportState state, string sourceCode, SageImportExecutionRequest request, List<string> notes)
    {
        var fallbackCode = string.IsNullOrWhiteSpace(sourceCode) ? request.Mapping.DefaultSalesTaxCode : sourceCode;
        if (string.IsNullOrWhiteSpace(fallbackCode))
        {
            return null;
        }

        if (state.TaxCodes.TryGetValue(fallbackCode, out var tax))
        {
            return tax.Id;
        }

        if (request.Mapping.MissingReferencePolicy == SageMissingReferencePolicy.CreateMissing)
        {
            tax = new TaxCode { TenantId = request.TenantId, Code = fallbackCode.Trim(), Label = fallbackCode.Trim(), Rate = 0m };
            dbContext.TaxCodes.Add(tax);
            state.TaxCodes[fallbackCode] = tax;
            notes.Add($"Taxe `{fallbackCode}` creee automatiquement car absente de LigCom.");
            return tax.Id;
        }

        notes.Add($"Taxe `{fallbackCode}` introuvable.");
        return null;
    }

    private static Guid? ResolvePaymentTerm(ImportState state, string sourceCode, SageImportExecutionRequest request)
    {
        var fallbackCode = string.IsNullOrWhiteSpace(sourceCode) ? request.Mapping.DefaultPaymentTermCode : sourceCode;
        return !string.IsNullOrWhiteSpace(fallbackCode) && state.PaymentTerms.TryGetValue(fallbackCode, out var paymentTerm)
            ? paymentTerm.Id
            : null;
    }

    private static string ResolveTargetProductCode(ImportState state, string sourceProductCode, string prefix)
    {
        if (state.ProductCodeMap.TryGetValue(sourceProductCode, out var mappedCode))
        {
            return mappedCode;
        }

        if (state.Products.ContainsKey(sourceProductCode))
        {
            return sourceProductCode;
        }

        var prefixedCode = BuildPrefixedCode(sourceProductCode, prefix);
        return state.Products.ContainsKey(prefixedCode) ? prefixedCode : string.Empty;
    }

    private static bool IsInactive(Dictionary<string, object?> row)
    {
        var value = GetString(row, "SOMMEIL", "ISACTIVE", "ACTIF", "ACTIVE");
        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("FALSE", StringComparison.OrdinalIgnoreCase)
            || value.Equals("NON", StringComparison.OrdinalIgnoreCase);
    }

    private static bool InRange(string code, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to))
        {
            return true;
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var normalizedFrom = string.IsNullOrWhiteSpace(from) ? null : from.Trim().ToUpperInvariant();
        var normalizedTo = string.IsNullOrWhiteSpace(to) ? null : to.Trim().ToUpperInvariant();

        return (normalizedFrom is null || string.CompareOrdinal(normalizedCode, normalizedFrom) >= 0)
               && (normalizedTo is null || string.CompareOrdinal(normalizedCode, normalizedTo) <= 0);
    }

    private static string BuildPrefixedCode(string baseCode, string prefix)
    {
        var safePrefix = string.IsNullOrWhiteSpace(prefix) ? "SAGE-" : prefix.Trim();
        return $"{safePrefix}{baseCode}".Trim();
    }

    private static string EnsureUniqueCode(HashSet<string> existingCodes, string desiredCode)
    {
        if (!existingCodes.Contains(desiredCode))
        {
            existingCodes.Add(desiredCode);
            return desiredCode;
        }

        var index = 2;
        while (existingCodes.Contains($"{desiredCode}-{index}"))
        {
            index++;
        }

        var finalCode = $"{desiredCode}-{index}";
        existingCodes.Add(finalCode);
        return finalCode;
    }

    private static string EmptyAsDefault(string value, string fallback, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string GetString(Dictionary<string, object?> row, params string[] hints)
    {
        foreach (var hint in hints)
        {
            var key = FindRowKey(row, hint);
            if (key is null)
            {
                continue;
            }

            var value = row[key];
            if (value is not null)
            {
                var text = Convert.ToString(value)?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }

    private static decimal GetDecimal(Dictionary<string, object?> row, params string[] hints)
    {
        var text = GetString(row, hints);
        if (decimal.TryParse(text, out var value))
        {
            return value;
        }

        foreach (var hint in hints)
        {
            var key = FindRowKey(row, hint);
            if (key is not null && row[key] is not null)
            {
                try
                {
                    return Convert.ToDecimal(row[key]);
                }
                catch
                {
                }
            }
        }

        return 0m;
    }

    private static int GetInt(Dictionary<string, object?> row, params string[] hints)
    {
        var text = GetString(row, hints);
        if (int.TryParse(text, out var value))
        {
            return value;
        }

        foreach (var hint in hints)
        {
            var key = FindRowKey(row, hint);
            if (key is not null && row[key] is not null)
            {
                try
                {
                    return Convert.ToInt32(row[key]);
                }
                catch
                {
                }
            }
        }

        return 0;
    }

    private static string? FindRowKey(Dictionary<string, object?> row, string hint)
    {
        var normalizedHint = Normalize(hint);

        var exactKey = row.Keys.FirstOrDefault(x => string.Equals(Normalize(x), normalizedHint, StringComparison.OrdinalIgnoreCase));
        if (exactKey is not null)
        {
            return exactKey;
        }

        return row.Keys.FirstOrDefault(x => Normalize(x).Contains(normalizedHint, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTreasuryJournal(int journalType, string code, string label, string? journalKind)
    {
        if (journalType == 2)
        {
            return true;
        }

        var tokens = new[] { code, label, journalKind ?? string.Empty };
        return tokens.Any(value =>
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().ToUpperInvariant();
            return normalized.Contains("TRESOR")
                || normalized.Contains("TRESO")
                || normalized.Contains("CAISSE")
                || normalized.Contains("BANQUE")
                || normalized.Contains("BANK")
                || normalized == "CAI"
                || normalized.StartsWith("CAI")
                || normalized.StartsWith("BQ")
                || normalized.StartsWith("BAN");
        });
    }

    private static string? GetFirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string Normalize(string value)
    {
        return value.Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToUpperInvariant();
    }

    private sealed class SourceTableInfo(string tableName)
    {
        public string TableName { get; } = tableName;
        public List<string> Columns { get; } = [];
    }

    private sealed class ImportState
    {
        public Dictionary<string, BusinessPartner> Partners { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> PartnerCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ProductCategory> Categories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> CategoryCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, TaxCode> TaxCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, PaymentTerm> PaymentTerms { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, JournalAccount> JournalAccounts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> JournalAccountCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Warehouse> Warehouses { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Product> Products { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ProductCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ProductCodeMap { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, PriceList> PriceLists { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, CommercialDocument> Documents { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> DocumentNumbers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<StockMovement> StockMovements { get; } = [];
        public List<Payment> Payments { get; } = [];

        public static async Task<ImportState> CreateAsync(ApplicationDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
        {
            var state = new ImportState();

            foreach (var partner in await dbContext.BusinessPartners.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken))
            {
                state.Partners[partner.Code] = partner;
                state.PartnerCodes.Add(partner.Code);
            }

            foreach (var category in await dbContext.ProductCategories.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken))
            {
                state.Categories[category.Code] = category;
                state.CategoryCodes.Add(category.Code);
            }

            foreach (var tax in await dbContext.TaxCodes.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken))
            {
                state.TaxCodes[tax.Code] = tax;
            }

            foreach (var paymentTerm in await dbContext.PaymentTerms.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken))
            {
                state.PaymentTerms[paymentTerm.Code] = paymentTerm;
            }

            foreach (var journalAccount in await dbContext.JournalAccounts.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken))
            {
                state.JournalAccounts[journalAccount.Code] = journalAccount;
                state.JournalAccountCodes.Add(journalAccount.Code);
            }

            foreach (var warehouse in await dbContext.Warehouses.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken))
            {
                state.Warehouses[warehouse.Code] = warehouse;
            }

            foreach (var product in await dbContext.Products.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken))
            {
                state.Products[product.Sku] = product;
                state.ProductCodes.Add(product.Sku);
                state.ProductCodeMap[product.Sku] = product.Sku;
            }

            foreach (var priceList in await dbContext.PriceLists.Include(x => x.Lines).Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken))
            {
                state.PriceLists[priceList.Code] = priceList;
            }

            foreach (var document in await dbContext.CommercialDocuments.Include(x => x.Lines).Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken))
            {
                state.Documents[document.Number] = document;
                state.DocumentNumbers.Add(document.Number);
            }

            state.StockMovements.AddRange(await dbContext.StockMovements.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken));
            state.Payments.AddRange(await dbContext.Payments.Include(x => x.Allocations).Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken));

            return state;
        }
    }

    private sealed class OpeningStockAggregate(string productCode, string warehouseCode)
    {
        public string ProductCode { get; } = productCode;
        public string WarehouseCode { get; } = warehouseCode;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public DateOnly? MovementDate { get; set; }
    }
}
