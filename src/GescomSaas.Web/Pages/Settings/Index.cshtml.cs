using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace GescomSaas.Web.Pages.Settings;

public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    IOptions<LigComRuntimeOptions> runtimeOptions) : SettingsPageModel(dbContext, currentTenantAccessor, userPermissionService, runtimeOptions)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys =>
    [
        TenantPermissionKeys.SettingsCompanyManage,
        TenantPermissionKeys.SettingsNumberingManage,
        TenantPermissionKeys.SettingsSageImportManage,
        TenantPermissionKeys.SettingsAccessProfilesManage,
        TenantPermissionKeys.SettingsOfflineSyncManage
    ];

    public IReadOnlyList<SettingsModuleCard> Modules { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var permissions = await UserPermissionService.GetCurrentPermissionKeysAsync(HttpContext.RequestAborted);
        Modules = BuildModules(permissions, User.IsInRole("PlatformAdmin") || User.IsInRole("TenantOwner"));
    }

    private static IReadOnlyList<SettingsModuleCard> BuildModules(IReadOnlyCollection<string> permissions, bool canAdministerOfflineMode)
    {
        List<SettingsModuleCard> modules = [];

        if (permissions.Contains(TenantPermissionKeys.SettingsCompanyManage, StringComparer.OrdinalIgnoreCase))
        {
            modules.Add(new SettingsModuleCard("/Settings/Company", "Societe et formats", "Nom commercial, raison sociale, coordonnees, devise, symbole et precision d'affichage.", "Ouvrir"));
        }

        if (permissions.Contains(TenantPermissionKeys.SettingsNumberingManage, StringComparer.OrdinalIgnoreCase))
        {
            modules.Add(new SettingsModuleCard("/Settings/Numbering", "Numerotation", "Choisissez les regles de numerotation des referentiels et de chaque type de document.", "Ouvrir"));
        }

        if (permissions.Contains(TenantPermissionKeys.SettingsSageImportManage, StringComparer.OrdinalIgnoreCase))
        {
            modules.Add(new SettingsModuleCard("/Settings/SageImport", "Import Sage SQL", "Connexion source, perimetre, filtres, mapping et mode de transfert pour rapatrier totalement ou partiellement les donnees Sage Gescom.", "Configurer"));
        }

        if (permissions.Contains(TenantPermissionKeys.SettingsAccessProfilesManage, StringComparer.OrdinalIgnoreCase))
        {
            modules.Add(new SettingsModuleCard("/Settings/AccessProfiles", "Profils et droits", "Creez des profils de droits granulaires et affectez-les aux utilisateurs du tenant.", "Administrer"));
        }

        if (canAdministerOfflineMode
            && permissions.Contains(TenantPermissionKeys.SettingsOfflineSyncManage, StringComparer.OrdinalIgnoreCase))
        {
            modules.Add(new SettingsModuleCard("/Settings/StartupMode", "Mode de demarrage", "Choisissez le mode d'utilisation par defaut au demarrage : En ligne (SQL Server) ou Hors ligne (SQLite). Le basculement a chaud protege par mot de passe reste disponible.", "Configurer"));
            modules.Add(new SettingsModuleCard("/Settings/Offline", "Base locale et synchronisation", "Configurez la base SQLite locale, visualisez le perimetre des traitements autorises en local et lancez les synchronisations manuelles avec le serveur central.", "Ouvrir"));
            modules.Add(new SettingsModuleCard("/OfflineBootstrap", "Initialiser la base locale", "Creez la base SQLite du noeud local, recuperez le tenant depuis le central et creez l'administrateur local de secours.", "Initialiser"));
        }

        return modules;
    }
}

public sealed record SettingsModuleCard(string Page, string Title, string Description, string ActionLabel);
