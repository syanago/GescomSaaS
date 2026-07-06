using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GescomSaas.Web.Pages.Settings;

public class OfflineModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    IOfflineSyncService offlineSyncService,
    IHostEnvironment hostEnvironment,
    IOptions<LigComRuntimeOptions> runtimeOptions,
    IOptions<OfflineSyncOptions> offlineSyncOptions) : SettingsPageModel(dbContext, currentTenantAccessor, userPermissionService, runtimeOptions)
{
    private readonly LigComRuntimeOptions runtime = runtimeOptions.Value;
    private readonly OfflineSyncOptions offline = offlineSyncOptions.Value;

    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.SettingsOfflineSyncManage];

    public OfflineSyncDashboard? Dashboard { get; private set; }
    public IReadOnlyList<OfflineSyncHistoryItem> FilteredHistory { get; private set; } = [];
    public IReadOnlyList<OfflineSyncConflictItem> FilteredConflicts { get; private set; } = [];
    public IReadOnlyList<string> AvailableModules { get; private set; } = [];
    public string CurrentSqlitePath { get; private set; } = string.Empty;
    public bool RestartRequired { get; private set; }
    public string CurrentTenantSlug { get; private set; } = string.Empty;
    public string CurrentCentralBaseUrl => offline.CentralBaseUrl;
    public string CurrentLocalNodeId => string.IsNullOrWhiteSpace(offline.LocalNodeId) ? Environment.MachineName : offline.LocalNodeId.Trim();
    public bool IsLocalNodeMode => runtime.Mode == LigComNodeMode.LocalNode;
    public bool IsSqliteProvider => runtime.DatabaseProvider == LigComDatabaseProvider.Sqlite;
    public bool IsOfflineConfigured => offline.Enabled && !string.IsNullOrWhiteSpace(offline.CentralBaseUrl) && !string.IsNullOrWhiteSpace(offline.SharedAccessKey);
    public bool IsConnectedMode => !IsLocalNodeMode || !IsSqliteProvider;

    [BindProperty(SupportsGet = true)]
    public string Search { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string Direction { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public string Module { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public string ConflictStatus { get; set; } = "open";

    [BindProperty]
    public string SqliteDatabasePath { get; set; } = string.Empty;

    [BindProperty]
    public string BootstrapTenantSlug { get; set; } = string.Empty;

    [BindProperty]
    public string BootstrapAdminEmail { get; set; } = string.Empty;

    [BindProperty]
    public string BootstrapAdminPassword { get; set; } = string.Empty;

    [BindProperty]
    public string BootstrapAdminFirstName { get; set; } = string.Empty;

    [BindProperty]
    public string BootstrapAdminLastName { get; set; } = string.Empty;

    [BindProperty]
    public string ActivationCentralBaseUrl { get; set; } = string.Empty;

    [BindProperty]
    public string ActivationSharedAccessKey { get; set; } = string.Empty;

    [BindProperty]
    public string ActivationLocalNodeId { get; set; } = string.Empty;

    [BindProperty]
    public bool SyncEnabled { get; set; }

    [BindProperty]
    public string SyncCentralBaseUrl { get; set; } = string.Empty;

    [BindProperty]
    public string SyncSharedAccessKey { get; set; } = string.Empty;

    [BindProperty]
    public string SyncLocalNodeId { get; set; } = string.Empty;

    public bool IsSyncEnabled => offline.Enabled;

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostSaveSyncSettingsAsync()
    {
        if (SyncEnabled && string.IsNullOrWhiteSpace(SyncCentralBaseUrl))
        {
            StatusMessage = "L'URL du serveur central est obligatoire pour activer la synchronisation.";
            return RedirectToPage(routeValues: BuildRouteValues());
        }

        var effectiveKey = string.IsNullOrWhiteSpace(SyncSharedAccessKey) ? offline.SharedAccessKey : SyncSharedAccessKey;
        if (SyncEnabled && string.IsNullOrWhiteSpace(effectiveKey))
        {
            StatusMessage = "La cle API partagee est obligatoire pour activer la synchronisation.";
            return RedirectToPage(routeValues: BuildRouteValues());
        }

        await LocalRuntimeSettingsStore.SaveSyncSettingsAsync(
            hostEnvironment,
            runtime.Mode,
            runtime.DatabaseProvider,
            runtime.SqliteDatabasePath,
            SyncEnabled,
            SyncCentralBaseUrl,
            SyncSharedAccessKey,
            SyncLocalNodeId,
            HttpContext.RequestAborted);

        RestartRequired = true;
        StatusMessage = SyncEnabled
            ? "Synchronisation activee et configuree. Redemarrez LigCom pour l'appliquer."
            : "Synchronisation desactivee. Redemarrez LigCom pour l'appliquer.";
        return RedirectToPage(routeValues: BuildRouteValues());
    }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (!context.HttpContext.User.IsInRole("PlatformAdmin") && !context.HttpContext.User.IsInRole("TenantOwner"))
        {
            context.Result = Forbid();
            return;
        }

        await base.OnPageHandlerExecutionAsync(context, next);
    }

    public async Task<IActionResult> OnPostPushAsync()
    {
        var tenant = await LoadTenantAsync();
        var result = await offlineSyncService.PushToCentralAsync(
            tenant.Id,
            User.Identity?.Name ?? User.Identity?.AuthenticationType ?? "Utilisateur LigCom",
            HttpContext.RequestAborted);

        StatusMessage = result.Message;
        return RedirectToPage(routeValues: BuildRouteValues());
    }

    public async Task<IActionResult> OnPostActivateLocalNodeAsync()
    {
        await LoadAsync();

        var requestedPath = NormalizeSqlitePath(SqliteDatabasePath);
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            StatusMessage = "Le chemin de la base SQLite est obligatoire.";
            return RedirectToPage(routeValues: BuildRouteValues());
        }

        if (string.IsNullOrWhiteSpace(ActivationCentralBaseUrl) || string.IsNullOrWhiteSpace(ActivationSharedAccessKey))
        {
            StatusMessage = "L'URL du central et la cle partagee sont obligatoires pour activer le noeud local.";
            return RedirectToPage(routeValues: BuildRouteValues());
        }

        var localNodeId = string.IsNullOrWhiteSpace(ActivationLocalNodeId)
            ? Environment.MachineName
            : ActivationLocalNodeId.Trim();

        await LocalRuntimeSettingsStore.SaveLocalNodeActivationAsync(
            hostEnvironment,
            requestedPath,
            ActivationCentralBaseUrl,
            ActivationSharedAccessKey,
            localNodeId,
            HttpContext.RequestAborted);

        RestartRequired = true;
        StatusMessage = "La base locale SQLite a ete activee pour cette instance. Redemarrez LigCom puis revenez pour initialiser et alimenter la base locale d'exploitation.";
        return RedirectToPage(routeValues: BuildRouteValues());
    }

    public async Task<IActionResult> OnPostReturnToConnectedAsync()
    {
        await LocalRuntimeSettingsStore.ClearOverridesAsync(hostEnvironment, HttpContext.RequestAborted);
        RestartRequired = true;
        StatusMessage = "La base locale a ete desactivee pour cette instance. Redemarrez LigCom pour revenir en mode connecte SQL Server.";
        return RedirectToPage(routeValues: BuildRouteValues());
    }

    public async Task<IActionResult> OnPostUpdateSqlitePathAsync()
    {
        await LoadAsync();

        var requestedPath = NormalizeSqlitePath(SqliteDatabasePath);
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            StatusMessage = "Le chemin de la base SQLite est obligatoire.";
            return RedirectToPage(routeValues: BuildRouteValues());
        }

        if (string.Equals(requestedPath, CurrentSqlitePath, StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Le chemin SQLite est deja celui qui est charge par cette instance.";
            return RedirectToPage(routeValues: BuildRouteValues());
        }

        await LocalRuntimeSettingsStore.SaveSqliteDatabasePathAsync(hostEnvironment, requestedPath, HttpContext.RequestAborted);
        RestartRequired = true;
        StatusMessage = $"Le nouvel emplacement SQLite a ete enregistre : {requestedPath}. Redemarrez LigCom local pour l'activer.";
        return RedirectToPage(routeValues: BuildRouteValues());
    }

    public async Task<IActionResult> OnPostPullAsync()
    {
        var tenant = await LoadTenantAsync();
        var result = await offlineSyncService.PullFromCentralAsync(
            tenant.Id,
            User.Identity?.Name ?? User.Identity?.AuthenticationType ?? "Utilisateur LigCom",
            HttpContext.RequestAborted);

        StatusMessage = result.Message;
        return RedirectToPage(routeValues: BuildRouteValues());
    }

    public async Task<IActionResult> OnPostRefreshLocalFromCentralAsync()
    {
        var tenant = await LoadTenantAsync();
        var result = await offlineSyncService.RefreshLocalFromCentralAsync(
            tenant.Id,
            tenant.Slug,
            User.Identity?.Name ?? User.Identity?.AuthenticationType ?? "Utilisateur LigCom",
            HttpContext.RequestAborted);

        StatusMessage = result.Message;
        return RedirectToPage(routeValues: BuildRouteValues());
    }

    public async Task<IActionResult> OnPostBootstrapAndPullAsync()
    {
        var tenant = await LoadTenantAsync();
        await LoadAsync();

        var requestedPath = NormalizeSqlitePath(SqliteDatabasePath);
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            StatusMessage = "Le chemin de la base SQLite est obligatoire.";
            return RedirectToPage(routeValues: BuildRouteValues());
        }

        if (!string.Equals(requestedPath, CurrentSqlitePath, StringComparison.OrdinalIgnoreCase))
        {
            await LocalRuntimeSettingsStore.SaveSqliteDatabasePathAsync(hostEnvironment, requestedPath, HttpContext.RequestAborted);
            RestartRequired = true;
            StatusMessage = $"Le nouvel emplacement SQLite a ete enregistre : {requestedPath}. Redemarrez LigCom local, puis relancez l'initialisation et l'alimentation.";
            return RedirectToPage(routeValues: BuildRouteValues());
        }

        var tenantSlug = string.IsNullOrWhiteSpace(BootstrapTenantSlug) ? CurrentTenantSlug : BootstrapTenantSlug.Trim();
        var bootstrapResult = await offlineSyncService.BootstrapLocalNodeAsync(
            new OfflineNodeBootstrapRequest(
                tenantSlug,
                BootstrapAdminEmail,
                BootstrapAdminPassword,
                BootstrapAdminFirstName,
                BootstrapAdminLastName),
            HttpContext.RequestAborted);

        if (!bootstrapResult.Succeeded || !bootstrapResult.TenantId.HasValue)
        {
            StatusMessage = bootstrapResult.Message;
            return RedirectToPage(routeValues: BuildRouteValues());
        }

        var syncResult = await offlineSyncService.PullFromCentralAsync(
            bootstrapResult.TenantId.Value,
            User.Identity?.Name ?? User.Identity?.AuthenticationType ?? "Utilisateur LigCom",
            HttpContext.RequestAborted);

        StatusMessage = $"{bootstrapResult.Message} {syncResult.Message}".Trim();
        return RedirectToPage(routeValues: BuildRouteValues());
    }

    public async Task<IActionResult> OnPostResolveConflictAsync(Guid conflictId, string? resolutionNote)
    {
        var tenant = await LoadTenantAsync();
        var resolved = await offlineSyncService.ResolveConflictAsync(
            tenant.Id,
            conflictId,
            User.Identity?.Name ?? User.Identity?.AuthenticationType ?? "Utilisateur LigCom",
            resolutionNote,
            ignored: false,
            HttpContext.RequestAborted);

        StatusMessage = resolved
            ? "Le conflit a ete marque comme resolu."
            : "Le conflit n'a pas ete retrouve.";
        return RedirectToPage(routeValues: BuildRouteValues());
    }

    public async Task<IActionResult> OnPostIgnoreConflictAsync(Guid conflictId, string? resolutionNote)
    {
        var tenant = await LoadTenantAsync();
        var resolved = await offlineSyncService.ResolveConflictAsync(
            tenant.Id,
            conflictId,
            User.Identity?.Name ?? User.Identity?.AuthenticationType ?? "Utilisateur LigCom",
            resolutionNote,
            ignored: true,
            HttpContext.RequestAborted);

        StatusMessage = resolved
            ? "Le conflit a ete ignore."
            : "Le conflit n'a pas ete retrouve.";
        return RedirectToPage(routeValues: BuildRouteValues());
    }

    private async Task LoadAsync()
    {
        var tenant = await LoadTenantAsync();
        Dashboard = await offlineSyncService.GetDashboardAsync(tenant.Id, tenant.CompanyName, HttpContext.RequestAborted);
        CurrentSqlitePath = ResolveCurrentSqlitePath();
        CurrentTenantSlug = tenant.Slug;

        AvailableModules = Dashboard.History
            .SelectMany(x => x.Modules.Select(module => module.Name))
            .Concat(Dashboard.Conflicts.Select(x => x.ModuleName))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();

        FilteredHistory = Dashboard.History
            .Where(MatchesHistoryFilters)
            .ToArray();

        FilteredConflicts = Dashboard.Conflicts
            .Where(MatchesConflictFilters)
            .ToArray();

        if (string.IsNullOrWhiteSpace(SqliteDatabasePath))
        {
            SqliteDatabasePath = CurrentSqlitePath;
        }

        if (string.IsNullOrWhiteSpace(BootstrapTenantSlug))
        {
            BootstrapTenantSlug = tenant.Slug;
        }

        if (string.IsNullOrWhiteSpace(ActivationCentralBaseUrl))
        {
            ActivationCentralBaseUrl = offline.CentralBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(ActivationSharedAccessKey))
        {
            ActivationSharedAccessKey = offline.SharedAccessKey;
        }

        if (string.IsNullOrWhiteSpace(ActivationLocalNodeId))
        {
            ActivationLocalNodeId = CurrentLocalNodeId;
        }

        SyncEnabled = offline.Enabled;
        if (string.IsNullOrWhiteSpace(SyncCentralBaseUrl))
        {
            SyncCentralBaseUrl = offline.CentralBaseUrl;
        }
        if (string.IsNullOrWhiteSpace(SyncLocalNodeId))
        {
            SyncLocalNodeId = CurrentLocalNodeId;
        }
    }

    private bool MatchesHistoryFilters(OfflineSyncHistoryItem item)
    {
        if (!MatchesDirection(item.Direction))
        {
            return false;
        }

        if (!MatchesModule(item.Modules.Select(x => x.Name)))
        {
            return false;
        }

        if (!MatchesSearch(
            item.Message,
            item.TriggeredBy,
            item.Status,
            item.Direction,
            string.Join(" ", item.Notes),
            string.Join(" ", item.Modules.SelectMany(x => new[] { x.Name, x.Status, x.Summary }.Concat(x.Notes)))))
        {
            return false;
        }

        return true;
    }

    private bool MatchesConflictFilters(OfflineSyncConflictItem item)
    {
        if (!MatchesDirection(item.Direction))
        {
            return false;
        }

        if (!MatchesModule([item.ModuleName]))
        {
            return false;
        }

        if (!string.Equals(ConflictStatus, "all", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.Status, ConflictStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!MatchesSearch(
            item.ModuleName,
            item.Status,
            item.Severity,
            item.Summary,
            item.ResolutionNote,
            item.ResolvedBy,
            string.Join(" ", item.Notes)))
        {
            return false;
        }

        return true;
    }

    private bool MatchesDirection(string direction) =>
        string.Equals(Direction, "all", StringComparison.OrdinalIgnoreCase)
        || string.Equals(direction, Direction, StringComparison.OrdinalIgnoreCase);

    private bool MatchesModule(IEnumerable<string> moduleNames) =>
        string.Equals(Module, "all", StringComparison.OrdinalIgnoreCase)
        || moduleNames.Any(name => string.Equals(name, Module, StringComparison.OrdinalIgnoreCase));

    private bool MatchesSearch(params string?[] values)
    {
        if (string.IsNullOrWhiteSpace(Search))
        {
            return true;
        }

        return values.Any(value => !string.IsNullOrWhiteSpace(value)
            && value.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private object BuildRouteValues() => new
    {
        search = Search,
        direction = Direction,
        module = Module,
        conflictStatus = ConflictStatus
    };

    private string ResolveCurrentSqlitePath()
    {
        if (!string.IsNullOrWhiteSpace(runtime.SqliteDatabasePath))
        {
            var configuredPath = runtime.SqliteDatabasePath.Trim();
            return Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, configuredPath));
        }

        var appDataPath = Path.Combine(hostEnvironment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataPath);
        return Path.Combine(appDataPath, "ligcom-local.db");
    }

    private static string NormalizeSqlitePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        return Path.GetFullPath(rawPath.Trim());
    }

    private async Task<GescomSaas.Domain.Entities.SaaS.Tenant> LoadTenantAsync()
    {
        var tenantId = await GetTenantIdAsync();
        return await DbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, HttpContext.RequestAborted)
            ?? throw new InvalidOperationException("Tenant introuvable.");
    }
}
