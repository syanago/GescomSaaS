using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GescomSaas.Web.Pages;

[AllowAnonymous]
public class OfflineBootstrapModel(
    ApplicationDbContext dbContext,
    IOfflineSyncService offlineSyncService,
    UserManager<ApplicationUser> userManager,
    IHostEnvironment hostEnvironment,
    IOptions<LigComRuntimeOptions> runtimeOptions,
    IOptions<OfflineSyncOptions> offlineSyncOptions) : PageModel
{
    private readonly LigComRuntimeOptions runtime = runtimeOptions.Value;
    private readonly OfflineSyncOptions offline = offlineSyncOptions.Value;

    [BindProperty]
    public BootstrapInput Input { get; set; } = new();

    public OfflineNodeBootstrapResult? Result { get; private set; }

    public bool IsLocalNodeMode => runtime.Mode == LigComNodeMode.LocalNode;
    public bool IsSqliteProvider => runtime.DatabaseProvider == LigComDatabaseProvider.Sqlite;
    public bool IsOfflineConfigured => offline.Enabled && !string.IsNullOrWhiteSpace(offline.CentralBaseUrl) && !string.IsNullOrWhiteSpace(offline.SharedAccessKey);
    public int LocalTenantCount { get; private set; }
    public int LocalUserCount { get; private set; }
    public string CurrentSqlitePath { get; private set; } = string.Empty;
    public string CurrentCentralBaseUrl => offline.CentralBaseUrl;
    public string CurrentLocalNodeId => string.IsNullOrWhiteSpace(offline.LocalNodeId) ? Environment.MachineName : offline.LocalNodeId.Trim();
    public string CurrentRequestBaseUrl { get; private set; } = string.Empty;
    public bool IsCentralBaseUrlSelfReferencing { get; private set; }
    public bool IsDevelopment => hostEnvironment.IsDevelopment();
    public bool RestartRequired { get; private set; }

    [BindProperty]
    public string? ActivationCentralBaseUrl { get; set; }

    [BindProperty]
    public string? ActivationSharedAccessKey { get; set; }

    [BindProperty]
    public string? ActivationLocalNodeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Reason { get; set; }

    public string? StartupMessage =>
        string.Equals(Reason, "empty-local-node", StringComparison.OrdinalIgnoreCase)
            ? "Aucun compte local n'existe encore dans cette base SQLite. Initialisez d'abord le noeud local pour creer le tenant et l'administrateur local."
            : null;

    public async Task<IActionResult> OnGetAsync()
    {
        var accessResult = await EnsureOfflineAdministrationAccessAsync();
        if (accessResult is not null)
        {
            return accessResult;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostReturnToCentralAsync()
    {
        var accessResult = await EnsureOfflineAdministrationAccessAsync();
        if (accessResult is not null)
        {
            return accessResult;
        }

        await LocalRuntimeSettingsStore.ClearOverridesAsync(hostEnvironment, HttpContext.RequestAborted);
        RestartRequired = true;
        Result = new OfflineNodeBootstrapResult(
            false,
            "Les overrides du noeud local ont ete retires. Redemarrez LigCom pour revenir en mode central SQL Server.",
            null,
            null,
            null,
            null);

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostActivateAsync()
    {
        var accessResult = await EnsureOfflineAdministrationAccessAsync();
        if (accessResult is not null)
        {
            return accessResult;
        }

        await LoadAsync();

        var requestedPath = NormalizeSqlitePath(Input.SqliteDatabasePath);
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            ModelState.AddModelError(nameof(Input.SqliteDatabasePath), "Le chemin de la base SQLite est obligatoire.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(ActivationCentralBaseUrl) || string.IsNullOrWhiteSpace(ActivationSharedAccessKey))
        {
            ModelState.AddModelError(string.Empty, "L'URL du central et la cle partagee sont obligatoires pour activer le noeud local.");
            return Page();
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
        Result = new OfflineNodeBootstrapResult(
            false,
            "Le mode noeud local SQLite a ete active localement. Redemarrez LigCom puis revenez sur cette page pour initialiser la base.",
            null,
            null,
            null,
            Input.AdminEmail);

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var accessResult = await EnsureOfflineAdministrationAccessAsync();
        if (accessResult is not null)
        {
            return accessResult;
        }

        await LoadAsync();

        if (!IsLocalNodeMode || !IsSqliteProvider)
        {
            ModelState.AddModelError(string.Empty, "Cette page n'est disponible que pour un noeud local SQLite.");
            return Page();
        }

        if (!IsOfflineConfigured)
        {
            ModelState.AddModelError(string.Empty, "La connexion au central n'est pas encore configuree dans appsettings.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (LooksLikeUrl(Input.TenantSlug))
        {
            ModelState.AddModelError(nameof(Input.TenantSlug), "Le champ attend le slug du tenant central, pas une URL. Exemple : demo-distribution.");
            return Page();
        }

        if (IsCentralBaseUrlSelfReferencing && !hostEnvironment.IsDevelopment())
        {
            ModelState.AddModelError(string.Empty, "Le noeud local pointe actuellement sur lui-meme comme serveur central. Corrigez l'URL du central ou revenez d'abord en mode central.");
            return Page();
        }

        var requestedPath = NormalizeSqlitePath(Input.SqliteDatabasePath);
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            ModelState.AddModelError(nameof(Input.SqliteDatabasePath), "Le chemin de la base SQLite est obligatoire.");
            return Page();
        }

        if (!string.Equals(requestedPath, CurrentSqlitePath, StringComparison.OrdinalIgnoreCase))
        {
            await LocalRuntimeSettingsStore.SaveSqliteDatabasePathAsync(hostEnvironment, requestedPath, HttpContext.RequestAborted);
            RestartRequired = true;
            Result = new OfflineNodeBootstrapResult(
                false,
                $"Le nouvel emplacement SQLite a ete enregistre : {requestedPath}. Redemarrez LigCom local puis relancez l'initialisation.",
                null,
                null,
                null,
                Input.AdminEmail);
            return Page();
        }

        Result = await offlineSyncService.BootstrapLocalNodeAsync(
            new OfflineNodeBootstrapRequest(
                Input.TenantSlug,
                Input.AdminEmail,
                Input.AdminPassword,
                Input.AdminFirstName,
                Input.AdminLastName),
            HttpContext.RequestAborted);

        await LoadAsync();
        return Page();
    }

    private async Task<IActionResult?> EnsureOfflineAdministrationAccessAsync()
    {
        var hasLocalUsers = await userManager.Users.AsNoTracking().AnyAsync(HttpContext.RequestAborted);
        if (!hasLocalUsers)
        {
            return null;
        }

        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            return Challenge();
        }

        if (User.IsInRole("PlatformAdmin") || User.IsInRole("TenantOwner"))
        {
            return null;
        }

        return Forbid();
    }

    private async Task LoadAsync()
    {
        LocalTenantCount = await dbContext.Tenants.AsNoTracking().CountAsync(HttpContext.RequestAborted);
        LocalUserCount = await userManager.Users.AsNoTracking().CountAsync(HttpContext.RequestAborted);
        CurrentSqlitePath = ResolveCurrentSqlitePath();
        CurrentRequestBaseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/";
        IsCentralBaseUrlSelfReferencing = IsSameBaseUrl(CurrentCentralBaseUrl, CurrentRequestBaseUrl);

        if (string.IsNullOrWhiteSpace(Input.TenantSlug))
        {
            Input.TenantSlug = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(Input.SqliteDatabasePath))
        {
            Input.SqliteDatabasePath = CurrentSqlitePath;
        }

        if (string.IsNullOrWhiteSpace(ActivationCentralBaseUrl))
        {
            ActivationCentralBaseUrl = CurrentCentralBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(ActivationSharedAccessKey))
        {
            ActivationSharedAccessKey = offline.SharedAccessKey;
        }

        if (string.IsNullOrWhiteSpace(ActivationLocalNodeId))
        {
            ActivationLocalNodeId = CurrentLocalNodeId;
        }
    }

    private static bool LooksLikeUrl(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var trimmed = rawValue.Trim();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains('/');
    }

    private static bool IsSameBaseUrl(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        static string Normalize(string url)
        {
            return url.Trim().TrimEnd('/').ToUpperInvariant();
        }

        return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }

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

        var trimmed = rawPath.Trim();
        return Path.GetFullPath(trimmed);
    }

    public sealed class BootstrapInput
    {
        [BindProperty]
        public string TenantSlug { get; set; } = string.Empty;

        [BindProperty]
        public string AdminEmail { get; set; } = string.Empty;

        [BindProperty]
        public string AdminPassword { get; set; } = string.Empty;

        [BindProperty]
        public string AdminFirstName { get; set; } = string.Empty;

        [BindProperty]
        public string AdminLastName { get; set; } = string.Empty;

        [BindProperty]
        public string SqliteDatabasePath { get; set; } = string.Empty;
    }
}
