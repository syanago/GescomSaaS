using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GescomSaas.Web.Pages.Settings;

/// <summary>
/// Fenetre d'administration : choix du mode d'utilisation par defaut au demarrage.
///   - "En ligne"  : base SQL Server (Central) + badge "En ligne".
///   - "Hors ligne" : base SQLite locale (LocalNode) + badge "Hors ligne".
/// Le choix est persiste dans App_Data/ligcom-runtime.overrides.json et applique
/// au prochain demarrage. Le basculement a chaud via le toggle protege par mot de
/// passe reste disponible dans les deux cas.
/// </summary>
public class StartupModeModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    IHostEnvironment hostEnvironment,
    IOptions<LigComRuntimeOptions> runtimeOptions) : SettingsPageModel(dbContext, currentTenantAccessor, userPermissionService, runtimeOptions)
{
    private readonly LigComRuntimeOptions runtime = runtimeOptions.Value;

    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.SettingsOfflineSyncManage];

    /// <summary>Mode actuellement CHARGE (en cours d'execution) par cette instance.</summary>
    public bool CurrentlyOffline => runtime.Mode == LigComNodeMode.LocalNode
        && runtime.DatabaseProvider == LigComDatabaseProvider.Sqlite;

    /// <summary>Mode CONFIGURE pour le prochain demarrage (choix enregistre par l'admin).</summary>
    public bool ConfiguredOffline { get; private set; }

    /// <summary>Vrai si le choix enregistre differe du mode en cours d'execution (redemarrage en attente).</summary>
    public bool ChangePending => ConfiguredOffline != CurrentlyOffline;

    public string CurrentDatabaseTarget => CurrentlyOffline
        ? "SQLite (base locale)"
        : "SQL Server (base centrale)";

    public string ConfiguredDatabaseTarget => ConfiguredOffline
        ? "SQLite (base locale)"
        : "SQL Server (base centrale)";

    public bool RestartRequired { get; private set; }

    /// <summary>"online" ou "offline" — valeur choisie dans le formulaire.</summary>
    [BindProperty]
    public string SelectedMode { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        await LoadConfiguredModeAsync();
        SelectedMode = ConfiguredOffline ? "offline" : "online";
    }

    private async Task LoadConfiguredModeAsync()
    {
        // Le choix enregistre fait foi pour l'affichage ; a defaut d'override, on reflete le mode charge.
        var configured = await LocalRuntimeSettingsStore.ReadConfiguredOfflineAsync(hostEnvironment, HttpContext.RequestAborted);
        ConfiguredOffline = configured ?? CurrentlyOffline;
    }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        // Action sensible (touche la base de donnees) : reservee aux administrateurs.
        if (!context.HttpContext.User.IsInRole("PlatformAdmin") && !context.HttpContext.User.IsInRole("TenantOwner"))
        {
            context.Result = Forbid();
            return;
        }

        await base.OnPageHandlerExecutionAsync(context, next);
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        var wantsOffline = string.Equals(SelectedMode, "offline", StringComparison.OrdinalIgnoreCase);
        await LoadConfiguredModeAsync();

        if (wantsOffline == ConfiguredOffline)
        {
            StatusMessage = wantsOffline
                ? "Le mode de demarrage est deja configure sur Hors ligne (SQLite)."
                : "Le mode de demarrage est deja configure sur En ligne (SQL Server).";
            return RedirectToPage();
        }

        await LocalRuntimeSettingsStore.SaveStartupModeAsync(
            hostEnvironment,
            wantsOffline,
            sqliteDatabasePath: null,
            HttpContext.RequestAborted);

        StatusMessage = wantsOffline
            ? "Mode de demarrage enregistre : Hors ligne (base SQLite locale). Il sera applique au prochain redemarrage."
            : "Mode de demarrage enregistre : En ligne (base SQL Server). Il sera applique au prochain redemarrage.";
        return RedirectToPage();
    }
}
