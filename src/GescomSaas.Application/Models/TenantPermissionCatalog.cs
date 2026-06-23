namespace GescomSaas.Application.Models;

public static class TenantPermissionKeys
{
    public const string DashboardView = "dashboard.view";

    public const string SalesDocumentsManage = "sales.documents.manage";
    public const string PurchasesDocumentsManage = "purchases.documents.manage";
    public const string FinanceManage = "finance.manage";
    public const string InventoryManage = "inventory.manage";

    public const string ReferencesPartnersManage = "references.partners.manage";
    public const string ReferencesProductsManage = "references.products.manage";
    public const string ReferencesWarehousesManage = "references.warehouses.manage";
    public const string ReferencesPricingManage = "references.pricing.manage";

    public const string SettingsCompanyManage = "settings.company.manage";
    public const string SettingsNumberingManage = "settings.numbering.manage";
    public const string SettingsSageImportManage = "settings.sage_import.manage";
    public const string SettingsAccessProfilesManage = "settings.access_profiles.manage";
    public const string SettingsOfflineSyncManage = "settings.offline_sync.manage";
}

public static class TenantPermissionCatalog
{
    private static readonly IReadOnlyList<TenantAccessPermissionGroup> Groups =
    [
        new(
            "Pilotage",
            [
                new(TenantPermissionKeys.DashboardView, "Cockpit et indicateurs", "Consulter le cockpit et les indicateurs globaux du tenant.")
            ]),
        new(
            "Traitements",
            [
                new(TenantPermissionKeys.SalesDocumentsManage, "Ventes", "Consulter et gerer les documents de vente."),
                new(TenantPermissionKeys.PurchasesDocumentsManage, "Achats", "Consulter et gerer les documents d'achat."),
                new(TenantPermissionKeys.FinanceManage, "Finances", "Acceder aux reglements, echeances, acomptes et relances."),
                new(TenantPermissionKeys.InventoryManage, "Stock", "Consulter et gerer les mouvements, ajustements et documents de stock.")
            ]),
        new(
            "Referentiels",
            [
                new(TenantPermissionKeys.ReferencesPartnersManage, "Tiers", "Gerer les clients, fournisseurs et leur situation."),
                new(TenantPermissionKeys.ReferencesProductsManage, "Articles", "Gerer les articles et familles."),
                new(TenantPermissionKeys.ReferencesWarehousesManage, "Depots", "Gerer les depots et organisations de stock."),
                new(TenantPermissionKeys.ReferencesPricingManage, "Tarification", "Gerer taxes, conditions de paiement et listes de prix.")
            ]),
        new(
            "Parametres",
            [
                new(TenantPermissionKeys.SettingsCompanyManage, "Societe et formats", "Modifier l'identite societe, les formats et les options finance."),
                new(TenantPermissionKeys.SettingsNumberingManage, "Numerotation", "Modifier les regles de numerotation des referentiels et documents."),
                new(TenantPermissionKeys.SettingsSageImportManage, "Import Sage SQL", "Configurer la connexion, le mapping et les campagnes d'import Sage."),
                new(TenantPermissionKeys.SettingsAccessProfilesManage, "Profils et droits", "Administrer les profils de droits granulaires du tenant."),
                new(TenantPermissionKeys.SettingsOfflineSyncManage, "Base locale et synchronisation", "Administrer la base locale, le basculement de mode et les synchronisations manuelles.")
            ])
    ];

    public static IReadOnlyList<TenantAccessPermissionGroup> PermissionGroups => Groups;

    public static IReadOnlyList<string> AllKeys { get; } = Groups
        .SelectMany(static group => group.Permissions)
        .Select(static permission => permission.Key)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RoleDefaultPermissions =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["PlatformAdmin"] = AllKeys,
            ["TenantOwner"] = AllKeys,
            ["SalesManager"] =
            [
                TenantPermissionKeys.DashboardView,
                TenantPermissionKeys.SalesDocumentsManage,
                TenantPermissionKeys.ReferencesPartnersManage,
                TenantPermissionKeys.ReferencesProductsManage
            ],
            ["PurchasingManager"] =
            [
                TenantPermissionKeys.DashboardView,
                TenantPermissionKeys.PurchasesDocumentsManage,
                TenantPermissionKeys.ReferencesPartnersManage,
                TenantPermissionKeys.ReferencesProductsManage
            ],
            ["FinanceManager"] =
            [
                TenantPermissionKeys.DashboardView,
                TenantPermissionKeys.FinanceManage,
                TenantPermissionKeys.ReferencesPartnersManage
            ],
            ["InventoryManager"] =
            [
                TenantPermissionKeys.DashboardView,
                TenantPermissionKeys.InventoryManage,
                TenantPermissionKeys.ReferencesProductsManage,
                TenantPermissionKeys.ReferencesWarehousesManage
            ]
        };

    public static bool IsKnownPermission(string permissionKey) =>
        AllKeys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> GetDefaultPermissionsForRoles(IEnumerable<string> roles)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            if (RoleDefaultPermissions.TryGetValue(role, out var rolePermissions))
            {
                permissions.UnionWith(rolePermissions);
            }
        }

        return permissions.ToArray();
    }
}
