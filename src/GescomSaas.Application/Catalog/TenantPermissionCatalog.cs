namespace GescomSaas.Application.Catalog;

public static class TenantPermissionCatalog
{
    public const string DashboardView = "dashboard.view";
    public const string SalesView = "sales.view";
    public const string SalesManage = "sales.manage";
    public const string PurchasesView = "purchases.view";
    public const string PurchasesManage = "purchases.manage";
    public const string FinanceView = "finance.view";
    public const string FinanceManage = "finance.manage";
    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";
    public const string CustomersView = "customers.view";
    public const string CustomersManage = "customers.manage";
    public const string SuppliersView = "suppliers.view";
    public const string SuppliersManage = "suppliers.manage";
    public const string ProductsView = "products.view";
    public const string ProductsManage = "products.manage";
    public const string WarehousesView = "warehouses.view";
    public const string WarehousesManage = "warehouses.manage";
    public const string SettingsCompanyManage = "settings.company.manage";
    public const string SettingsNumberingManage = "settings.numbering.manage";
    public const string SettingsSageImportManage = "settings.sageimport.manage";
    public const string SettingsSecurityManage = "settings.security.manage";

    public static IReadOnlyList<TenantPermissionDefinition> All { get; } =
    [
        new(DashboardView, "Cockpit", "Voir le cockpit et les indicateurs"),
        new(SalesView, "Ventes", "Consulter les documents de vente"),
        new(SalesManage, "Ventes", "Creer, modifier et transformer les documents de vente"),
        new(PurchasesView, "Achats", "Consulter les documents d'achat"),
        new(PurchasesManage, "Achats", "Creer et modifier les documents d'achat"),
        new(FinanceView, "Finance", "Consulter les echeances, reglements et situations"),
        new(FinanceManage, "Finance", "Saisir des reglements, acomptes, relances et imputations"),
        new(InventoryView, "Stock", "Consulter les mouvements et niveaux de stock"),
        new(InventoryManage, "Stock", "Saisir les ajustements et documents de stock"),
        new(CustomersView, "Referentiels", "Consulter les clients et prospects"),
        new(CustomersManage, "Referentiels", "Creer et modifier les clients et prospects"),
        new(SuppliersView, "Referentiels", "Consulter les fournisseurs"),
        new(SuppliersManage, "Referentiels", "Creer et modifier les fournisseurs"),
        new(ProductsView, "Referentiels", "Consulter les articles"),
        new(ProductsManage, "Referentiels", "Creer et modifier les articles"),
        new(WarehousesView, "Referentiels", "Consulter les depots"),
        new(WarehousesManage, "Referentiels", "Creer et modifier les depots"),
        new(SettingsCompanyManage, "Parametres", "Gerer la societe, les formats et les options generales"),
        new(SettingsNumberingManage, "Parametres", "Gerer la numerotation"),
        new(SettingsSageImportManage, "Parametres", "Configurer l'import Sage SQL"),
        new(SettingsSecurityManage, "Parametres", "Gerer les profils et droits granulaires")
    ];

    public static ISet<string> AllKeys { get; } = All
        .Select(static x => x.Key)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GetRoleDefaults(string role) => role.Trim() switch
    {
        "TenantOwner" => All.Select(static x => x.Key).ToList(),
        "SalesManager" =>
        [
            DashboardView,
            SalesView,
            SalesManage,
            CustomersView,
            CustomersManage,
            ProductsView
        ],
        "PurchasingManager" =>
        [
            DashboardView,
            PurchasesView,
            PurchasesManage,
            SuppliersView,
            SuppliersManage,
            ProductsView
        ],
        "FinanceManager" =>
        [
            DashboardView,
            FinanceView,
            FinanceManage,
            CustomersView,
            SuppliersView
        ],
        "InventoryManager" =>
        [
            DashboardView,
            InventoryView,
            InventoryManage,
            ProductsView,
            WarehousesView,
            WarehousesManage
        ],
        _ => []
    };
}

public sealed record TenantPermissionDefinition(
    string Key,
    string Group,
    string Label);
