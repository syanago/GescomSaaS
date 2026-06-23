using System.Net.Http.Json;
using System.Text.Json;
using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Domain.Exceptions;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GescomSaas.Infrastructure.Services;

public sealed class OfflineSyncService(
    ApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IHostEnvironment hostEnvironment,
    ICommercialDocumentWorkflowService commercialDocumentWorkflowService,
    ISettlementService settlementService,
    IStockDocumentService stockDocumentService,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ITenantAccessProfileService tenantAccessProfileService,
    IRuntimeInitializationStateService runtimeInitializationStateService,
    IConfiguration configuration,
    IOptions<LigComRuntimeOptions> runtimeOptions,
    IOptions<OfflineSyncOptions> offlineSyncOptions) : IOfflineSyncService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly LigComRuntimeOptions runtime = runtimeOptions.Value;
    private readonly OfflineSyncOptions offline = offlineSyncOptions.Value;

    public async Task<OfflineSyncDashboard> GetDashboardAsync(Guid tenantId, string tenantName, CancellationToken cancellationToken = default)
    {
        var state = await ReadStateAsync(cancellationToken);
        var runtimeState = await runtimeInitializationStateService.GetStateAsync(cancellationToken);
        var warnings = BuildWarnings(runtimeState);

        return new OfflineSyncDashboard(
            tenantId,
            tenantName,
            runtimeState.RuntimeDisplayMode,
            runtime.DatabaseProvider.ToString(),
            offline.Enabled,
            offline.RequireManualTrigger,
            CanPush(runtimeState),
            CanPull(runtimeState),
            ResolveNodeId(),
            offline.CentralBaseUrl.Trim(),
            ResolveDatabaseTarget(),
            new OfflineSyncStateSnapshot(
                state.LastPushRequestedOnUtc,
                state.LastPullRequestedOnUtc,
                state.LastPushStatus,
                state.LastPullStatus,
                state.LastMessage),
            LocalCapabilities,
            CentralOnlyCapabilities,
            state.History
                .OrderByDescending(x => x.OccurredOnUtc)
                .Take(12)
                .Select(x => new OfflineSyncHistoryItem(
                    x.OccurredOnUtc,
                    x.Direction,
                    x.Status,
                    x.TriggeredBy,
                    x.Message,
                    x.Modules
                        .Select(module => new OfflineSyncModuleSummary(
                            module.Name,
                            module.Status,
                            module.Summary,
                            module.Notes))
                        .ToArray(),
                    x.Notes))
                .ToArray(),
            state.Conflicts
                .OrderBy(x => string.Equals(x.Status, "Open", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenByDescending(x => x.OccurredOnUtc)
                .Take(20)
                .Select(x => new OfflineSyncConflictItem(
                    x.Id,
                    x.OccurredOnUtc,
                    x.Direction,
                    x.ModuleName,
                    x.Severity,
                    x.Status,
                    x.Summary,
                    x.Notes,
                    x.ResolvedBy,
                    x.ResolvedOnUtc,
                    x.ResolutionNote))
                .ToArray(),
            warnings);
    }

    public Task<OfflineSyncExecutionResult> PushToCentralAsync(Guid tenantId, string triggeredBy, CancellationToken cancellationToken = default) =>
        ExecutePushAsync(tenantId, triggeredBy, cancellationToken);

    public Task<OfflineSyncExecutionResult> PullFromCentralAsync(Guid tenantId, string triggeredBy, CancellationToken cancellationToken = default) =>
        ExecutePullAsync(tenantId, triggeredBy, cancellationToken);

    public Task<OfflineSyncExecutionResult> RefreshLocalFromCentralAsync(Guid tenantId, string tenantSlug, string triggeredBy, CancellationToken cancellationToken = default) =>
        ExecutePullAsync(tenantId, triggeredBy, cancellationToken, fullReset: true, tenantSlug);

    public async Task<OfflineNodeBootstrapResult> BootstrapLocalNodeAsync(OfflineNodeBootstrapRequest request, CancellationToken cancellationToken = default)
    {
        if (runtime.Mode != LigComNodeMode.LocalNode)
        {
            return new OfflineNodeBootstrapResult(false, "Le bootstrap du noeud local n'est disponible qu'en mode LocalNode.", null, null, null, null);
        }

        if (runtime.DatabaseProvider != LigComDatabaseProvider.Sqlite)
        {
            return new OfflineNodeBootstrapResult(false, "Le bootstrap du noeud local est prevu pour une base SQLite.", null, null, null, null);
        }

        if (!offline.Enabled || !CanCallCentral())
        {
            return new OfflineNodeBootstrapResult(false, "La connexion de synchronisation vers le central n'est pas configuree.", null, null, ResolveNodeId(), request.AdminEmail);
        }

        if (string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            return new OfflineNodeBootstrapResult(false, "Le slug du tenant est obligatoire.", null, null, ResolveNodeId(), request.AdminEmail);
        }

        if (string.IsNullOrWhiteSpace(request.AdminEmail) || string.IsNullOrWhiteSpace(request.AdminPassword))
        {
            return new OfflineNodeBootstrapResult(false, "L'email et le mot de passe de l'administrateur local sont obligatoires.", null, null, ResolveNodeId(), request.AdminEmail);
        }

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        try
        {
            var payload = await TryReadBootstrapPackageAsync(request.TenantSlug.Trim(), cancellationToken);
            if (payload is null)
            {
                return new OfflineNodeBootstrapResult(false, "Le central n'a retourne aucun paquet d'initialisation.", null, null, ResolveNodeId(), request.AdminEmail.Trim());
            }

            var tenant = await UpsertLocalTenantAsync(payload, cancellationToken);
            await EnsureApplicationRolesAsync();
            await tenantAccessProfileService.EnsureStandardProfilesAsync(tenant.Id, cancellationToken);
            await EnsureLocalAdminUserAsync(tenant.Id, request, cancellationToken);

            return new OfflineNodeBootstrapResult(
                true,
                $"Le noeud local {ResolveNodeId()} est initialise pour {tenant.CompanyName}. L'administrateur local {request.AdminEmail.Trim()} peut maintenant se connecter.",
                tenant.Id,
                tenant.CompanyName,
                ResolveNodeId(),
                request.AdminEmail.Trim());
        }
        catch (Exception exception)
        {
            return new OfflineNodeBootstrapResult(false, $"Echec du bootstrap du noeud local : {exception.Message}", null, null, ResolveNodeId(), request.AdminEmail.Trim());
        }
    }

    private async Task<OfflineTenantBootstrapPackage?> TryReadBootstrapPackageAsync(string tenantSlug, CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateCentralClient();
            using var response = await client.GetAsync(
                $"/api/offline-sync/v1/bootstrap?tenantSlug={Uri.EscapeDataString(tenantSlug)}&nodeId={Uri.EscapeDataString(ResolveNodeId())}",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<OfflineTenantBootstrapPackage>(SerializerOptions, cancellationToken);
            }
        }
        catch when (hostEnvironment.IsDevelopment())
        {
            // Fallback below for single-instance development loops.
        }

        if (!hostEnvironment.IsDevelopment())
        {
            return null;
        }

        var defaultConnection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(defaultConnection))
        {
            return null;
        }

        var centralOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(defaultConnection)
            .Options;

        await using var centralContext = new ApplicationDbContext(centralOptions);
        var tenant = await centralContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == tenantSlug, cancellationToken);

        return tenant is null ? null : BuildBootstrapPackage(tenant);
    }

    private OfflineTenantBootstrapPackage BuildBootstrapPackage(Tenant tenant) =>
        new(
            tenant.Id,
            tenant.Slug,
            tenant.CompanyName,
            tenant.CompanyLegalName,
            tenant.PrimaryContactEmail,
            tenant.PhoneNumber,
            tenant.AddressLine1,
            tenant.AddressLine2,
            tenant.PostalCode,
            tenant.City,
            tenant.State,
            tenant.CountryCode,
            tenant.CurrencyCode,
            tenant.CashCurrencyCode,
            tenant.CurrencySymbol,
            tenant.CurrencySymbolPosition.ToString(),
            tenant.MoneyDecimalSeparator,
            tenant.MoneyGroupSeparator,
            tenant.MoneyDecimalPlaces,
            tenant.QuantityDecimalSeparator,
            tenant.QuantityGroupSeparator,
            tenant.QuantityDecimalPlaces,
            tenant.PaymentMethodsJson,
            tenant.PartnerLookupMode.ToString(),
            tenant.IncomingPaymentAllocationMode.ToString(),
            tenant.ReminderFriendlyDelayDays,
            tenant.ReminderFormalDelayDays,
            tenant.ReminderFinalNoticeDelayDays,
            tenant.BlockSalesOrdersOnCreditLimit,
            tenant.BlockSalesOrdersOnOverdue,
            tenant.BlockDeliveriesOnCreditLimit,
            tenant.BlockDeliveriesOnOverdue,
            tenant.AllowNegativeStock,
            tenant.DefaultStockValuationMethod.ToString(),
            tenant.VisualTheme.ToString(),
            tenant.IsActive,
            DateTime.UtcNow,
            ResolveNodeId());

    public async Task<bool> ResolveConflictAsync(
        Guid tenantId,
        Guid conflictId,
        string resolvedBy,
        string? resolutionNote,
        bool ignored,
        CancellationToken cancellationToken = default)
    {
        var state = await ReadStateAsync(cancellationToken);
        if (state.TenantId.HasValue && state.TenantId.Value != tenantId)
        {
            return false;
        }

        var conflict = state.Conflicts.FirstOrDefault(x => x.Id == conflictId);
        if (conflict is null)
        {
            return false;
        }

        conflict.Status = ignored ? "Ignored" : "Resolved";
        conflict.ResolvedBy = string.IsNullOrWhiteSpace(resolvedBy) ? "Utilisateur inconnu" : resolvedBy.Trim();
        conflict.ResolvedOnUtc = DateTime.UtcNow;
        conflict.ResolutionNote = string.IsNullOrWhiteSpace(resolutionNote)
            ? (ignored ? "Conflit ignore manuellement depuis LigCom." : "Conflit marque comme resolu manuellement depuis LigCom.")
            : resolutionNote.Trim();

        await WriteStateAsync(state, cancellationToken);
        return true;
    }

    private async Task<OfflineSyncExecutionResult> ExecutePushAsync(Guid tenantId, string triggeredBy, CancellationToken cancellationToken)
    {
        var runtimeState = await runtimeInitializationStateService.GetStateAsync(cancellationToken);

        if (!offline.Enabled)
        {
            return new OfflineSyncExecutionResult(false, "push", DateTime.UtcNow, "La base locale et la synchronisation ne sont pas activees dans la configuration.");
        }

        if (runtime.Mode != LigComNodeMode.LocalNode)
        {
            return new OfflineSyncExecutionResult(false, "push", DateTime.UtcNow, "Cette instance LigCom n'est pas demarree en mode noeud local.");
        }

        if (!CanPush(runtimeState))
        {
            return new OfflineSyncExecutionResult(false, "push", DateTime.UtcNow, runtimeState.IsReady
                ? "La synchronisation montante est desactivee."
                : "Le noeud local n'est pas encore initialise. Creez d'abord le tenant local et l'administrateur avant d'utiliser la synchronisation.");
        }

        if (!CanCallCentral())
        {
            return new OfflineSyncExecutionResult(false, "push", DateTime.UtcNow, "La connexion au central n'est pas configuree.");
        }

        var requestedOnUtc = DateTime.UtcNow;
        var state = await ReadStateAsync(cancellationToken);

        try
        {
            List<ModuleExecution> modules = [];
            var products = await dbContext.Products
                .AsNoTracking()
                .Include(x => x.ProductCategory)
                .Include(x => x.TaxCode)
                .Where(x => x.TenantId == tenantId)
                .OrderBy(x => x.Sku)
                .Select(x => new OfflineProductSyncItem(
                    x.Sku,
                    x.Label,
                    x.Description,
                    x.ProductType.ToString(),
                    x.UnitOfMeasure,
                    x.TrackStock,
                    x.StockValuationMethod.ToString(),
                    x.StockIdentityTrackingMode.ToString(),
                    x.IsActive,
                    x.ProductCategory != null ? x.ProductCategory.Code : null,
                    x.ProductCategory != null ? x.ProductCategory.Label : null,
                    x.TaxCode != null ? x.TaxCode.Code : null,
                    x.TaxCode != null ? x.TaxCode.Label : null,
                    x.TaxCode != null ? x.TaxCode.Rate : null,
                    x.PurchasePrice,
                    x.SalesPrice,
                    x.UpdatedOnUtc))
                .ToListAsync(cancellationToken);

            using var client = CreateCentralClient();
            using var response = await client.PostAsJsonAsync(
                "/api/offline-sync/v1/products/push",
                new OfflineProductPushRequest(tenantId, ResolveNodeId(), products),
                SerializerOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var failureMessage = $"Le central a refuse la synchronisation des articles ({(int)response.StatusCode}).";
                await UpdateStateAsync(state, tenantId, triggeredBy, "push", requestedOnUtc, "Failed", failureMessage, [new ModuleExecution("Articles", "Failed", failureMessage, [])], cancellationToken);
                return new OfflineSyncExecutionResult(false, "push", requestedOnUtc, failureMessage);
            }

            var payload = await response.Content.ReadFromJsonAsync<OfflineProductPushResponse>(SerializerOptions, cancellationToken);
            var productMessage = payload is null
                ? "Les articles locaux ont ete envoyes au central."
                : $"{payload.ReceivedCount} article(s) envoye(s), {payload.CreatedCount} cree(s), {payload.UpdatedCount} mis a jour, {payload.UnchangedCount} deja alignes.";
            modules.Add(new ModuleExecution("Articles", "Completed", productMessage, payload?.Notes ?? []));

            var documentResult = await PushDocumentsAsync(client, tenantId, cancellationToken);
            modules.Add(documentResult);
            var stockResult = await PushStockDocumentsAsync(client, tenantId, cancellationToken);
            modules.Add(stockResult);
            var paymentResult = await PushPaymentsAsync(client, tenantId, cancellationToken);
            modules.Add(paymentResult);
            var successMessage = $"{productMessage} {documentResult.Message} {stockResult.Message} {paymentResult.Message}".Trim();

            await UpdateStateAsync(state, tenantId, triggeredBy, "push", requestedOnUtc, "Completed", successMessage, modules, cancellationToken);
            return new OfflineSyncExecutionResult(true, "push", requestedOnUtc, successMessage);
        }
        catch (Exception exception)
        {
            var failureMessage = $"Echec de la synchronisation montante des articles : {exception.Message}";
            await UpdateStateAsync(state, tenantId, triggeredBy, "push", requestedOnUtc, "Failed", failureMessage, [new ModuleExecution("Execution", "Failed", failureMessage, [])], cancellationToken);
            return new OfflineSyncExecutionResult(false, "push", requestedOnUtc, failureMessage);
        }
    }

    private async Task<OfflineSyncExecutionResult> ExecutePullAsync(
        Guid tenantId,
        string triggeredBy,
        CancellationToken cancellationToken,
        bool fullReset = false,
        string? tenantSlug = null)
    {
        var runtimeState = await runtimeInitializationStateService.GetStateAsync(cancellationToken);

        if (!offline.Enabled)
        {
            return new OfflineSyncExecutionResult(false, "pull", DateTime.UtcNow, "La base locale et la synchronisation ne sont pas activees dans la configuration.");
        }

        if (runtime.Mode != LigComNodeMode.LocalNode)
        {
            return new OfflineSyncExecutionResult(false, "pull", DateTime.UtcNow, "Cette instance LigCom n'est pas demarree en mode noeud local.");
        }

        if (!CanPull(runtimeState))
        {
            return new OfflineSyncExecutionResult(false, "pull", DateTime.UtcNow, runtimeState.IsReady
                ? "La synchronisation descendante est desactivee."
                : "Le noeud local n'est pas encore initialise. Finalisez le bootstrap avant d'importer les donnees du central.");
        }

        if (!CanCallCentral())
        {
            return new OfflineSyncExecutionResult(false, "pull", DateTime.UtcNow, "La connexion au central n'est pas configuree.");
        }

        var requestedOnUtc = DateTime.UtcNow;
        var state = await ReadStateAsync(cancellationToken);

        try
        {
            List<ModuleExecution> modules = [];
            if (fullReset)
            {
                var effectiveTenantSlug = string.IsNullOrWhiteSpace(tenantSlug)
                    ? await ResolveTenantSlugAsync(tenantId, cancellationToken)
                    : tenantSlug.Trim();

                if (string.IsNullOrWhiteSpace(effectiveTenantSlug))
                {
                    var slugFailure = "Impossible de determiner le slug du tenant a recharger dans la base locale.";
                    await UpdateStateAsync(state, tenantId, triggeredBy, "pull", requestedOnUtc, "Failed", slugFailure, [new ModuleExecution("Reinitialisation locale", "Failed", slugFailure, [])], cancellationToken);
                    return new OfflineSyncExecutionResult(false, "pull", requestedOnUtc, slugFailure);
                }

                var bootstrapPackage = await TryReadBootstrapPackageAsync(effectiveTenantSlug, cancellationToken);
                if (bootstrapPackage is null)
                {
                    var bootstrapFailure = "Le central n'a retourne aucun paquet d'initialisation pour recharger la base locale.";
                    await UpdateStateAsync(state, tenantId, triggeredBy, "pull", requestedOnUtc, "Failed", bootstrapFailure, [new ModuleExecution("Reinitialisation locale", "Failed", bootstrapFailure, [])], cancellationToken);
                    return new OfflineSyncExecutionResult(false, "pull", requestedOnUtc, bootstrapFailure);
                }

                await UpsertLocalTenantAsync(bootstrapPackage, cancellationToken);
                var resetSummary = await ResetSynchronizedDataAsync(tenantId, cancellationToken);
                modules.Add(new ModuleExecution("Reinitialisation locale", "Completed", resetSummary, []));
            }

            var referenceDataSummary = await PullReferenceDataAsync(tenantId, cancellationToken);
            modules.Add(referenceDataSummary);

            var importSummary = await PullProductsAsync(tenantId, cancellationToken);
            modules.Add(importSummary);

            var documentSummary = await PullDocumentsAsync(tenantId, cancellationToken);
            modules.Add(documentSummary);

            var stockSummary = await PullStockDocumentsAsync(tenantId, cancellationToken);
            modules.Add(stockSummary);

            var paymentSummary = await PullPaymentsAsync(tenantId, cancellationToken);
            modules.Add(paymentSummary);

            var successMessage = string.Join(" ", modules.Select(x => x.Message).Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            await UpdateStateAsync(state, tenantId, triggeredBy, "pull", requestedOnUtc, "Completed", successMessage, modules, cancellationToken);
            return new OfflineSyncExecutionResult(true, "pull", requestedOnUtc, successMessage);
        }
        catch (Exception exception)
        {
            var failureMessage = $"Echec de la mise a niveau locale : {exception.Message}";
            await UpdateStateAsync(state, tenantId, triggeredBy, "pull", requestedOnUtc, "Failed", failureMessage, [new ModuleExecution("Execution", "Failed", failureMessage, [])], cancellationToken);
            return new OfflineSyncExecutionResult(false, "pull", requestedOnUtc, failureMessage);
        }
    }

    private bool CanPush(RuntimeInitializationState runtimeState) =>
        offline.Enabled
        && runtime.Mode == LigComNodeMode.LocalNode
        && runtimeState.IsReady
        && offline.AllowPush;

    private bool CanPull(RuntimeInitializationState runtimeState) =>
        offline.Enabled
        && runtime.Mode == LigComNodeMode.LocalNode
        && runtimeState.IsReady
        && offline.AllowPull;

    private bool CanCallCentral() =>
        !string.IsNullOrWhiteSpace(offline.CentralBaseUrl)
        && !string.IsNullOrWhiteSpace(offline.SharedAccessKey);

    private string ResolveDatabaseTarget()
    {
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            if (!string.IsNullOrWhiteSpace(connection.DataSource))
            {
                return connection.DataSource;
            }
        }
        catch
        {
            // Fallback below when the provider has not opened the connection yet.
        }

        return runtime.DatabaseProvider == LigComDatabaseProvider.Sqlite
            ? "SQLite local"
            : "SQL Server central";
    }

    private IReadOnlyList<string> BuildWarnings(RuntimeInitializationState runtimeState)
    {
        List<string> warnings = [];

        if (!offline.Enabled)
        {
            warnings.Add("La base locale et la synchronisation sont desactivees dans la configuration de cette instance.");
        }

        if (runtime.Mode != LigComNodeMode.LocalNode)
        {
            warnings.Add("L'instance active est en mode central. Les actions de synchronisation manuelle sont informatives uniquement.");
        }
        else if (!runtimeState.IsReady)
        {
            warnings.Add("Le noeud local SQLite est encore en preparation. Finalisez le bootstrap du tenant et la creation de l'administrateur local avant d'utiliser la base locale.");
        }

        if (runtime.Mode == LigComNodeMode.LocalNode && runtime.DatabaseProvider != LigComDatabaseProvider.Sqlite)
        {
            warnings.Add("Le mode noeud local est prevu pour SQLite. Le provider actuel ne correspond pas a l'architecture cible.");
        }

        if (offline.Enabled && string.IsNullOrWhiteSpace(offline.CentralBaseUrl))
        {
            warnings.Add("L'URL du serveur web central n'est pas encore renseignee.");
        }

        if (offline.Enabled && string.IsNullOrWhiteSpace(offline.SharedAccessKey))
        {
            warnings.Add("La cle d'acces partagee de synchronisation n'est pas encore renseignee.");
        }

        warnings.Add("Les rejets et conflits sont maintenant historises dans le journal de synchronisation.");

        return warnings;
    }

    private string GetStateFilePath()
    {
        var appDataPath = Path.Combine(hostEnvironment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataPath);
        var fileName = string.IsNullOrWhiteSpace(offline.StateFileName)
            ? "offline-sync-state.json"
            : offline.StateFileName.Trim();
        return Path.Combine(appDataPath, fileName);
    }

    private async Task<OfflineSyncStateStorage> ReadStateAsync(CancellationToken cancellationToken)
    {
        var path = GetStateFilePath();
        if (!File.Exists(path))
        {
            return new OfflineSyncStateStorage();
        }

        await using var stream = File.OpenRead(path);
        var state = await JsonSerializer.DeserializeAsync<OfflineSyncStateStorage>(stream, SerializerOptions, cancellationToken);
        return state ?? new OfflineSyncStateStorage();
    }

    private async Task WriteStateAsync(OfflineSyncStateStorage state, CancellationToken cancellationToken)
    {
        var path = GetStateFilePath();
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
    }

    private async Task UpdateStateAsync(
        OfflineSyncStateStorage state,
        Guid tenantId,
        string triggeredBy,
        string direction,
        DateTime requestedOnUtc,
        string status,
        string message,
        IReadOnlyList<ModuleExecution> modules,
        CancellationToken cancellationToken)
    {
        state.TenantId = tenantId;
        state.LastTriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "Utilisateur inconnu" : triggeredBy.Trim();
        state.LastMessage = message;

        if (string.Equals(direction, "push", StringComparison.OrdinalIgnoreCase))
        {
            state.LastPushRequestedOnUtc = requestedOnUtc;
            state.LastPushStatus = status;
        }
        else
        {
            state.LastPullRequestedOnUtc = requestedOnUtc;
            state.LastPullStatus = status;
        }

        var flattenedNotes = modules
            .SelectMany(module => module.Notes)
            .Where(note => !string.IsNullOrWhiteSpace(note))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToArray();

        state.History.Insert(0, new OfflineSyncHistoryStorage
        {
            OccurredOnUtc = requestedOnUtc,
            Direction = direction,
            Status = status,
            TriggeredBy = state.LastTriggeredBy,
            Message = message,
            Notes = flattenedNotes,
            Modules = modules
                .Select(module => new OfflineSyncModuleStorage
                {
                    Name = module.Name,
                    Status = module.Status,
                    Summary = module.Message,
                    Notes = module.Notes.Where(note => !string.IsNullOrWhiteSpace(note)).Take(20).ToArray()
                })
                .ToList()
        });

        var generatedConflicts = modules
            .Where(module =>
                !string.Equals(module.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                || module.Notes.Count > 0)
            .Select(module => new OfflineSyncConflictStorage
            {
                Id = Guid.NewGuid(),
                OccurredOnUtc = requestedOnUtc,
                Direction = direction,
                ModuleName = module.Name,
                Severity = string.Equals(module.Status, "Failed", StringComparison.OrdinalIgnoreCase) ? "High" : "Warning",
                Status = "Open",
                Summary = module.Message,
                Notes = module.Notes.Where(note => !string.IsNullOrWhiteSpace(note)).Take(20).ToArray()
            })
            .ToList();

        if (generatedConflicts.Count > 0)
        {
            state.Conflicts.InsertRange(0, generatedConflicts);
        }

        if (state.History.Count > 30)
        {
            state.History = state.History.Take(30).ToList();
        }

        if (state.Conflicts.Count > 80)
        {
            state.Conflicts = state.Conflicts.Take(80).ToList();
        }

        await WriteStateAsync(state, cancellationToken);
    }

    private async Task<string> ApplyIncomingProductsAsync(Guid tenantId, IReadOnlyList<OfflineProductSyncItem> products, CancellationToken cancellationToken)
    {
        var categories = await dbContext.ProductCategories
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var taxCodes = await dbContext.TaxCodes
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingProducts = await dbContext.Products
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Sku, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var created = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var item in products)
        {
            if (string.IsNullOrWhiteSpace(item.Sku))
            {
                continue;
            }

            var categoryId = ResolveLocalCategoryId(tenantId, item, categories);
            var taxCodeId = ResolveLocalTaxCodeId(tenantId, item, taxCodes);

            if (!existingProducts.TryGetValue(item.Sku.Trim().ToUpperInvariant(), out var product))
            {
                product = new Product { TenantId = tenantId };
                ApplyIncomingProduct(product, item, categoryId, taxCodeId);
                dbContext.Products.Add(product);
                existingProducts[product.Sku] = product;
                created++;
                continue;
            }

            if (ApplyIncomingProduct(product, item, categoryId, taxCodeId))
            {
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return $"{products.Count} article(s) recuperes du central, {created} cree(s), {updated} mis a jour, {unchanged} deja alignes.";
    }

    private Guid? ResolveLocalCategoryId(Guid tenantId, OfflineProductSyncItem item, Dictionary<string, ProductCategory> categories)
    {
        if (string.IsNullOrWhiteSpace(item.ProductCategoryCode))
        {
            return null;
        }

        var code = item.ProductCategoryCode.Trim().ToUpperInvariant();
        if (!categories.TryGetValue(code, out var category))
        {
            category = new ProductCategory
            {
                TenantId = tenantId,
                Code = code,
                Label = string.IsNullOrWhiteSpace(item.ProductCategoryLabel) ? code : item.ProductCategoryLabel.Trim()
            };
            dbContext.ProductCategories.Add(category);
            categories[code] = category;
        }
        else if (!string.IsNullOrWhiteSpace(item.ProductCategoryLabel) && !string.Equals(category.Label, item.ProductCategoryLabel.Trim(), StringComparison.Ordinal))
        {
            category.Label = item.ProductCategoryLabel.Trim();
        }

        return category.Id;
    }

    private Guid? ResolveLocalTaxCodeId(Guid tenantId, OfflineProductSyncItem item, Dictionary<string, TaxCode> taxCodes)
    {
        if (string.IsNullOrWhiteSpace(item.TaxCodeCode))
        {
            return null;
        }

        var code = item.TaxCodeCode.Trim().ToUpperInvariant();
        if (!taxCodes.TryGetValue(code, out var taxCode))
        {
            taxCode = new TaxCode
            {
                TenantId = tenantId,
                Code = code,
                Label = string.IsNullOrWhiteSpace(item.TaxCodeLabel) ? code : item.TaxCodeLabel.Trim(),
                Rate = item.TaxRate ?? 0m
            };
            dbContext.TaxCodes.Add(taxCode);
            taxCodes[code] = taxCode;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(item.TaxCodeLabel) && !string.Equals(taxCode.Label, item.TaxCodeLabel.Trim(), StringComparison.Ordinal))
            {
                taxCode.Label = item.TaxCodeLabel.Trim();
            }

            if (item.TaxRate.HasValue && taxCode.Rate != item.TaxRate.Value)
            {
                taxCode.Rate = item.TaxRate.Value;
            }
        }

        return taxCode.Id;
    }

    private static bool ApplyIncomingProduct(Product product, OfflineProductSyncItem item, Guid? categoryId, Guid? taxCodeId)
    {
        var changed = false;
        var normalizedSku = item.Sku.Trim().ToUpperInvariant();
        var normalizedLabel = item.Label.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim();
        var normalizedUnit = item.UnitOfMeasure.Trim().ToUpperInvariant();
        var productType = ParseEnum(item.ProductType, ProductType.StockItem);
        var valuationMethod = ParseEnum(item.StockValuationMethod, StockValuationMethod.Cmup);
        var trackingMode = ParseEnum(item.StockIdentityTrackingMode, StockIdentityTrackingMode.None);

        if (!string.Equals(product.Sku, normalizedSku, StringComparison.Ordinal))
        {
            product.Sku = normalizedSku;
            changed = true;
        }

        if (!string.Equals(product.Label, normalizedLabel, StringComparison.Ordinal))
        {
            product.Label = normalizedLabel;
            changed = true;
        }

        if (!string.Equals(product.Description, normalizedDescription, StringComparison.Ordinal))
        {
            product.Description = normalizedDescription;
            changed = true;
        }

        if (product.ProductType != productType)
        {
            product.ProductType = productType;
            changed = true;
        }

        if (!string.Equals(product.UnitOfMeasure, normalizedUnit, StringComparison.Ordinal))
        {
            product.UnitOfMeasure = normalizedUnit;
            changed = true;
        }

        if (product.TrackStock != item.TrackStock)
        {
            product.TrackStock = item.TrackStock;
            changed = true;
        }

        if (product.StockValuationMethod != valuationMethod)
        {
            product.StockValuationMethod = valuationMethod;
            changed = true;
        }

        if (product.StockIdentityTrackingMode != trackingMode)
        {
            product.StockIdentityTrackingMode = trackingMode;
            changed = true;
        }

        if (product.IsActive != item.IsActive)
        {
            product.IsActive = item.IsActive;
            changed = true;
        }

        if (product.ProductCategoryId != categoryId)
        {
            product.ProductCategoryId = categoryId;
            changed = true;
        }

        if (product.TaxCodeId != taxCodeId)
        {
            product.TaxCodeId = taxCodeId;
            changed = true;
        }

        if (product.PurchasePrice != item.PurchasePrice)
        {
            product.PurchasePrice = item.PurchasePrice;
            changed = true;
        }

        if (product.SalesPrice != item.SalesPrice)
        {
            product.SalesPrice = item.SalesPrice;
            changed = true;
        }

        return changed;
    }

    private HttpClient CreateCentralClient()
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(offline.CentralBaseUrl.Trim(), UriKind.Absolute);
        client.DefaultRequestHeaders.Add("X-LigCom-Offline-Key", offline.SharedAccessKey.Trim());
        return client;
    }

    private async Task<Tenant> UpsertLocalTenantAsync(OfflineTenantBootstrapPackage payload, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(x => x.Id == payload.TenantId || x.Slug == payload.TenantSlug, cancellationToken);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = payload.TenantId
            };
            dbContext.Tenants.Add(tenant);
        }

        tenant.Slug = payload.TenantSlug.Trim();
        tenant.CompanyName = payload.TenantName.Trim();
        tenant.CompanyLegalName = payload.CompanyLegalName.Trim();
        tenant.PrimaryContactEmail = payload.PrimaryContactEmail.Trim();
        tenant.PhoneNumber = payload.PhoneNumber.Trim();
        tenant.AddressLine1 = payload.AddressLine1.Trim();
        tenant.AddressLine2 = payload.AddressLine2.Trim();
        tenant.PostalCode = payload.PostalCode.Trim();
        tenant.City = payload.City.Trim();
        tenant.State = payload.State.Trim();
        tenant.CountryCode = payload.CountryCode.Trim();
        tenant.CurrencyCode = payload.CurrencyCode.Trim();
        tenant.CashCurrencyCode = payload.CashCurrencyCode.Trim();
        tenant.CurrencySymbol = payload.CurrencySymbol.Trim();
        tenant.CurrencySymbolPosition = ParseEnum(payload.CurrencySymbolPosition, CurrencySymbolPosition.BeforeAmount);
        tenant.MoneyDecimalSeparator = payload.MoneyDecimalSeparator;
        tenant.MoneyGroupSeparator = payload.MoneyGroupSeparator;
        tenant.MoneyDecimalPlaces = payload.MoneyDecimalPlaces;
        tenant.QuantityDecimalSeparator = payload.QuantityDecimalSeparator;
        tenant.QuantityGroupSeparator = payload.QuantityGroupSeparator;
        tenant.QuantityDecimalPlaces = payload.QuantityDecimalPlaces;
        tenant.PaymentMethodsJson = payload.PaymentMethodsJson;
        tenant.PartnerLookupMode = ParseEnum(payload.PartnerLookupMode, PartnerLookupMode.Code);
        tenant.IncomingPaymentAllocationMode = ParseEnum(payload.IncomingPaymentAllocationMode, PaymentAllocationMode.Manual);
        tenant.ReminderFriendlyDelayDays = payload.ReminderFriendlyDelayDays;
        tenant.ReminderFormalDelayDays = payload.ReminderFormalDelayDays;
        tenant.ReminderFinalNoticeDelayDays = payload.ReminderFinalNoticeDelayDays;
        tenant.BlockSalesOrdersOnCreditLimit = payload.BlockSalesOrdersOnCreditLimit;
        tenant.BlockSalesOrdersOnOverdue = payload.BlockSalesOrdersOnOverdue;
        tenant.BlockDeliveriesOnCreditLimit = payload.BlockDeliveriesOnCreditLimit;
        tenant.BlockDeliveriesOnOverdue = payload.BlockDeliveriesOnOverdue;
        tenant.AllowNegativeStock = payload.AllowNegativeStock;
        tenant.DefaultStockValuationMethod = ParseEnum(payload.DefaultStockValuationMethod, StockValuationMethod.Cmup);
        tenant.VisualTheme = ParseEnum(payload.VisualTheme, ApplicationTheme.LigComMidnight);
        tenant.IsActive = payload.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    private async Task EnsureApplicationRolesAsync()
    {
        foreach (var roleName in OfflineBootstrapRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }

    private async Task EnsureLocalAdminUserAsync(Guid tenantId, OfflineNodeBootstrapRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.AdminEmail.Trim();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                EmailConfirmed = true,
                FirstName = string.IsNullOrWhiteSpace(request.AdminFirstName) ? "Admin" : request.AdminFirstName.Trim(),
                LastName = string.IsNullOrWhiteSpace(request.AdminLastName) ? "LigCom" : request.AdminLastName.Trim(),
                TenantId = tenantId
            };

            var createResult = await userManager.CreateAsync(user, request.AdminPassword);
            if (!createResult.Succeeded)
            {
                throw new BusinessRuleException(
                    $"Impossible de creer l'administrateur local : {string.Join(", ", createResult.Errors.Select(x => x.Description))}",
                    errorCode: "OFFLINE_ADMIN_CREATE_FAILED");
            }
        }
        else
        {
            user.FirstName = string.IsNullOrWhiteSpace(request.AdminFirstName) ? user.FirstName : request.AdminFirstName.Trim();
            user.LastName = string.IsNullOrWhiteSpace(request.AdminLastName) ? user.LastName : request.AdminLastName.Trim();
            user.TenantId = tenantId;
            user.EmailConfirmed = true;

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                throw new BusinessRuleException(
                    $"Impossible de mettre a jour l'administrateur local : {string.Join(", ", updateResult.Errors.Select(x => x.Description))}",
                    errorCode: "OFFLINE_ADMIN_UPDATE_FAILED");
            }

            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await userManager.ResetPasswordAsync(user, resetToken, request.AdminPassword);
            if (!passwordResult.Succeeded)
            {
                throw new BusinessRuleException(
                    $"Impossible de definir le mot de passe de l'administrateur local : {string.Join(", ", passwordResult.Errors.Select(x => x.Description))}",
                    errorCode: "OFFLINE_ADMIN_PASSWORD_FAILED");
            }
        }

        if (!await userManager.IsInRoleAsync(user, "TenantOwner"))
        {
            var roleResult = await userManager.AddToRoleAsync(user, "TenantOwner");
            if (!roleResult.Succeeded)
            {
                throw new BusinessRuleException(
                    $"Impossible d'affecter le role administrateur tenant : {string.Join(", ", roleResult.Errors.Select(x => x.Description))}",
                    errorCode: "OFFLINE_ADMIN_ROLE_FAILED");
            }
        }
    }

    private string ResolveNodeId() =>
        string.IsNullOrWhiteSpace(offline.LocalNodeId)
            ? Environment.MachineName
            : offline.LocalNodeId.Trim();

    private async Task<string> ResolveTenantSlugAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.Slug)
            .FirstOrDefaultAsync(cancellationToken)
            ?? string.Empty;
    }

    private bool UseDevelopmentCentralFallback() =>
        hostEnvironment.IsDevelopment()
        && runtime.Mode == LigComNodeMode.LocalNode
        && runtime.DatabaseProvider == LigComDatabaseProvider.Sqlite
        && !string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection"));

    private ApplicationDbContext CreateCentralDbContext()
    {
        var defaultConnection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(defaultConnection))
        {
            throw new BusinessRuleException(
                "La connexion SQL Server centrale n'est pas configuree.",
                errorCode: "OFFLINE_CENTRAL_CONNECTION_MISSING");
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(defaultConnection)
            .Options;

        return new ApplicationDbContext(options);
    }

    private async Task<ModuleExecution> PullReferenceDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (UseDevelopmentCentralFallback())
        {
            await using var centralContext = CreateCentralDbContext();
            var payload = await BuildReferenceDataPullResponseAsync(centralContext, tenantId, cancellationToken);
            return new ModuleExecution("Referentiels serveur (lecture seule)", "Completed", await ApplyIncomingReferenceDataAsync(tenantId, payload, cancellationToken), []);
        }

        using var client = CreateCentralClient();
        using var response = await client.GetAsync(
            $"/api/offline-sync/v1/reference-data/pull?tenantId={tenantId}&nodeId={Uri.EscapeDataString(ResolveNodeId())}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ModuleExecution("Referentiels serveur (lecture seule)", "Failed", $"Les referentiels n'ont pas pu etre telecharges depuis le central ({(int)response.StatusCode}).", []);
        }

        var payloadFromHttp = await response.Content.ReadFromJsonAsync<OfflineReferenceDataPullResponse>(SerializerOptions, cancellationToken);
        if (payloadFromHttp is null)
        {
            return new ModuleExecution("Referentiels serveur (lecture seule)", "Completed", "Aucun referentiel central a importer.", []);
        }

        return new ModuleExecution("Referentiels serveur (lecture seule)", "Completed", await ApplyIncomingReferenceDataAsync(tenantId, payloadFromHttp, cancellationToken), []);
    }

    private async Task<ModuleExecution> PullProductsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (UseDevelopmentCentralFallback())
        {
            await using var centralContext = CreateCentralDbContext();
            var centralPayload = await BuildProductPullResponseAsync(centralContext, tenantId, cancellationToken);
            return new ModuleExecution("Articles", "Completed", await ApplyIncomingProductsAsync(tenantId, centralPayload.Products, cancellationToken), []);
        }

        using var client = CreateCentralClient();
        using var response = await client.GetAsync(
            $"/api/offline-sync/v1/products/pull?tenantId={tenantId}&nodeId={Uri.EscapeDataString(ResolveNodeId())}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ModuleExecution("Articles", "Failed", $"Les articles n'ont pas pu etre telecharges depuis le central ({(int)response.StatusCode}).", []);
        }

        var payload = await response.Content.ReadFromJsonAsync<OfflineProductPullResponse>(SerializerOptions, cancellationToken);
        if (payload is null)
        {
            return new ModuleExecution("Articles", "Completed", "Aucun article central a importer.", []);
        }

        return new ModuleExecution("Articles", "Completed", await ApplyIncomingProductsAsync(tenantId, payload.Products, cancellationToken), []);
    }

    private async Task<ModuleExecution> PullDocumentsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (UseDevelopmentCentralFallback())
        {
            await using var centralContext = CreateCentralDbContext();
            var payload = await BuildCommercialDocumentPullResponseAsync(centralContext, tenantId, cancellationToken);
            return new ModuleExecution("Documents commerciaux", "Completed", await ApplyIncomingDocumentsAsync(tenantId, payload.Documents, cancellationToken), []);
        }

        using var client = CreateCentralClient();
        return await PullDocumentsAsync(client, tenantId, cancellationToken);
    }

    private async Task<ModuleExecution> PullStockDocumentsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (UseDevelopmentCentralFallback())
        {
            await using var centralContext = CreateCentralDbContext();
            var payload = await BuildStockDocumentPullResponseAsync(centralContext, tenantId, cancellationToken);
            return new ModuleExecution("Documents de stock", "Completed", await ApplyIncomingStockDocumentsAsync(tenantId, payload.Documents, cancellationToken), []);
        }

        using var client = CreateCentralClient();
        return await PullStockDocumentsAsync(client, tenantId, cancellationToken);
    }

    private async Task<ModuleExecution> PullPaymentsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (UseDevelopmentCentralFallback())
        {
            await using var centralContext = CreateCentralDbContext();
            var payload = await BuildPaymentPullResponseAsync(centralContext, tenantId, cancellationToken);
            return new ModuleExecution("Reglements et acomptes", "Completed", await ApplyIncomingPaymentsAsync(tenantId, payload.Payments, cancellationToken), []);
        }

        using var client = CreateCentralClient();
        return await PullPaymentsAsync(client, tenantId, cancellationToken);
    }

    private async Task<string> ApplyIncomingReferenceDataAsync(Guid tenantId, OfflineReferenceDataPullResponse payload, CancellationToken cancellationToken)
    {
        var paymentTerms = await dbContext.PaymentTerms
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var categories = await dbContext.ProductCategories
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var taxCodes = await dbContext.TaxCodes
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var warehouses = await dbContext.Warehouses
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var documentSequences = await dbContext.DocumentSequences
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.DocumentType, cancellationToken);

        var referenceNumberingSettings = await dbContext.ReferenceNumberingSettings
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Scope, cancellationToken);

        var journalAccounts = await dbContext.JournalAccounts
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var partners = await dbContext.BusinessPartners
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var paymentTermCreated = 0;
        var paymentTermUpdated = 0;
        var categoryCreated = 0;
        var categoryUpdated = 0;
        var taxCreated = 0;
        var taxUpdated = 0;
        var warehouseCreated = 0;
        var warehouseUpdated = 0;
        var partnerCreated = 0;
        var partnerUpdated = 0;
        var sequenceCreated = 0;
        var sequenceUpdated = 0;
        var numberingCreated = 0;
        var numberingUpdated = 0;
        var journalCreated = 0;
        var journalUpdated = 0;

        foreach (var item in payload.PaymentTerms)
        {
            var code = item.Code.Trim().ToUpperInvariant();
            if (!paymentTerms.TryGetValue(code, out var paymentTerm))
            {
                paymentTerm = new PaymentTerm
                {
                    TenantId = tenantId,
                    Code = code
                };

                dbContext.PaymentTerms.Add(paymentTerm);
                paymentTerms[code] = paymentTerm;
                paymentTermCreated++;
            }
            else
            {
                paymentTermUpdated += ApplyPaymentTerm(paymentTerm, item) ? 1 : 0;
                continue;
            }

            ApplyPaymentTerm(paymentTerm, item);
        }

        foreach (var item in payload.ProductCategories)
        {
            var code = item.Code.Trim().ToUpperInvariant();
            if (!categories.TryGetValue(code, out var category))
            {
                category = new ProductCategory
                {
                    TenantId = tenantId,
                    Code = code
                };

                dbContext.ProductCategories.Add(category);
                categories[code] = category;
                categoryCreated++;
            }
            else
            {
                categoryUpdated += ApplyProductCategory(category, item) ? 1 : 0;
                continue;
            }

            ApplyProductCategory(category, item);
        }

        foreach (var item in payload.TaxCodes)
        {
            var code = item.Code.Trim().ToUpperInvariant();
            if (!taxCodes.TryGetValue(code, out var taxCode))
            {
                taxCode = new TaxCode
                {
                    TenantId = tenantId,
                    Code = code
                };

                dbContext.TaxCodes.Add(taxCode);
                taxCodes[code] = taxCode;
                taxCreated++;
            }
            else
            {
                taxUpdated += ApplyTaxCode(taxCode, item) ? 1 : 0;
                continue;
            }

            ApplyTaxCode(taxCode, item);
        }

        foreach (var item in payload.Warehouses)
        {
            var code = item.Code.Trim().ToUpperInvariant();
            if (!warehouses.TryGetValue(code, out var warehouse))
            {
                warehouse = new Warehouse
                {
                    TenantId = tenantId,
                    Code = code
                };

                dbContext.Warehouses.Add(warehouse);
                warehouses[code] = warehouse;
                warehouseCreated++;
            }
            else
            {
                warehouseUpdated += ApplyWarehouse(warehouse, item) ? 1 : 0;
                continue;
            }

            ApplyWarehouse(warehouse, item);
        }

        foreach (var item in payload.DocumentSequences)
        {
            var documentType = ParseEnum(item.DocumentType, CommercialDocumentType.SalesQuote);
            if (!documentSequences.TryGetValue(documentType, out var sequence))
            {
                sequence = new DocumentSequence
                {
                    TenantId = tenantId,
                    DocumentType = documentType
                };

                dbContext.DocumentSequences.Add(sequence);
                documentSequences[documentType] = sequence;
                sequenceCreated++;
            }
            else
            {
                sequenceUpdated += ApplyDocumentSequence(sequence, item) ? 1 : 0;
                continue;
            }

            ApplyDocumentSequence(sequence, item);
        }

        foreach (var item in payload.ReferenceNumberingSettings)
        {
            var scope = ParseEnum(item.Scope, ReferenceNumberingScope.Customer);
            if (!referenceNumberingSettings.TryGetValue(scope, out var setting))
            {
                setting = new ReferenceNumberingSetting
                {
                    TenantId = tenantId,
                    Scope = scope
                };

                dbContext.ReferenceNumberingSettings.Add(setting);
                referenceNumberingSettings[scope] = setting;
                numberingCreated++;
            }
            else
            {
                numberingUpdated += ApplyReferenceNumberingSetting(setting, item) ? 1 : 0;
                continue;
            }

            ApplyReferenceNumberingSetting(setting, item);
        }

        foreach (var item in payload.JournalAccounts)
        {
            var code = item.Code.Trim().ToUpperInvariant();
            if (!journalAccounts.TryGetValue(code, out var journal))
            {
                journal = new JournalAccount
                {
                    TenantId = tenantId,
                    Code = code
                };

                dbContext.JournalAccounts.Add(journal);
                journalAccounts[code] = journal;
                journalCreated++;
            }
            else
            {
                journalUpdated += ApplyJournalAccount(journal, item) ? 1 : 0;
                continue;
            }

            ApplyJournalAccount(journal, item);
        }

        foreach (var item in payload.Partners)
        {
            var code = item.Code.Trim().ToUpperInvariant();
            Guid? paymentTermId = null;
            if (!string.IsNullOrWhiteSpace(item.PaymentTermCode)
                && paymentTerms.TryGetValue(item.PaymentTermCode.Trim().ToUpperInvariant(), out var paymentTerm))
            {
                paymentTermId = paymentTerm.Id;
            }

            if (!partners.TryGetValue(code, out var partner))
            {
                partner = new BusinessPartner
                {
                    TenantId = tenantId,
                    Code = code
                };

                dbContext.BusinessPartners.Add(partner);
                partners[code] = partner;
                partnerCreated++;
            }
            else
            {
                partnerUpdated += ApplyBusinessPartner(partner, item, paymentTermId) ? 1 : 0;
                continue;
            }

            ApplyBusinessPartner(partner, item, paymentTermId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var summary = string.Join(", ", new[]
        {
            $"{payload.PaymentTerms.Count} condition(s) de reglement",
            $"{payload.ProductCategories.Count} famille(s)",
            $"{payload.TaxCodes.Count} taxe(s)",
            $"{payload.Warehouses.Count} depot(s)",
            $"{payload.Partners.Count} tiers",
            $"{payload.DocumentSequences.Count} sequence(s)",
            $"{payload.ReferenceNumberingSettings.Count} numerotation(s) referentiel",
            $"{payload.JournalAccounts.Count} journal(aux)"
        });

        return $"Referentiels recuperes du central: {summary}. Cree(s)/maj: conditions {paymentTermCreated}/{paymentTermUpdated}, familles {categoryCreated}/{categoryUpdated}, taxes {taxCreated}/{taxUpdated}, depots {warehouseCreated}/{warehouseUpdated}, tiers {partnerCreated}/{partnerUpdated}, sequences {sequenceCreated}/{sequenceUpdated}, referentiels {numberingCreated}/{numberingUpdated}, journaux {journalCreated}/{journalUpdated}.";
    }

    private static bool ApplyPaymentTerm(PaymentTerm entity, OfflinePaymentTermSyncItem item)
    {
        var changed = false;
        var label = item.Label.Trim();

        if (!string.Equals(entity.Label, label, StringComparison.Ordinal))
        {
            entity.Label = label;
            changed = true;
        }

        if (entity.DueInDays != item.DueInDays)
        {
            entity.DueInDays = item.DueInDays;
            changed = true;
        }

        return changed;
    }

    private static bool ApplyProductCategory(ProductCategory entity, OfflineProductCategorySyncItem item)
    {
        var changed = false;
        var label = item.Label.Trim();
        var valuationMethod = ParseEnum(item.StockValuationMethod, StockValuationMethod.Cmup);
        var trackingMode = ParseEnum(item.StockIdentityTrackingMode, StockIdentityTrackingMode.None);

        if (!string.Equals(entity.Label, label, StringComparison.Ordinal))
        {
            entity.Label = label;
            changed = true;
        }

        if (entity.StockValuationMethod != valuationMethod)
        {
            entity.StockValuationMethod = valuationMethod;
            changed = true;
        }

        if (entity.StockIdentityTrackingMode != trackingMode)
        {
            entity.StockIdentityTrackingMode = trackingMode;
            changed = true;
        }

        return changed;
    }

    private static bool ApplyTaxCode(TaxCode entity, OfflineTaxCodeSyncItem item)
    {
        var changed = false;
        var label = item.Label.Trim();

        if (!string.Equals(entity.Label, label, StringComparison.Ordinal))
        {
            entity.Label = label;
            changed = true;
        }

        if (entity.Rate != item.Rate)
        {
            entity.Rate = item.Rate;
            changed = true;
        }

        return changed;
    }

    private static bool ApplyWarehouse(Warehouse entity, OfflineWarehouseSyncItem item)
    {
        var changed = false;
        var label = item.Label.Trim();

        if (!string.Equals(entity.Label, label, StringComparison.Ordinal))
        {
            entity.Label = label;
            changed = true;
        }

        if (entity.IsDefault != item.IsDefault)
        {
            entity.IsDefault = item.IsDefault;
            changed = true;
        }

        return changed;
    }

    private static bool ApplyBusinessPartner(BusinessPartner entity, OfflineBusinessPartnerSyncItem item, Guid? paymentTermId)
    {
        var changed = false;
        var name = item.Name.Trim();
        var partnerType = ParseEnum(item.PartnerType, BusinessPartnerType.Customer);
        var email = string.IsNullOrWhiteSpace(item.Email) ? null : item.Email.Trim();
        var phoneNumber = string.IsNullOrWhiteSpace(item.PhoneNumber) ? null : item.PhoneNumber.Trim();
        var vatNumber = string.IsNullOrWhiteSpace(item.VatNumber) ? null : item.VatNumber.Trim();

        if (!string.Equals(entity.Name, name, StringComparison.Ordinal))
        {
            entity.Name = name;
            changed = true;
        }

        if (entity.PartnerType != partnerType)
        {
            entity.PartnerType = partnerType;
            changed = true;
        }

        if (!string.Equals(entity.Email, email, StringComparison.Ordinal))
        {
            entity.Email = email;
            changed = true;
        }

        if (!string.Equals(entity.PhoneNumber, phoneNumber, StringComparison.Ordinal))
        {
            entity.PhoneNumber = phoneNumber;
            changed = true;
        }

        if (!string.Equals(entity.VatNumber, vatNumber, StringComparison.Ordinal))
        {
            entity.VatNumber = vatNumber;
            changed = true;
        }

        if (entity.CreditLimit != item.CreditLimit)
        {
            entity.CreditLimit = item.CreditLimit;
            changed = true;
        }

        if (entity.IsActive != item.IsActive)
        {
            entity.IsActive = item.IsActive;
            changed = true;
        }

        if (entity.PaymentTermId != paymentTermId)
        {
            entity.PaymentTermId = paymentTermId;
            changed = true;
        }

        changed |= ApplyAddress(entity.BillingAddress, item.BillingAddress);
        changed |= ApplyAddress(entity.ShippingAddress, item.ShippingAddress);
        return changed;
    }

    private static bool ApplyAddress(Address entity, OfflineAddressSyncItem item)
    {
        var changed = false;
        changed |= ApplyText(entity.Recipient, item.Recipient, value => entity.Recipient = value);
        changed |= ApplyText(entity.StreetLine1, item.StreetLine1, value => entity.StreetLine1 = value);
        changed |= ApplyText(entity.StreetLine2, item.StreetLine2, value => entity.StreetLine2 = value);
        changed |= ApplyText(entity.PostalCode, item.PostalCode, value => entity.PostalCode = value);
        changed |= ApplyText(entity.City, item.City, value => entity.City = value);
        changed |= ApplyText(entity.State, item.State, value => entity.State = value);
        changed |= ApplyText(entity.Country, item.Country, value => entity.Country = value);
        return changed;
    }

    private static bool ApplyText(string? currentValue, string? nextValue, Action<string?> apply)
    {
        var normalized = string.IsNullOrWhiteSpace(nextValue) ? null : nextValue.Trim();
        if (string.Equals(currentValue, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        apply(normalized);
        return true;
    }

    private static bool ApplyDocumentSequence(DocumentSequence entity, OfflineDocumentSequenceSyncItem item)
    {
        var changed = false;
        var prefix = item.Prefix.Trim();
        var mode = ParseEnum(item.Mode, NumberingMode.AutomaticWithPrefix);

        if (entity.Mode != mode)
        {
            entity.Mode = mode;
            changed = true;
        }

        if (!string.Equals(entity.Prefix, prefix, StringComparison.Ordinal))
        {
            entity.Prefix = prefix;
            changed = true;
        }

        if (entity.NumberLength != item.NumberLength)
        {
            entity.NumberLength = item.NumberLength;
            changed = true;
        }

        if (entity.NextValue != item.NextValue)
        {
            entity.NextValue = item.NextValue;
            changed = true;
        }

        return changed;
    }

    private static bool ApplyReferenceNumberingSetting(ReferenceNumberingSetting entity, OfflineReferenceNumberingSettingSyncItem item)
    {
        var changed = false;
        var prefix = item.Prefix.Trim();
        var mode = ParseEnum(item.Mode, NumberingMode.AutomaticWithPrefix);

        if (entity.Mode != mode)
        {
            entity.Mode = mode;
            changed = true;
        }

        if (!string.Equals(entity.Prefix, prefix, StringComparison.Ordinal))
        {
            entity.Prefix = prefix;
            changed = true;
        }

        if (entity.NumberLength != item.NumberLength)
        {
            entity.NumberLength = item.NumberLength;
            changed = true;
        }

        if (entity.NextValue != item.NextValue)
        {
            entity.NextValue = item.NextValue;
            changed = true;
        }

        return changed;
    }

    private static bool ApplyJournalAccount(JournalAccount entity, OfflineJournalAccountSyncItem item)
    {
        var changed = false;
        var label = item.Label.Trim();
        var counterpart = string.IsNullOrWhiteSpace(item.CounterpartAccountCode) ? null : item.CounterpartAccountCode.Trim().ToUpperInvariant();

        if (!string.Equals(entity.Label, label, StringComparison.Ordinal))
        {
            entity.Label = label;
            changed = true;
        }

        if (!string.Equals(entity.CounterpartAccountCode, counterpart, StringComparison.Ordinal))
        {
            entity.CounterpartAccountCode = counterpart;
            changed = true;
        }

        return changed;
    }

    private async Task<string> ResetSynchronizedDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var paymentCount = await dbContext.Payments.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var documentCount = await dbContext.CommercialDocuments.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var stockDocumentCount = await dbContext.StockDocuments.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var productCount = await dbContext.Products.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var partnerCount = await dbContext.BusinessPartners.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var warehouseCount = await dbContext.Warehouses.CountAsync(x => x.TenantId == tenantId, cancellationToken);

        await dbContext.CommercialDocuments
            .Where(x => x.TenantId == tenantId && x.SourceDocumentId != null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SourceDocumentId, (Guid?)null), cancellationToken);

        await dbContext.Payments
            .Where(x => x.TenantId == tenantId && x.SourceCommercialDocumentId != null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SourceCommercialDocumentId, (Guid?)null), cancellationToken);

        await dbContext.PaymentAllocations
            .Where(x => x.Payment != null && x.Payment.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ReminderLogs.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.StockMovements.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.StockDocumentLines.Where(x => x.StockDocument != null && x.StockDocument.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.StockDocuments.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.CommercialDocumentLines.Where(x => x.CommercialDocument != null && x.CommercialDocument.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.Payments.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.CommercialDocuments.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.PriceListLines.Where(x => x.PriceList != null && x.PriceList.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.PriceLists.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.DocumentSequences.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ReferenceNumberingSettings.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.JournalAccounts.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.Products.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ProductCategories.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.TaxCodes.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.Warehouses.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.BusinessPartners.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.PaymentTerms.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);

        return $"{paymentCount} reglement(s), {documentCount} document(s), {stockDocumentCount} document(s) de stock, {productCount} article(s), {partnerCount} tiers et {warehouseCount} depot(s) locaux ont ete purges avant rechargement complet.";
    }

    private async Task<OfflineReferenceDataPullResponse> BuildReferenceDataPullResponseAsync(ApplicationDbContext sourceContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var paymentTerms = await sourceContext.PaymentTerms
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new OfflinePaymentTermSyncItem(
                x.Code,
                x.Label,
                x.DueInDays))
            .ToListAsync(cancellationToken);

        var productCategories = await sourceContext.ProductCategories
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new OfflineProductCategorySyncItem(
                x.Code,
                x.Label,
                x.StockValuationMethod.ToString(),
                x.StockIdentityTrackingMode.ToString()))
            .ToListAsync(cancellationToken);

        var taxCodes = await sourceContext.TaxCodes
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new OfflineTaxCodeSyncItem(
                x.Code,
                x.Label,
                x.Rate))
            .ToListAsync(cancellationToken);

        var warehouses = await sourceContext.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new OfflineWarehouseSyncItem(
                x.Code,
                x.Label,
                x.IsDefault))
            .ToListAsync(cancellationToken);

        var partners = await sourceContext.BusinessPartners
            .AsNoTracking()
            .Include(x => x.PaymentTerm)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new OfflineBusinessPartnerSyncItem(
                x.Code,
                x.Name,
                x.PartnerType.ToString(),
                x.Email,
                x.PhoneNumber,
                x.VatNumber,
                x.CreditLimit,
                x.IsActive,
                x.PaymentTerm != null ? x.PaymentTerm.Code : null,
                new OfflineAddressSyncItem(
                    x.BillingAddress.Recipient,
                    x.BillingAddress.StreetLine1,
                    x.BillingAddress.StreetLine2,
                    x.BillingAddress.PostalCode,
                    x.BillingAddress.City,
                    x.BillingAddress.State,
                    x.BillingAddress.Country),
                new OfflineAddressSyncItem(
                    x.ShippingAddress.Recipient,
                    x.ShippingAddress.StreetLine1,
                    x.ShippingAddress.StreetLine2,
                    x.ShippingAddress.PostalCode,
                    x.ShippingAddress.City,
                    x.ShippingAddress.State,
                    x.ShippingAddress.Country),
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        var documentSequences = await sourceContext.DocumentSequences
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.DocumentType)
            .Select(x => new OfflineDocumentSequenceSyncItem(
                x.DocumentType.ToString(),
                x.Mode.ToString(),
                x.Prefix,
                x.NumberLength,
                x.NextValue))
            .ToListAsync(cancellationToken);

        var referenceNumberingSettings = await sourceContext.ReferenceNumberingSettings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Scope)
            .Select(x => new OfflineReferenceNumberingSettingSyncItem(
                x.Scope.ToString(),
                x.Mode.ToString(),
                x.Prefix,
                x.NumberLength,
                x.NextValue))
            .ToListAsync(cancellationToken);

        var journalAccounts = await sourceContext.JournalAccounts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new OfflineJournalAccountSyncItem(
                x.Code,
                x.Label,
                x.CounterpartAccountCode))
            .ToListAsync(cancellationToken);

        return new OfflineReferenceDataPullResponse(
            tenantId,
            ResolveNodeId(),
            DateTime.UtcNow,
            paymentTerms,
            productCategories,
            taxCodes,
            warehouses,
            partners,
            documentSequences,
            referenceNumberingSettings,
            journalAccounts);
    }

    private async Task<OfflineProductPullResponse> BuildProductPullResponseAsync(ApplicationDbContext sourceContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var products = await sourceContext.Products
            .AsNoTracking()
            .Include(x => x.ProductCategory)
            .Include(x => x.TaxCode)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Sku)
            .Select(x => new OfflineProductSyncItem(
                x.Sku,
                x.Label,
                x.Description,
                x.ProductType.ToString(),
                x.UnitOfMeasure,
                x.TrackStock,
                x.StockValuationMethod.ToString(),
                x.StockIdentityTrackingMode.ToString(),
                x.IsActive,
                x.ProductCategory != null ? x.ProductCategory.Code : null,
                x.ProductCategory != null ? x.ProductCategory.Label : null,
                x.TaxCode != null ? x.TaxCode.Code : null,
                x.TaxCode != null ? x.TaxCode.Label : null,
                x.TaxCode != null ? x.TaxCode.Rate : null,
                x.PurchasePrice,
                x.SalesPrice,
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        return new OfflineProductPullResponse(tenantId, ResolveNodeId(), DateTime.UtcNow, products);
    }

    private async Task<OfflineCommercialDocumentPullResponse> BuildCommercialDocumentPullResponseAsync(ApplicationDbContext sourceContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var documents = await sourceContext.CommercialDocuments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.Warehouse)
            .Include(x => x.SourceDocument)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Number)
            .Select(x => new OfflineCommercialDocumentSyncItem(
                x.Number,
                x.DocumentType.ToString(),
                x.Status.ToString(),
                x.DocumentDate,
                x.DueDate,
                x.CurrencyCode,
                x.Partner != null ? x.Partner.Code : string.Empty,
                x.Warehouse != null ? x.Warehouse.Code : null,
                x.Notes,
                x.PaymentStatus.ToString(),
                x.PaidAmount,
                x.BalanceAmount,
                x.InDispute,
                x.PromiseToPayDate,
                x.SourceDocument != null ? x.SourceDocument.Number : null,
                x.Lines
                    .OrderBy(line => line.CreatedOnUtc)
                    .Select(line => new OfflineCommercialDocumentLineSyncItem(
                        line.Product != null ? line.Product.Sku : null,
                        line.Description,
                        line.Quantity,
                        line.UnitPriceExcludingTax,
                        line.DiscountRate,
                        line.TaxRate,
                        line.LotNumber,
                        line.SerialNumber,
                        line.ExpirationDate))
                    .ToList(),
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        return new OfflineCommercialDocumentPullResponse(tenantId, ResolveNodeId(), DateTime.UtcNow, documents);
    }

    private async Task<OfflineStockDocumentPullResponse> BuildStockDocumentPullResponseAsync(ApplicationDbContext sourceContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var documents = await sourceContext.StockDocuments
            .AsNoTracking()
            .Include(x => x.SourceWarehouse)
            .Include(x => x.DestinationWarehouse)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Number)
            .Select(x => new OfflineStockDocumentSyncItem(
                x.Number,
                x.DocumentType.ToString(),
                x.Status.ToString(),
                x.DocumentDate,
                x.SourceWarehouse != null ? x.SourceWarehouse.Code : null,
                x.DestinationWarehouse != null ? x.DestinationWarehouse.Code : null,
                x.Notes,
                x.PostedOnUtc,
                x.Lines
                    .OrderBy(line => line.CreatedOnUtc)
                    .Select(line => new OfflineStockDocumentLineSyncItem(
                        line.Product != null ? line.Product.Sku : null,
                        line.Description,
                        line.Quantity,
                        line.UnitCost,
                        line.LotNumber,
                        line.SerialNumber,
                        line.ExpirationDate))
                    .ToList(),
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        return new OfflineStockDocumentPullResponse(tenantId, ResolveNodeId(), DateTime.UtcNow, documents);
    }

    private async Task<OfflinePaymentPullResponse> BuildPaymentPullResponseAsync(ApplicationDbContext sourceContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var payments = await sourceContext.Payments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.SourceCommercialDocument)
            .Include(x => x.Allocations)
                .ThenInclude(x => x.CommercialDocument)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.PaymentDate)
            .ThenBy(x => x.ReferenceNumber)
            .Select(x => new OfflinePaymentSyncItem(
                x.PaymentDate,
                x.Direction.ToString(),
                x.Type.ToString(),
                x.Method.ToString(),
                x.AllocationStatus.ToString(),
                x.ReferenceNumber,
                x.CurrencyCode,
                x.Amount,
                x.AllocatedAmount,
                x.AvailableAmount,
                x.Notes,
                x.Partner != null ? x.Partner.Code : string.Empty,
                x.SourceCommercialDocument != null ? x.SourceCommercialDocument.Number : null,
                x.Allocations
                    .OrderBy(a => a.AllocatedOnUtc)
                    .Select(a => new OfflinePaymentAllocationSyncItem(
                        a.CommercialDocument != null ? a.CommercialDocument.Number : string.Empty,
                        a.AllocatedAmount,
                        a.AllocatedOnUtc,
                        a.Notes))
                    .ToList(),
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        return new OfflinePaymentPullResponse(tenantId, ResolveNodeId(), DateTime.UtcNow, payments);
    }

    private async Task<ModuleExecution> PushDocumentsAsync(HttpClient client, Guid tenantId, CancellationToken cancellationToken)
    {
        var documents = await dbContext.CommercialDocuments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.Warehouse)
            .Include(x => x.SourceDocument)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Number)
            .Select(x => new OfflineCommercialDocumentSyncItem(
                x.Number,
                x.DocumentType.ToString(),
                x.Status.ToString(),
                x.DocumentDate,
                x.DueDate,
                x.CurrencyCode,
                x.Partner != null ? x.Partner.Code : string.Empty,
                x.Warehouse != null ? x.Warehouse.Code : null,
                x.Notes,
                x.PaymentStatus.ToString(),
                x.PaidAmount,
                x.BalanceAmount,
                x.InDispute,
                x.PromiseToPayDate,
                x.SourceDocument != null ? x.SourceDocument.Number : null,
                x.Lines
                    .OrderBy(line => line.CreatedOnUtc)
                    .Select(line => new OfflineCommercialDocumentLineSyncItem(
                        line.Product != null ? line.Product.Sku : null,
                        line.Description,
                        line.Quantity,
                        line.UnitPriceExcludingTax,
                        line.DiscountRate,
                        line.TaxRate,
                        line.LotNumber,
                        line.SerialNumber,
                        line.ExpirationDate))
                    .ToList(),
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        using var response = await client.PostAsJsonAsync(
            "/api/offline-sync/v1/documents/push",
            new OfflineCommercialDocumentPushRequest(tenantId, ResolveNodeId(), documents),
            SerializerOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ModuleExecution("Documents commerciaux", "Failed", $"Les documents n'ont pas pu etre synchronises ({(int)response.StatusCode}).", []);
        }

        var payload = await response.Content.ReadFromJsonAsync<OfflineCommercialDocumentPushResponse>(SerializerOptions, cancellationToken);
        var message = payload is null
            ? "Les documents locaux ont ete envoyes au central."
            : $"{payload.ReceivedCount} document(s) envoye(s), {payload.CreatedCount} cree(s), {payload.UpdatedCount} mis a jour, {payload.UnchangedCount} deja alignes, {payload.SkippedCount} ignore(s).";
        return new ModuleExecution("Documents commerciaux", "Completed", message, payload?.Notes ?? []);
    }

    private async Task<ModuleExecution> PullDocumentsAsync(HttpClient client, Guid tenantId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"/api/offline-sync/v1/documents/pull?tenantId={tenantId}&nodeId={Uri.EscapeDataString(ResolveNodeId())}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ModuleExecution("Documents commerciaux", "Failed", $"Les documents n'ont pas pu etre telecharges depuis le central ({(int)response.StatusCode}).", []);
        }

        var payload = await response.Content.ReadFromJsonAsync<OfflineCommercialDocumentPullResponse>(SerializerOptions, cancellationToken);
        if (payload is null)
        {
            return new ModuleExecution("Documents commerciaux", "Completed", "Aucun document central a importer.", []);
        }

        return new ModuleExecution("Documents commerciaux", "Completed", await ApplyIncomingDocumentsAsync(tenantId, payload.Documents, cancellationToken), []);
    }

    private async Task<ModuleExecution> PushStockDocumentsAsync(HttpClient client, Guid tenantId, CancellationToken cancellationToken)
    {
        var documents = await dbContext.StockDocuments
            .AsNoTracking()
            .Include(x => x.SourceWarehouse)
            .Include(x => x.DestinationWarehouse)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Number)
            .Select(x => new OfflineStockDocumentSyncItem(
                x.Number,
                x.DocumentType.ToString(),
                x.Status.ToString(),
                x.DocumentDate,
                x.SourceWarehouse != null ? x.SourceWarehouse.Code : null,
                x.DestinationWarehouse != null ? x.DestinationWarehouse.Code : null,
                x.Notes,
                x.PostedOnUtc,
                x.Lines
                    .OrderBy(line => line.CreatedOnUtc)
                    .Select(line => new OfflineStockDocumentLineSyncItem(
                        line.Product != null ? line.Product.Sku : null,
                        line.Description,
                        line.Quantity,
                        line.UnitCost,
                        line.LotNumber,
                        line.SerialNumber,
                        line.ExpirationDate))
                    .ToList(),
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        using var response = await client.PostAsJsonAsync(
            "/api/offline-sync/v1/stock-documents/push",
            new OfflineStockDocumentPushRequest(tenantId, ResolveNodeId(), documents),
            SerializerOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ModuleExecution("Documents de stock", "Failed", $"Les documents de stock n'ont pas pu etre synchronises ({(int)response.StatusCode}).", []);
        }

        var payload = await response.Content.ReadFromJsonAsync<OfflineStockDocumentPushResponse>(SerializerOptions, cancellationToken);
        var message = payload is null
            ? "Les documents de stock locaux ont ete envoyes au central."
            : $"{payload.ReceivedCount} document(s) de stock envoye(s), {payload.CreatedCount} cree(s), {payload.UpdatedCount} mis a jour, {payload.PostedCount} valide(s), {payload.UnchangedCount} deja alignes, {payload.SkippedCount} ignore(s).";
        return new ModuleExecution("Documents de stock", "Completed", message, payload?.Notes ?? []);
    }

    private async Task<ModuleExecution> PullStockDocumentsAsync(HttpClient client, Guid tenantId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"/api/offline-sync/v1/stock-documents/pull?tenantId={tenantId}&nodeId={Uri.EscapeDataString(ResolveNodeId())}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ModuleExecution("Documents de stock", "Failed", $"Les documents de stock n'ont pas pu etre telecharges depuis le central ({(int)response.StatusCode}).", []);
        }

        var payload = await response.Content.ReadFromJsonAsync<OfflineStockDocumentPullResponse>(SerializerOptions, cancellationToken);
        if (payload is null)
        {
            return new ModuleExecution("Documents de stock", "Completed", "Aucun document de stock central a importer.", []);
        }

        return new ModuleExecution("Documents de stock", "Completed", await ApplyIncomingStockDocumentsAsync(tenantId, payload.Documents, cancellationToken), []);
    }

    private async Task<ModuleExecution> PushPaymentsAsync(HttpClient client, Guid tenantId, CancellationToken cancellationToken)
    {
        var payments = await dbContext.Payments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.SourceCommercialDocument)
            .Include(x => x.Allocations)
                .ThenInclude(x => x.CommercialDocument)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.PaymentDate)
            .ThenBy(x => x.ReferenceNumber)
            .Select(x => new OfflinePaymentSyncItem(
                x.PaymentDate,
                x.Direction.ToString(),
                x.Type.ToString(),
                x.Method.ToString(),
                x.AllocationStatus.ToString(),
                x.ReferenceNumber,
                x.CurrencyCode,
                x.Amount,
                x.AllocatedAmount,
                x.AvailableAmount,
                x.Notes,
                x.Partner != null ? x.Partner.Code : string.Empty,
                x.SourceCommercialDocument != null ? x.SourceCommercialDocument.Number : null,
                x.Allocations
                    .OrderBy(a => a.AllocatedOnUtc)
                    .Select(a => new OfflinePaymentAllocationSyncItem(
                        a.CommercialDocument != null ? a.CommercialDocument.Number : string.Empty,
                        a.AllocatedAmount,
                        a.AllocatedOnUtc,
                        a.Notes))
                    .ToList(),
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        using var response = await client.PostAsJsonAsync(
            "/api/offline-sync/v1/payments/push",
            new OfflinePaymentPushRequest(tenantId, ResolveNodeId(), payments),
            SerializerOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ModuleExecution("Reglements et acomptes", "Failed", $"Les reglements et acomptes n'ont pas pu etre synchronises ({(int)response.StatusCode}).", []);
        }

        var payload = await response.Content.ReadFromJsonAsync<OfflinePaymentPushResponse>(SerializerOptions, cancellationToken);
        var message = payload is null
            ? "Les reglements et acomptes locaux ont ete envoyes au central."
            : $"{payload.ReceivedCount} reglement(s)/acompte(s) envoye(s), {payload.CreatedCount} cree(s), {payload.UpdatedCount} mis a jour, {payload.UnchangedCount} deja alignes, {payload.AllocationRefreshCount} imputation(s) rafraichie(s), {payload.SkippedCount} ignore(s).";
        return new ModuleExecution("Reglements et acomptes", "Completed", message, payload?.Notes ?? []);
    }

    private async Task<ModuleExecution> PullPaymentsAsync(HttpClient client, Guid tenantId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"/api/offline-sync/v1/payments/pull?tenantId={tenantId}&nodeId={Uri.EscapeDataString(ResolveNodeId())}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ModuleExecution("Reglements et acomptes", "Failed", $"Les reglements et acomptes n'ont pas pu etre telecharges depuis le central ({(int)response.StatusCode}).", []);
        }

        var payload = await response.Content.ReadFromJsonAsync<OfflinePaymentPullResponse>(SerializerOptions, cancellationToken);
        if (payload is null)
        {
            return new ModuleExecution("Reglements et acomptes", "Completed", "Aucun reglement central a importer.", []);
        }

        return new ModuleExecution("Reglements et acomptes", "Completed", await ApplyIncomingPaymentsAsync(tenantId, payload.Payments, cancellationToken), []);
    }

    private static TEnum ParseEnum<TEnum>(string rawValue, TEnum fallback)
        where TEnum : struct
        => Enum.TryParse<TEnum>(rawValue, true, out var parsed) ? parsed : fallback;

    private async Task<string> ApplyIncomingPaymentsAsync(Guid tenantId, IReadOnlyList<OfflinePaymentSyncItem> payments, CancellationToken cancellationToken)
    {
        var created = 0;
        var updated = 0;
        var unchanged = 0;
        var allocationRefreshCount = 0;
        var skipped = 0;

        foreach (var item in payments)
        {
            if (string.IsNullOrWhiteSpace(item.ReferenceNumber) || string.IsNullOrWhiteSpace(item.PartnerCode))
            {
                skipped++;
                continue;
            }

            try
            {
                var result = await settlementService.UpsertOfflinePaymentAsync(tenantId, item, cancellationToken);
                await settlementService.ReplaceOfflineAllocationsAsync(tenantId, result.PaymentId, item.Allocations, cancellationToken);

                if (result.Created)
                {
                    created++;
                }
                else if (result.Updated)
                {
                    updated++;
                }
                else
                {
                    unchanged++;
                }

                allocationRefreshCount += item.Allocations.Count;
            }
            catch
            {
                skipped++;
            }
        }

        return $"{payments.Count} reglement(s)/acompte(s) recuperes du central, {created} cree(s), {updated} mis a jour, {unchanged} deja alignes, {allocationRefreshCount} imputation(s) appliquee(s), {skipped} ignore(s).";
    }

    private async Task<string> ApplyIncomingStockDocumentsAsync(Guid tenantId, IReadOnlyList<OfflineStockDocumentSyncItem> documents, CancellationToken cancellationToken)
    {
        var warehouses = await dbContext.Warehouses
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var products = await dbContext.Products
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Sku, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingDocuments = await dbContext.StockDocuments
            .Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Number, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var created = 0;
        var updated = 0;
        var posted = 0;
        var unchanged = 0;
        var skipped = 0;

        foreach (var item in documents)
        {
            if (!TryResolveStockDependencies(item, warehouses, products, out var resolution))
            {
                skipped++;
                continue;
            }

            var documentNumber = item.Number.Trim().ToUpperInvariant();
            if (!existingDocuments.TryGetValue(documentNumber, out var document))
            {
                document = new StockDocument
                {
                    TenantId = tenantId,
                    Number = documentNumber
                };

                ApplyIncomingStockHeader(document, item, resolution);
                ReplaceIncomingStockLines(document, item, products);
                dbContext.StockDocuments.Add(document);
                existingDocuments[document.Number] = document;
                created++;

                if (ParseEnum(item.Status, StockDocumentStatus.Draft) == StockDocumentStatus.Posted)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await stockDocumentService.PostAsync(tenantId, document.Id, cancellationToken);
                    posted++;
                }

                continue;
            }

            if (document.Status == StockDocumentStatus.Posted)
            {
                unchanged++;
                continue;
            }

            var changed = ApplyIncomingStockHeader(document, item, resolution);
            changed |= ReplaceIncomingStockLines(document, item, products);

            var targetStatus = ParseEnum(item.Status, StockDocumentStatus.Draft);
            if (targetStatus == StockDocumentStatus.Posted)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await stockDocumentService.PostAsync(tenantId, document.Id, cancellationToken);
                posted++;
                continue;
            }

            if (changed)
            {
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return $"{documents.Count} document(s) de stock recuperes du central, {created} cree(s), {updated} mis a jour, {posted} valide(s), {unchanged} deja alignes, {skipped} ignores.";
    }

    private static bool TryResolveStockDependencies(
        OfflineStockDocumentSyncItem item,
        IReadOnlyDictionary<string, Warehouse> warehouses,
        IReadOnlyDictionary<string, Product> products,
        out ResolvedStockDocumentDependencies resolution)
    {
        resolution = default;

        Warehouse? sourceWarehouse = null;
        if (!string.IsNullOrWhiteSpace(item.SourceWarehouseCode))
        {
            var sourceCode = item.SourceWarehouseCode.Trim().ToUpperInvariant();
            if (!warehouses.TryGetValue(sourceCode, out sourceWarehouse))
            {
                return false;
            }
        }

        Warehouse? destinationWarehouse = null;
        if (!string.IsNullOrWhiteSpace(item.DestinationWarehouseCode))
        {
            var destinationCode = item.DestinationWarehouseCode.Trim().ToUpperInvariant();
            if (!warehouses.TryGetValue(destinationCode, out destinationWarehouse))
            {
                return false;
            }
        }

        foreach (var line in item.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ProductSku))
            {
                continue;
            }

            var sku = line.ProductSku.Trim().ToUpperInvariant();
            if (!products.ContainsKey(sku))
            {
                return false;
            }
        }

        resolution = new ResolvedStockDocumentDependencies(sourceWarehouse?.Id, destinationWarehouse?.Id);
        return true;
    }

    private static bool ApplyIncomingStockHeader(
        StockDocument document,
        OfflineStockDocumentSyncItem item,
        ResolvedStockDocumentDependencies resolution)
    {
        var changed = false;
        var documentType = ParseEnum(item.DocumentType, StockDocumentType.Entry);
        var status = ParseEnum(item.Status, StockDocumentStatus.Draft);
        var notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();

        if (document.DocumentType != documentType)
        {
            document.DocumentType = documentType;
            changed = true;
        }

        if (document.Status != status)
        {
            document.Status = status == StockDocumentStatus.Posted ? StockDocumentStatus.Draft : status;
            changed = true;
        }

        if (document.DocumentDate != item.DocumentDate)
        {
            document.DocumentDate = item.DocumentDate;
            changed = true;
        }

        if (document.SourceWarehouseId != resolution.SourceWarehouseId)
        {
            document.SourceWarehouseId = resolution.SourceWarehouseId;
            changed = true;
        }

        if (document.DestinationWarehouseId != resolution.DestinationWarehouseId)
        {
            document.DestinationWarehouseId = resolution.DestinationWarehouseId;
            changed = true;
        }

        if (!string.Equals(document.Notes, notes, StringComparison.Ordinal))
        {
            document.Notes = notes;
            changed = true;
        }

        return changed;
    }

    private static bool ReplaceIncomingStockLines(
        StockDocument document,
        OfflineStockDocumentSyncItem item,
        IReadOnlyDictionary<string, Product> products)
    {
        var changed = true;
        document.Lines.Clear();

        foreach (var line in item.Lines)
        {
            Guid? productId = null;
            if (!string.IsNullOrWhiteSpace(line.ProductSku))
            {
                var sku = line.ProductSku.Trim().ToUpperInvariant();
                if (products.TryGetValue(sku, out var product))
                {
                    productId = product.Id;
                }
            }

            document.Lines.Add(new StockDocumentLine
            {
                ProductId = productId,
                Description = line.Description.Trim(),
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                LotNumber = string.IsNullOrWhiteSpace(line.LotNumber) ? null : line.LotNumber.Trim().ToUpperInvariant(),
                SerialNumber = string.IsNullOrWhiteSpace(line.SerialNumber) ? null : line.SerialNumber.Trim().ToUpperInvariant(),
                ExpirationDate = line.ExpirationDate
            });
        }

        return changed;
    }

    private async Task<string> ApplyIncomingDocumentsAsync(Guid tenantId, IReadOnlyList<OfflineCommercialDocumentSyncItem> documents, CancellationToken cancellationToken)
    {
        var partners = await dbContext.BusinessPartners
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var warehouses = await dbContext.Warehouses
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var products = await dbContext.Products
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Sku, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingDocuments = await dbContext.CommercialDocuments
            .Include(x => x.Lines)
            .Include(x => x.PaymentAllocations)
            .Include(x => x.DerivedDocuments)
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.Number, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var created = 0;
        var updated = 0;
        var unchanged = 0;
        var skipped = 0;

        foreach (var item in documents)
        {
            if (!TryResolveDocumentDependencies(item, partners, warehouses, products, out var resolution))
            {
                skipped++;
                continue;
            }

            if (!existingDocuments.TryGetValue(item.Number.Trim().ToUpperInvariant(), out var document))
            {
                document = new CommercialDocument
                {
                    TenantId = tenantId,
                    Number = item.Number.Trim().ToUpperInvariant()
                };

                ApplyIncomingDocumentHeader(document, item, resolution);
                ReplaceDocumentLines(document, item, products);
                commercialDocumentWorkflowService.RecalculateTotals(document);
                ApplyFinancialFlags(document, item);
                dbContext.CommercialDocuments.Add(document);
                existingDocuments[document.Number] = document;
                created++;
                continue;
            }

            if (document.PaymentAllocations.Count > 0 || document.DerivedDocuments.Count > 0)
            {
                skipped++;
                continue;
            }

            var changed = ApplyIncomingDocumentHeader(document, item, resolution);
            changed |= ReplaceDocumentLines(document, item, products);
            commercialDocumentWorkflowService.RecalculateTotals(document);
            changed |= ApplyFinancialFlags(document, item);

            if (changed)
            {
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return $"{documents.Count} document(s) recuperes du central, {created} cree(s), {updated} mis a jour, {unchanged} deja alignes, {skipped} ignores.";
    }

    private static bool TryResolveDocumentDependencies(
        OfflineCommercialDocumentSyncItem item,
        IReadOnlyDictionary<string, GescomSaas.Domain.Entities.Commercial.BusinessPartner> partners,
        IReadOnlyDictionary<string, Warehouse> warehouses,
        IReadOnlyDictionary<string, Product> products,
        out ResolvedDocumentDependencies resolution)
    {
        resolution = default;

        if (string.IsNullOrWhiteSpace(item.PartnerCode))
        {
            return false;
        }

        var partnerCode = item.PartnerCode.Trim().ToUpperInvariant();
        if (!partners.TryGetValue(partnerCode, out var partner))
        {
            return false;
        }

        Warehouse? warehouse = null;
        if (!string.IsNullOrWhiteSpace(item.WarehouseCode))
        {
            var warehouseCode = item.WarehouseCode.Trim().ToUpperInvariant();
            if (!warehouses.TryGetValue(warehouseCode, out warehouse))
            {
                return false;
            }
        }

        foreach (var line in item.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ProductSku))
            {
                continue;
            }

            var sku = line.ProductSku.Trim().ToUpperInvariant();
            if (!products.ContainsKey(sku))
            {
                return false;
            }
        }

        resolution = new ResolvedDocumentDependencies(partner.Id, warehouse?.Id);
        return true;
    }

    private static bool ApplyIncomingDocumentHeader(
        CommercialDocument document,
        OfflineCommercialDocumentSyncItem item,
        ResolvedDocumentDependencies resolution)
    {
        var changed = false;
        var documentType = ParseEnum(item.DocumentType, CommercialDocumentType.SalesQuote);
        var status = ParseEnum(item.Status, CommercialDocumentStatus.Draft);
        var currencyCode = string.IsNullOrWhiteSpace(item.CurrencyCode) ? "CAD" : item.CurrencyCode.Trim().ToUpperInvariant();
        var notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();

        if (document.DocumentType != documentType)
        {
            document.DocumentType = documentType;
            changed = true;
        }

        if (document.Status != status)
        {
            document.Status = status;
            changed = true;
        }

        if (document.DocumentDate != item.DocumentDate)
        {
            document.DocumentDate = item.DocumentDate;
            changed = true;
        }

        if (document.DueDate != item.DueDate)
        {
            document.DueDate = item.DueDate;
            changed = true;
        }

        if (!string.Equals(document.CurrencyCode, currencyCode, StringComparison.Ordinal))
        {
            document.CurrencyCode = currencyCode;
            changed = true;
        }

        if (document.PartnerId != resolution.PartnerId)
        {
            document.PartnerId = resolution.PartnerId;
            changed = true;
        }

        if (document.WarehouseId != resolution.WarehouseId)
        {
            document.WarehouseId = resolution.WarehouseId;
            changed = true;
        }

        if (!string.Equals(document.Notes, notes, StringComparison.Ordinal))
        {
            document.Notes = notes;
            changed = true;
        }

        return changed;
    }

    private static bool ReplaceDocumentLines(
        CommercialDocument document,
        OfflineCommercialDocumentSyncItem item,
        IReadOnlyDictionary<string, Product> products)
    {
        var changed = document.Lines.Count != item.Lines.Count;
        document.Lines.Clear();

        foreach (var line in item.Lines)
        {
            Guid? productId = null;
            if (!string.IsNullOrWhiteSpace(line.ProductSku))
            {
                var sku = line.ProductSku.Trim().ToUpperInvariant();
                if (products.TryGetValue(sku, out var product))
                {
                    productId = product.Id;
                }
            }

            document.Lines.Add(new CommercialDocumentLine
            {
                ProductId = productId,
                Description = line.Description.Trim(),
                Quantity = line.Quantity,
                UnitPriceExcludingTax = line.UnitPriceExcludingTax,
                DiscountRate = line.DiscountRate,
                TaxRate = line.TaxRate,
                LotNumber = string.IsNullOrWhiteSpace(line.LotNumber) ? null : line.LotNumber.Trim().ToUpperInvariant(),
                SerialNumber = string.IsNullOrWhiteSpace(line.SerialNumber) ? null : line.SerialNumber.Trim().ToUpperInvariant(),
                ExpirationDate = line.ExpirationDate
            });
        }

        return changed || item.Lines.Count > 0;
    }

    private static bool ApplyFinancialFlags(CommercialDocument document, OfflineCommercialDocumentSyncItem item)
    {
        var changed = false;
        var paymentStatus = ParseEnum(item.PaymentStatus, CommercialPaymentStatus.Unpaid);

        if (document.PaymentStatus != paymentStatus)
        {
            document.PaymentStatus = paymentStatus;
            changed = true;
        }

        if (document.PaidAmount != item.PaidAmount)
        {
            document.PaidAmount = item.PaidAmount;
            changed = true;
        }

        if (document.BalanceAmount != item.BalanceAmount)
        {
            document.BalanceAmount = item.BalanceAmount;
            changed = true;
        }

        if (document.InDispute != item.InDispute)
        {
            document.InDispute = item.InDispute;
            changed = true;
        }

        if (document.PromiseToPayDate != item.PromiseToPayDate)
        {
            document.PromiseToPayDate = item.PromiseToPayDate;
            changed = true;
        }

        return changed;
    }

    private static readonly IReadOnlyList<string> LocalCapabilities =
    [
        "Articles",
        "Documents de vente",
        "Documents d'achat",
        "Documents de stock hors saisie d'inventaire",
        "Reglements, acomptes et imputations"
    ];

    private static readonly IReadOnlyList<string> CentralOnlyCapabilities =
    [
        "Referentiels",
        "Parametrage",
        "Imports Sage SQL",
        "Saisie d'inventaire",
        "Gestion des utilisateurs"
    ];

    private static readonly IReadOnlyList<string> OfflineBootstrapRoles =
    [
        "PlatformAdmin",
        "TenantOwner",
        "SalesManager",
        "PurchasingManager",
        "FinanceManager",
        "InventoryManager"
    ];

    private sealed record ModuleExecution(string Name, string Status, string Message, IReadOnlyList<string> Notes);
    private readonly record struct ResolvedDocumentDependencies(Guid PartnerId, Guid? WarehouseId);
    private readonly record struct ResolvedStockDocumentDependencies(Guid? SourceWarehouseId, Guid? DestinationWarehouseId);

    private sealed class OfflineSyncStateStorage
    {
        public Guid? TenantId { get; set; }
        public string LastTriggeredBy { get; set; } = string.Empty;
        public DateTime? LastPushRequestedOnUtc { get; set; }
        public DateTime? LastPullRequestedOnUtc { get; set; }
        public string LastPushStatus { get; set; } = "NotStarted";
        public string LastPullStatus { get; set; } = "NotStarted";
        public string LastMessage { get; set; } = "Aucune synchronisation manuelle n'a encore ete demandee.";
        public List<OfflineSyncHistoryStorage> History { get; set; } = [];
        public List<OfflineSyncConflictStorage> Conflicts { get; set; } = [];
    }

    private sealed class OfflineSyncHistoryStorage
    {
        public DateTime OccurredOnUtc { get; set; }
        public string Direction { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string TriggeredBy { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string[] Notes { get; set; } = [];
        public List<OfflineSyncModuleStorage> Modules { get; set; } = [];
    }

    private sealed class OfflineSyncModuleStorage
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string[] Notes { get; set; } = [];
    }

    private sealed class OfflineSyncConflictStorage
    {
        public Guid Id { get; set; }
        public DateTime OccurredOnUtc { get; set; }
        public string Direction { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public string Summary { get; set; } = string.Empty;
        public string[] Notes { get; set; } = [];
        public string? ResolvedBy { get; set; }
        public DateTime? ResolvedOnUtc { get; set; }
        public string? ResolutionNote { get; set; }
    }
}
