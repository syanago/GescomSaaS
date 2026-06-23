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

    /// <summary>Mode actuellement charge par cette instance.</summary>
    public bool CurrentlyOffline => runtime.Mode == LigComNodeMode.LocalNode
        && runtime.DatabaseProvider == LigComDatabaseProvider.Sqlite;

    public string CurrentDatabaseTarget => CurrentlyOffline
        ? "SQLite (base locale)"
        : "SQL Server (base centrale)";

    public bool RestartRequired { get; private set; }

    /// <summary>"online" ou "offline" — valeur choisie dans le formulaire.</summary>
    [BindProperty]
    public string SelectedMode { get; set; } = string.Empty;

    public void OnGet()
    {
        SelectedMode = CurrentlyOffline ? "offline" : "online";
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

        if (wantsOffline == CurrentlyOffline)
        {
            StatusMessage = wantsOffline
                ? "Le mode de demarrage est deja regle sur Hors ligne (SQLite)."
                : "Le mode de demarrage est deja regle sur En ligne (SQL Server).";
            return RedirectToPage();
        }

        await LocalRuntimeSettingsStore.SaveStartupModeAsync(
            hostEnvironment,
            wantsOffline,
            sqliteDatabasePath: null,
            HttpContext.RequestAborted);

        RestartRequired = true;
        StatusMessage = wantsOffline
            ? "Mode de demarrage enregistre : Hors ligne (base SQLite locale). Redemarrez LigCom pour l'appliquer."
            : "Mode de demarrage enregistre : En ligne (base SQL Server). Redemarrez LigCom pour l'appliquer.";
        return RedirectToPage();
    }
}
