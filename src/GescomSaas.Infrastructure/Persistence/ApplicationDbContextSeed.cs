using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GescomSaas.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.EnsureCreatedAsync();

        await EnsureRolesAsync(roleManager);
        var plans = await EnsureSubscriptionPlansAsync(context);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var demoTenant = await EnsureTenantAsync(
            context,
            "demo-distribution",
            "Demo Distribution",
            "Demo Distribution SARL",
            "owner@demo.gescom.local",
            "+1 514 555 0100",
            "125 boulevard du Commerce",
            "Bureau 400",
            "H3A1A1",
            "Montreal",
            "QC",
            "CA",
            "CAD");

        var pilotTenant = await EnsureTenantAsync(
            context,
            "ligcom-pilot",
            "LigCom Retail Pilot",
            "LigCom Retail Pilot Inc.",
            "pilot.owner@ligcom.local",
            "+1 416 555 0144",
            "80 King Street West",
            "Suite 1200",
            "M5H1A1",
            "Toronto",
            "ON",
            "CA",
            "CAD");

        var demoSubscription = await EnsureSubscriptionAsync(
            context,
            demoTenant,
            plans["STANDARD"],
            SubscriptionStatus.Active,
            today.AddMonths(-4),
            today.AddDays(10),
            autoRenew: true,
            maxMonthlyDocumentsOverride: 40);

        var pilotSubscription = await EnsureSubscriptionAsync(
            context,
            pilotTenant,
            plans["ESSENTIALS"],
            SubscriptionStatus.Active,
            today.AddMonths(-1),
            today.AddDays(5),
            autoRenew: true,
            maxUsersOverride: 3,
            maxProductsOverride: 5,
            maxWarehousesOverride: 1,
            maxMonthlyDocumentsOverride: 6);

        await EnsureDemoTenantDataAsync(context, demoTenant, demoSubscription, today);
        await EnsurePilotTenantDataAsync(context, pilotTenant, pilotSubscription, today);

        await EnsureApplicationUserAsync(userManager, "owner@demo.gescom.local", "Demo123!", "Owner", "Demo", demoTenant.Id, ["TenantOwner"]);
        await EnsureApplicationUserAsync(userManager, "sales@demo.gescom.local", "Demo123!", "Sophie", "Ventes", demoTenant.Id, ["SalesManager"]);
        await EnsureApplicationUserAsync(userManager, "finance@demo.gescom.local", "Demo123!", "Nadia", "Finance", demoTenant.Id, ["FinanceManager"]);
        await EnsureApplicationUserAsync(userManager, "inventory@demo.gescom.local", "Demo123!", "Lucas", "Stock", demoTenant.Id, ["InventoryManager"]);
        await EnsureApplicationUserAsync(userManager, "purchasing@demo.gescom.local", "Demo123!", "Benoit", "Achats", demoTenant.Id, ["PurchasingManager"]);

        await EnsureApplicationUserAsync(userManager, "pilot.owner@ligcom.local", "Demo123!", "Maya", "Pilote", pilotTenant.Id, ["TenantOwner"]);
        await EnsureApplicationUserAsync(userManager, "pilot.sales@ligcom.local", "Demo123!", "Akim", "Commerce", pilotTenant.Id, ["SalesManager"]);
        await EnsureApplicationUserAsync(userManager, "pilot.inventory@ligcom.local", "Demo123!", "Joel", "Operations", pilotTenant.Id, ["InventoryManager"]);

        await EnsureApplicationUserAsync(userManager, "consultant@gescom.local", "Demo123!", "Camille", "Consultant", null, []);
        await EnsureApplicationUserAsync(userManager, "platform@gescom.local", "Demo123!", "Platform", "Admin", null, ["PlatformAdmin"]);

        await EnsureInvitationAsync(
            context,
            demoTenant.Id,
            "buyer@demo.gescom.local",
            "Benoit",
            "Achats",
            "PurchasingManager",
            UserInvitationStatus.Pending,
            DateTime.UtcNow.AddDays(7),
            "Invitation de demonstration");

        await EnsureInvitationAsync(
            context,
            demoTenant.Id,
            "finance.guest@demo.gescom.local",
            "Helene",
            "Compta",
            "FinanceManager",
            UserInvitationStatus.Expired,
            DateTime.UtcNow.AddDays(-1),
            "Invitation expiree de demonstration");

        await EnsureInvitationAsync(
            context,
            pilotTenant.Id,
            "ops.external@ligcom.local",
            "Marion",
            "Audit",
            "InventoryManager",
            UserInvitationStatus.Pending,
            DateTime.UtcNow.AddDays(5),
            "Invitation pilote pour test quotas");
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in new[] { "PlatformAdmin", "TenantOwner", "SalesManager", "PurchasingManager", "FinanceManager", "InventoryManager" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }

    private static async Task<Dictionary<string, SubscriptionPlan>> EnsureSubscriptionPlansAsync(ApplicationDbContext context)
    {
        var specs = new[]
        {
            new SubscriptionPlanSpec("ESSENTIALS", "Essentials", TenantEdition.Essentials, 79m, 3, 50, 20, 100, 1, 120, 12m, 0.25m, 0.10m, false, false, false),
            new SubscriptionPlanSpec("STANDARD", "Standard", TenantEdition.Standard, 149m, 10, 250, 80, 500, 3, 500, 10m, 0.20m, 0.08m, true, true, false),
            new SubscriptionPlanSpec("ENTERPRISE", "Enterprise", TenantEdition.Enterprise, 299m, 100, 5000, 1000, 10000, 20, 10000, 8m, 0.10m, 0.03m, true, true, true)
        };

        var plans = await context.SubscriptionPlans.ToDictionaryAsync(x => x.Code);

        foreach (var spec in specs)
        {
            if (!plans.TryGetValue(spec.Code, out var plan))
            {
                plan = new SubscriptionPlan { Code = spec.Code };
                context.SubscriptionPlans.Add(plan);
                plans[spec.Code] = plan;
            }

            plan.Label = spec.Label;
            plan.Edition = spec.Edition;
            plan.MonthlyPrice = spec.MonthlyPrice;
            plan.MaxUsers = spec.MaxUsers;
            plan.MaxCustomers = spec.MaxCustomers;
            plan.MaxSuppliers = spec.MaxSuppliers;
            plan.MaxProducts = spec.MaxProducts;
            plan.MaxWarehouses = spec.MaxWarehouses;
            plan.MaxMonthlyDocuments = spec.MaxMonthlyDocuments;
            plan.OverageUserPrice = spec.OverageUserPrice;
            plan.OverageProductPrice = spec.OverageProductPrice;
            plan.OverageDocumentPrice = spec.OverageDocumentPrice;
            plan.IncludesAdvancedStock = spec.IncludesAdvancedStock;
            plan.IncludesPurchasing = spec.IncludesPurchasing;
            plan.IncludesBusinessIntelligence = spec.IncludesBusinessIntelligence;
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        return plans;
    }

    private static async Task<Tenant> EnsureTenantAsync(
        ApplicationDbContext context,
        string slug,
        string companyName,
        string companyLegalName,
        string primaryContactEmail,
        string phoneNumber,
        string addressLine1,
        string addressLine2,
        string postalCode,
        string city,
        string state,
        string countryCode,
        string currencyCode)
    {
        var tenant = await context.Tenants.FirstOrDefaultAsync(x => x.Slug == slug);
        var isNewTenant = tenant is null;
        if (tenant is null)
        {
            tenant = new Tenant { Slug = slug };
            context.Tenants.Add(tenant);
        }

        if (isNewTenant)
        {
            tenant.CompanyName = companyName;
            tenant.CompanyLegalName = companyLegalName;
            tenant.PrimaryContactEmail = primaryContactEmail;
            tenant.PhoneNumber = phoneNumber;
            tenant.AddressLine1 = addressLine1;
            tenant.AddressLine2 = addressLine2;
            tenant.PostalCode = postalCode;
            tenant.City = city;
            tenant.State = state;
            tenant.CountryCode = countryCode;
            tenant.CurrencyCode = currencyCode;
            tenant.CashCurrencyCode = currencyCode;
            tenant.CurrencySymbol = "$";
            tenant.CurrencySymbolPosition = CurrencySymbolPosition.BeforeAmount;
            tenant.MoneyDecimalSeparator = ",";
            tenant.MoneyGroupSeparator = " ";
            tenant.MoneyDecimalPlaces = 2;
            tenant.QuantityDecimalSeparator = ",";
            tenant.QuantityGroupSeparator = " ";
            tenant.QuantityDecimalPlaces = 3;
            tenant.AllowNegativeStock = false;
            tenant.DefaultStockValuationMethod = StockValuationMethod.Cmup;
            tenant.VisualTheme = ApplicationTheme.LigComMidnight;
            tenant.IsActive = true;
        }
        else
        {
            tenant.CompanyName = string.IsNullOrWhiteSpace(tenant.CompanyName) ? companyName : tenant.CompanyName;
            tenant.CompanyLegalName = string.IsNullOrWhiteSpace(tenant.CompanyLegalName) ? companyLegalName : tenant.CompanyLegalName;
            tenant.PrimaryContactEmail = string.IsNullOrWhiteSpace(tenant.PrimaryContactEmail) ? primaryContactEmail : tenant.PrimaryContactEmail;
            tenant.PhoneNumber = string.IsNullOrWhiteSpace(tenant.PhoneNumber) ? phoneNumber : tenant.PhoneNumber;
            tenant.AddressLine1 = string.IsNullOrWhiteSpace(tenant.AddressLine1) ? addressLine1 : tenant.AddressLine1;
            tenant.AddressLine2 = string.IsNullOrWhiteSpace(tenant.AddressLine2) ? addressLine2 : tenant.AddressLine2;
            tenant.PostalCode = string.IsNullOrWhiteSpace(tenant.PostalCode) ? postalCode : tenant.PostalCode;
            tenant.City = string.IsNullOrWhiteSpace(tenant.City) ? city : tenant.City;
            tenant.State = string.IsNullOrWhiteSpace(tenant.State) ? state : tenant.State;
            tenant.CountryCode = string.IsNullOrWhiteSpace(tenant.CountryCode) ? countryCode : tenant.CountryCode;
            tenant.CurrencyCode = string.IsNullOrWhiteSpace(tenant.CurrencyCode) ? currencyCode : tenant.CurrencyCode;
            tenant.CashCurrencyCode = string.IsNullOrWhiteSpace(tenant.CashCurrencyCode) ? tenant.CurrencyCode : tenant.CashCurrencyCode;
            tenant.CurrencySymbol = string.IsNullOrWhiteSpace(tenant.CurrencySymbol) ? "$" : tenant.CurrencySymbol;
            tenant.MoneyDecimalSeparator = string.IsNullOrEmpty(tenant.MoneyDecimalSeparator) ? "," : tenant.MoneyDecimalSeparator;
            tenant.MoneyGroupSeparator ??= " ";
            tenant.MoneyDecimalPlaces = tenant.MoneyDecimalPlaces < 0 ? 2 : tenant.MoneyDecimalPlaces;
            tenant.QuantityDecimalSeparator = string.IsNullOrEmpty(tenant.QuantityDecimalSeparator) ? "," : tenant.QuantityDecimalSeparator;
            tenant.QuantityGroupSeparator ??= " ";
            tenant.QuantityDecimalPlaces = tenant.QuantityDecimalPlaces < 0 ? 3 : tenant.QuantityDecimalPlaces;
            tenant.VisualTheme = Enum.IsDefined(tenant.VisualTheme) ? tenant.VisualTheme : ApplicationTheme.LigComMidnight;
            tenant.IsActive = true;
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        return tenant;
    }

    private static async Task<TenantSubscription> EnsureSubscriptionAsync(
        ApplicationDbContext context,
        Tenant tenant,
        SubscriptionPlan plan,
        SubscriptionStatus status,
        DateOnly startsOn,
        DateOnly? nextBillingDate,
        bool autoRenew,
        decimal? monthlyPriceOverride = null,
        int? maxUsersOverride = null,
        int? maxCustomersOverride = null,
        int? maxSuppliersOverride = null,
        int? maxProductsOverride = null,
        int? maxWarehousesOverride = null,
        int? maxMonthlyDocumentsOverride = null)
    {
        var subscription = await context.TenantSubscriptions
            .Where(x => x.TenantId == tenant.Id)
            .OrderByDescending(x => x.StartsOn)
            .FirstOrDefaultAsync();

        if (subscription is null)
        {
            subscription = new TenantSubscription
            {
                TenantId = tenant.Id
            };

            context.TenantSubscriptions.Add(subscription);
        }

        subscription.SubscriptionPlanId = plan.Id;
        subscription.Status = status;
        subscription.StartsOn = startsOn;
        subscription.NextBillingDate = nextBillingDate;
        subscription.AutoRenew = autoRenew;
        subscription.MonthlyPriceOverride = monthlyPriceOverride;
        subscription.MaxUsersOverride = maxUsersOverride;
        subscription.MaxCustomersOverride = maxCustomersOverride;
        subscription.MaxSuppliersOverride = maxSuppliersOverride;
        subscription.MaxProductsOverride = maxProductsOverride;
        subscription.MaxWarehousesOverride = maxWarehousesOverride;
        subscription.MaxMonthlyDocumentsOverride = maxMonthlyDocumentsOverride;

        await context.SaveChangesAsync();
        return subscription;
    }

    private static async Task EnsureDemoTenantDataAsync(ApplicationDbContext context, Tenant tenant, TenantSubscription subscription, DateOnly today)
    {
        var paymentTerms = await EnsurePaymentTermsAsync(context, tenant.Id, new[]
        {
            new PaymentTermSeed("COMPT", "Comptant", 0),
            new PaymentTermSeed("15J", "15 jours", 15),
            new PaymentTermSeed("30J", "30 jours fin de mois", 30),
            new PaymentTermSeed("45J", "45 jours", 45)
        });

        var taxCodes = await EnsureTaxCodesAsync(context, tenant.Id, new[]
        {
            new TaxCodeSeed("TPS", "TPS 5%", 5m),
            new TaxCodeSeed("TVH", "TVH 15%", 15m),
            new TaxCodeSeed("EXO", "Exonere", 0m)
        });

        var categories = await EnsureProductCategoriesAsync(context, tenant.Id, new[]
        {
            new CategorySeed("MATERIEL", "Materiel"),
            new CategorySeed("ACCESS", "Accessoires"),
            new CategorySeed("SERVICE", "Services"),
            new CategorySeed("CONSOM", "Consommables", StockValuationMethod.LastPurchaseCost, StockIdentityTrackingMode.Lot)
        });

        var warehouses = await EnsureWarehousesAsync(context, tenant.Id, new[]
        {
            new WarehouseSeed("PRINCIPAL", "Depot principal", true),
            new WarehouseSeed("SHOWROOM", "Stock showroom", false),
            new WarehouseSeed("SAV", "Zone SAV", false)
        });

        var products = await EnsureProductsAsync(context, tenant.Id, categories, taxCodes, new[]
        {
            new ProductSeed("ART-001", "Ordinateur portable Pro 14", ProductType.StockItem, true, "UN", "MATERIEL", "TPS", 980m, 1290m, "Portable professionnel 14 pouces"),
            new ProductSeed("ART-002", "Moniteur 27 pouces", ProductType.StockItem, true, "UN", "MATERIEL", "TPS", 210m, 329m, "Ecran bureautique 27 pouces"),
            new ProductSeed("ART-003", "Station d'accueil USB-C", ProductType.StockItem, true, "UN", "ACCESS", "TPS", 85m, 149m, "Dock universel"),
            new ProductSeed("ART-004", "Appliance reseau securisee", ProductType.StockItem, true, "UN", "MATERIEL", "TPS", 310m, 549m, "Pare-feu pour PME"),
            new ProductSeed("ART-005", "Imprimante laser compacte", ProductType.StockItem, true, "UN", "MATERIEL", "TPS", 175m, 289m, "Imprimante laser A4", StockValuationMethod.Fifo, StockIdentityTrackingMode.SerialNumber),
            new ProductSeed("CONS-001", "Cartouche toner noir", ProductType.StockItem, true, "UN", "CONSOM", "TPS", 22m, 39m, "Consommable laser", StockValuationMethod.LastPurchaseCost, StockIdentityTrackingMode.Lot),
            new ProductSeed("SERV-INST", "Installation sur site", ProductType.Service, false, "H", "SERVICE", "TPS", 0m, 250m, "Prestation de deploiement"),
            new ProductSeed("SERV-MAINT", "Maintenance annuelle", ProductType.Service, false, "AN", "SERVICE", "TPS", 0m, 420m, "Contrat annuel")
        });

        var partners = await EnsurePartnersAsync(context, tenant.Id, paymentTerms, new[]
        {
            new PartnerSeed("CLI-001", "Clinique Horizon", BusinessPartnerType.Customer, "comptabilite@horizon.test", "30J", 15000m, "Montreal", "Canada"),
            new PartnerSeed("CLI-002", "Atelier Nova", BusinessPartnerType.Customer, "factures@nova.test", "15J", 12000m, "Quebec", "Canada"),
            new PartnerSeed("CLI-003", "Ville de Laval", BusinessPartnerType.Customer, "achats@laval.test", "45J", 50000m, "Laval", "Canada"),
            new PartnerSeed("PROS-001", "Maison Atlas", BusinessPartnerType.Prospect, "projet@atlas.test", "30J", 5000m, "Sherbrooke", "Canada"),
            new PartnerSeed("FOU-001", "Nordic Supplies", BusinessPartnerType.Supplier, "sales@nordic.test", "30J", 0m, "Toronto", "Canada"),
            new PartnerSeed("FOU-002", "Quebec Network", BusinessPartnerType.Supplier, "appro@qnet.test", "15J", 0m, "Quebec", "Canada"),
            new PartnerSeed("TIERS-001", "TechnoPlus", BusinessPartnerType.Both, "ops@technoplus.test", "30J", 10000m, "Longueuil", "Canada")
        });

        await EnsureDocumentSequencesAsync(context, tenant.Id, new[]
        {
            new DocumentSequenceSeed(CommercialDocumentType.SalesQuote, "DEV-2026-", 3),
            new DocumentSequenceSeed(CommercialDocumentType.SalesOrder, "CMD-2026-", 3),
            new DocumentSequenceSeed(CommercialDocumentType.DeliveryNote, "BL-2026-", 2),
            new DocumentSequenceSeed(CommercialDocumentType.SalesInvoice, "FAC-2026-", 4),
            new DocumentSequenceSeed(CommercialDocumentType.SalesCreditNote, "AVO-2026-", 2),
            new DocumentSequenceSeed(CommercialDocumentType.PurchaseRequest, "DAP-2026-", 2),
            new DocumentSequenceSeed(CommercialDocumentType.PurchaseOrder, "ACH-2026-", 3),
            new DocumentSequenceSeed(CommercialDocumentType.GoodsReceipt, "REC-2026-", 2),
            new DocumentSequenceSeed(CommercialDocumentType.PurchaseInvoice, "FAF-2026-", 2),
            new DocumentSequenceSeed(CommercialDocumentType.SupplierCreditNote, "AVF-2026-", 2)
        });

        await EnsurePriceListsAsync(context, tenant.Id, tenant.CurrencyCode, products, new[]
        {
            new PriceListSeed("STANDARD", "Tarif standard", true,
                new PriceListLineSeed("ART-001", 1290m),
                new PriceListLineSeed("ART-002", 329m),
                new PriceListLineSeed("ART-003", 149m),
                new PriceListLineSeed("ART-004", 549m),
                new PriceListLineSeed("ART-005", 289m),
                new PriceListLineSeed("CONS-001", 39m),
                new PriceListLineSeed("SERV-INST", 250m),
                new PriceListLineSeed("SERV-MAINT", 420m)),
            new PriceListSeed("VIP", "Tarif grands comptes", false,
                new PriceListLineSeed("ART-001", 1240m),
                new PriceListLineSeed("ART-002", 309m),
                new PriceListLineSeed("ART-004", 519m),
                new PriceListLineSeed("SERV-MAINT", 390m)),
            new PriceListSeed("RESELLER", "Tarif revendeur", false,
                new PriceListLineSeed("ART-003", 135m),
                new PriceListLineSeed("CONS-001", 34m),
                new PriceListLineSeed("ART-005", 265m))
        });

        var quote1 = await EnsureDocumentAsync(context, tenant.Id, "DEV-2026-0001", CommercialDocumentType.SalesQuote, CommercialDocumentStatus.Open, partners["CLI-002"], warehouses["PRINCIPAL"], today.AddDays(-20), today.AddDays(10), tenant.CurrencyCode, "Devis pour equiper deux postes de travail", null, products, new[]
        {
            new DocumentLineSeed("ART-001", 2m, 1290m, 5m, 0m, null, null, "SN-LIG-ART001-0001"),
            new DocumentLineSeed("ART-003", 2m, 149m, 5m)
        });

        await EnsureDocumentAsync(context, tenant.Id, "DEV-2026-0002", CommercialDocumentType.SalesQuote, CommercialDocumentStatus.Open, partners["PROS-001"], warehouses["PRINCIPAL"], today.AddDays(-5), today.AddDays(20), tenant.CurrencyCode, "Proposition commerciale encore en discussion", null, products, new[]
        {
            new DocumentLineSeed("ART-004", 1m, 549m, 5m),
            new DocumentLineSeed("SERV-MAINT", 1m, 420m, 5m)
        });

        var salesOrder1 = await EnsureDocumentAsync(context, tenant.Id, "CMD-2026-0001", CommercialDocumentType.SalesOrder, CommercialDocumentStatus.PartiallyProcessed, partners["CLI-002"], warehouses["PRINCIPAL"], today.AddDays(-16), today.AddDays(14), tenant.CurrencyCode, "Commande issue du devis DEV-2026-0001", quote1, products, new[]
        {
            new DocumentLineSeed("ART-001", 2m, 1290m, 5m, 0m, null, null, "SN-LIG-ART001-0001"),
            new DocumentLineSeed("ART-003", 2m, 149m, 5m)
        });

        await EnsureDocumentAsync(context, tenant.Id, "CMD-2026-0002", CommercialDocumentType.SalesOrder, CommercialDocumentStatus.Open, partners["CLI-001"], warehouses["PRINCIPAL"], today.AddDays(-2), today.AddDays(12), tenant.CurrencyCode, "Commande ouverte en attente de livraison", null, products, new[]
        {
            new DocumentLineSeed("ART-004", 1m, 549m, 5m)
        });

        var delivery1 = await EnsureDocumentAsync(context, tenant.Id, "BL-2026-0001", CommercialDocumentType.DeliveryNote, CommercialDocumentStatus.Completed, partners["CLI-002"], warehouses["PRINCIPAL"], today.AddDays(-14), null, tenant.CurrencyCode, "Livraison complete de la commande CMD-2026-0001", salesOrder1, products, new[]
        {
            new DocumentLineSeed("ART-001", 2m, 1290m, 5m, 0m, null, null, "SN-LIG-ART001-0001"),
            new DocumentLineSeed("ART-003", 2m, 149m, 5m)
        });

        var salesInvoice1 = await EnsureDocumentAsync(context, tenant.Id, "FAC-2026-0001", CommercialDocumentType.SalesInvoice, CommercialDocumentStatus.PartiallyProcessed, partners["CLI-002"], warehouses["PRINCIPAL"], today.AddDays(-13), today.AddDays(-1), tenant.CurrencyCode, "Facture issue du BL-2026-0001 avec installation", delivery1, products, new[]
        {
            new DocumentLineSeed("ART-001", 2m, 1290m, 5m),
            new DocumentLineSeed("ART-003", 2m, 149m, 5m),
            new DocumentLineSeed("SERV-INST", 1m, 250m, 5m)
        });

        var salesInvoice3 = await EnsureDocumentAsync(context, tenant.Id, "FAC-2026-0003", CommercialDocumentType.SalesInvoice, CommercialDocumentStatus.Completed, partners["CLI-003"], warehouses["SAV"], today.AddDays(-7), today.AddDays(8), tenant.CurrencyCode, "Facture reglee integralement", null, products, new[]
        {
            new DocumentLineSeed("SERV-MAINT", 1m, 420m, 5m),
            new DocumentLineSeed("ART-005", 1m, 289m, 5m)
        });

        await EnsureDocumentAsync(context, tenant.Id, "AVO-2026-0001", CommercialDocumentType.SalesCreditNote, CommercialDocumentStatus.Completed, partners["CLI-002"], warehouses["PRINCIPAL"], today.AddDays(-10), null, tenant.CurrencyCode, "Retour d'une station d'accueil", salesInvoice1, products, new[]
        {
            new DocumentLineSeed("ART-003", 1m, 149m, 5m)
        });

        await EnsureDocumentAsync(context, tenant.Id, "FAC-2026-0002", CommercialDocumentType.SalesInvoice, CommercialDocumentStatus.Open, partners["CLI-001"], warehouses["PRINCIPAL"], today.AddDays(-30), today.AddDays(-5), tenant.CurrencyCode, "Facture en retard pour tests relances", null, products, new[]
        {
            new DocumentLineSeed("ART-002", 3m, 329m, 5m),
            new DocumentLineSeed("CONS-001", 6m, 39m, 5m)
        });

        var purchaseRequest1 = await EnsureDocumentAsync(context, tenant.Id, "DAP-2026-0001", CommercialDocumentType.PurchaseRequest, CommercialDocumentStatus.Open, partners["FOU-002"], warehouses["PRINCIPAL"], today.AddDays(-18), null, tenant.CurrencyCode, "Demande d'achat de materiel reseau et consommables", null, products, new[]
        {
            new DocumentLineSeed("ART-001", 3m, 980m, 5m),
            new DocumentLineSeed("CONS-001", 20m, 22m, 5m, 0m, null, "LOT-TONER-2026-A")
        });

        var purchaseOrder1 = await EnsureDocumentAsync(context, tenant.Id, "ACH-2026-0001", CommercialDocumentType.PurchaseOrder, CommercialDocumentStatus.PartiallyProcessed, partners["FOU-002"], warehouses["PRINCIPAL"], today.AddDays(-15), null, tenant.CurrencyCode, "Commande fournisseur issue de DAP-2026-0001", purchaseRequest1, products, new[]
        {
            new DocumentLineSeed("ART-001", 3m, 980m, 5m),
            new DocumentLineSeed("CONS-001", 20m, 22m, 5m, 0m, null, "LOT-TONER-2026-A")
        });

        await EnsureDocumentAsync(context, tenant.Id, "ACH-2026-0002", CommercialDocumentType.PurchaseOrder, CommercialDocumentStatus.Open, partners["FOU-001"], warehouses["SAV"], today.AddDays(-1), null, tenant.CurrencyCode, "Commande fournisseur encore ouverte", null, products, new[]
        {
            new DocumentLineSeed("ART-005", 4m, 175m, 5m)
        });

        var receipt1 = await EnsureDocumentAsync(context, tenant.Id, "REC-2026-0001", CommercialDocumentType.GoodsReceipt, CommercialDocumentStatus.Completed, partners["FOU-002"], warehouses["PRINCIPAL"], today.AddDays(-12), null, tenant.CurrencyCode, "Reception complete de la commande ACH-2026-0001", purchaseOrder1, products, new[]
        {
            new DocumentLineSeed("ART-001", 3m, 980m, 5m),
            new DocumentLineSeed("CONS-001", 20m, 22m, 5m, 0m, null, "LOT-TONER-2026-A", null, today.AddMonths(10))
        });

        var purchaseInvoice1 = await EnsureDocumentAsync(context, tenant.Id, "FAF-2026-0001", CommercialDocumentType.PurchaseInvoice, CommercialDocumentStatus.PartiallyProcessed, partners["FOU-002"], warehouses["PRINCIPAL"], today.AddDays(-9), today.AddDays(6), tenant.CurrencyCode, "Facture fournisseur issue de REC-2026-0001", receipt1, products, new[]
        {
            new DocumentLineSeed("ART-001", 3m, 980m, 5m),
            new DocumentLineSeed("CONS-001", 20m, 22m, 5m, 0m, null, "LOT-TONER-2026-A", null, today.AddMonths(10))
        });

        await EnsureDocumentAsync(context, tenant.Id, "AVF-2026-0001", CommercialDocumentType.SupplierCreditNote, CommercialDocumentStatus.Completed, partners["FOU-002"], warehouses["PRINCIPAL"], today.AddDays(-6), null, tenant.CurrencyCode, "Avoir fournisseur sur des consommables endommages", purchaseInvoice1, products, new[]
        {
            new DocumentLineSeed("CONS-001", 5m, 22m, 5m)
        });

        await EnsureStockMovementsAsync(context, tenant.Id, products, warehouses, new[]
        {
            new StockMovementSeed("ART-001", "PRINCIPAL", StockMovementType.OpeningBalance, today.AddDays(-60), 8m, 980m, "OUV-2026-ART-001"),
            new StockMovementSeed("ART-002", "PRINCIPAL", StockMovementType.OpeningBalance, today.AddDays(-60), 12m, 210m, "OUV-2026-ART-002"),
            new StockMovementSeed("ART-003", "SHOWROOM", StockMovementType.OpeningBalance, today.AddDays(-60), 14m, 85m, "OUV-2026-ART-003"),
            new StockMovementSeed("ART-004", "PRINCIPAL", StockMovementType.OpeningBalance, today.AddDays(-60), 6m, 310m, "OUV-2026-ART-004"),
            new StockMovementSeed("ART-005", "SAV", StockMovementType.OpeningBalance, today.AddDays(-60), 4m, 175m, "OUV-2026-ART-005"),
            new StockMovementSeed("CONS-001", "PRINCIPAL", StockMovementType.OpeningBalance, today.AddDays(-60), 40m, 22m, "OUV-2026-CONS-001", "LOT-TONER-2025-Z"),
            new StockMovementSeed("ART-001", "PRINCIPAL", StockMovementType.Receipt, today.AddDays(-12), 1m, 980m, "REC-2026-0001", null, "SN-LIG-ART001-0001"),
            new StockMovementSeed("ART-001", "PRINCIPAL", StockMovementType.Receipt, today.AddDays(-12), 1m, 980m, "REC-2026-0001", null, "SN-LIG-ART001-0002"),
            new StockMovementSeed("ART-001", "PRINCIPAL", StockMovementType.Receipt, today.AddDays(-12), 1m, 980m, "REC-2026-0001", null, "SN-LIG-ART001-0003"),
            new StockMovementSeed("CONS-001", "PRINCIPAL", StockMovementType.Receipt, today.AddDays(-12), 20m, 22m, "REC-2026-0001", "LOT-TONER-2026-A", null, today.AddMonths(10)),
            new StockMovementSeed("ART-001", "PRINCIPAL", StockMovementType.Issue, today.AddDays(-14), -1m, 980m, "BL-2026-0001", null, "SN-LIG-ART001-0001"),
            new StockMovementSeed("ART-001", "PRINCIPAL", StockMovementType.Issue, today.AddDays(-14), -1m, 980m, "BL-2026-0001", null, "SN-LIG-ART001-0002"),
            new StockMovementSeed("ART-003", "SHOWROOM", StockMovementType.Issue, today.AddDays(-14), -2m, 85m, "BL-2026-0001"),
            new StockMovementSeed("ART-002", "PRINCIPAL", StockMovementType.Issue, today.AddDays(-30), -3m, 210m, "FAC-2026-0002"),
            new StockMovementSeed("CONS-001", "PRINCIPAL", StockMovementType.Issue, today.AddDays(-30), -6m, 22m, "FAC-2026-0002", "LOT-TONER-2025-Z"),
            new StockMovementSeed("CONS-001", "PRINCIPAL", StockMovementType.AdjustmentOut, today.AddDays(-4), -2m, 22m, "ADJ-2026-0001", "LOT-TONER-2025-Z"),
            new StockMovementSeed("ART-005", "SAV", StockMovementType.AdjustmentIn, today.AddDays(-3), 1m, 175m, "ADJ-2026-0002"),
            new StockMovementSeed("ART-004", "PRINCIPAL", StockMovementType.Reservation, today.AddDays(-2), -1m, 310m, "RES-2026-0001"),
            new StockMovementSeed("ART-004", "PRINCIPAL", StockMovementType.Release, today.AddDays(-1), 1m, 310m, "RES-2026-0001")
        });

        await EnsurePaymentsAsync(context, tenant.Id, tenant.CurrencyCode, partners, new[]
        {
            new PaymentSeed("REG-CLI-0001", today.AddDays(-8), PaymentDirection.Incoming, PaymentMethod.BankTransfer, "CLI-002", 1500m, "Reglement partiel client", new[] { new PaymentAllocationSeed("FAC-2026-0001", 1500m) }),
            new PaymentSeed("REG-CLI-0002", today.AddDays(-5), PaymentDirection.Incoming, PaymentMethod.Card, "CLI-003", salesInvoice3.TotalIncludingTax, "Reglement integral facture projet", new[] { new PaymentAllocationSeed("FAC-2026-0003", salesInvoice3.TotalIncludingTax) }),
            new PaymentSeed("REG-FOU-0001", today.AddDays(-4), PaymentDirection.Outgoing, PaymentMethod.BankTransfer, "FOU-002", 1800m, "Acompte fournisseur", new[] { new PaymentAllocationSeed("FAF-2026-0001", 1800m) })
        });

        await EnsureReminderLogsAsync(context, tenant.Id, new[]
        {
            new ReminderSeed("FAC-2026-0002", ReminderLevel.Friendly, today.AddDays(-3).ToDateTime(new TimeOnly(9, 15)), "Email", "Premiere relance automatique"),
            new ReminderSeed("FAC-2026-0002", ReminderLevel.Formal, today.AddDays(-1).ToDateTime(new TimeOnly(14, 30)), "Email", "Deuxieme relance avec copie comptabilite")
        });

        await EnsurePlatformInvoicesAsync(context, tenant.Id, subscription.Id, tenant.CurrencyCode, new[]
        {
            new PlatformInvoiceSeed("SAS-2026-0001", today.AddDays(-25), today.AddDays(-10), new DateOnly(today.Year, today.Month, 1).AddMonths(-1), new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)).AddMonths(-1), PlatformInvoiceStatus.Paid, today.AddDays(-7), "Abonnement SaaS Standard - mois precedent", 149m, 0m, 149m),
            new PlatformInvoiceSeed("SAS-2026-0002", today.AddDays(-3), today.AddDays(12), new DateOnly(today.Year, today.Month, 1), new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)), PlatformInvoiceStatus.Issued, null, "Abonnement SaaS Standard + depassements", 179m, 0m, 179m)
        });
    }

    private static async Task EnsurePilotTenantDataAsync(ApplicationDbContext context, Tenant tenant, TenantSubscription subscription, DateOnly today)
    {
        var paymentTerms = await EnsurePaymentTermsAsync(context, tenant.Id, new[]
        {
            new PaymentTermSeed("COMPT", "Comptant", 0),
            new PaymentTermSeed("07J", "7 jours", 7)
        });

        var taxCodes = await EnsureTaxCodesAsync(context, tenant.Id, new[]
        {
            new TaxCodeSeed("TPS", "TPS 5%", 5m)
        });

        var categories = await EnsureProductCategoriesAsync(context, tenant.Id, new[]
        {
            new CategorySeed("POS", "Point de vente"),
            new CategorySeed("SERVICE", "Services")
        });

        var warehouses = await EnsureWarehousesAsync(context, tenant.Id, new[]
        {
            new WarehouseSeed("MAIN", "Magasin principal", true)
        });

        var products = await EnsureProductsAsync(context, tenant.Id, categories, taxCodes, new[]
        {
            new ProductSeed("PRT-001", "Tablette point de vente", ProductType.StockItem, true, "UN", "POS", "TPS", 180m, 249m, "Terminal compact"),
            new ProductSeed("PRT-002", "Lecteur code-barres", ProductType.StockItem, true, "UN", "POS", "TPS", 90m, 149m, "Scanner USB"),
            new ProductSeed("PRT-003", "Imprimante ticket", ProductType.StockItem, true, "UN", "POS", "TPS", 120m, 199m, "Imprimante thermique"),
            new ProductSeed("CONS-ROLL", "Rouleau thermique", ProductType.StockItem, true, "UN", "POS", "TPS", 6m, 12m, "Consommable caisse"),
            new ProductSeed("SERV-SUP", "Support hotline", ProductType.Service, false, "MOIS", "SERVICE", "TPS", 0m, 90m, "Assistance mensuelle")
        });

        var partners = await EnsurePartnersAsync(context, tenant.Id, paymentTerms, new[]
        {
            new PartnerSeed("CUS-001", "Kiosque Alpha", BusinessPartnerType.Customer, "alpha@retail.test", "07J", 3000m, "Montreal", "Canada"),
            new PartnerSeed("CUS-002", "Magasin Beta", BusinessPartnerType.Customer, "beta@retail.test", "07J", 3000m, "Laval", "Canada"),
            new PartnerSeed("SUP-001", "Retail Source", BusinessPartnerType.Supplier, "supply@retailsource.test", "COMPT", 0m, "Toronto", "Canada")
        });

        await EnsureDocumentSequencesAsync(context, tenant.Id, new[]
        {
            new DocumentSequenceSeed(CommercialDocumentType.SalesQuote, "DEV-2026-", 102),
            new DocumentSequenceSeed(CommercialDocumentType.SalesOrder, "CMD-2026-", 102),
            new DocumentSequenceSeed(CommercialDocumentType.DeliveryNote, "BL-2026-", 102),
            new DocumentSequenceSeed(CommercialDocumentType.SalesInvoice, "FAC-2026-", 102),
            new DocumentSequenceSeed(CommercialDocumentType.SalesCreditNote, "AVO-2026-", 101),
            new DocumentSequenceSeed(CommercialDocumentType.PurchaseRequest, "DAP-2026-", 101),
            new DocumentSequenceSeed(CommercialDocumentType.PurchaseOrder, "ACH-2026-", 102),
            new DocumentSequenceSeed(CommercialDocumentType.GoodsReceipt, "REC-2026-", 101),
            new DocumentSequenceSeed(CommercialDocumentType.PurchaseInvoice, "FAF-2026-", 102),
            new DocumentSequenceSeed(CommercialDocumentType.SupplierCreditNote, "AVF-2026-", 101)
        });

        await EnsurePriceListsAsync(context, tenant.Id, tenant.CurrencyCode, products, new[]
        {
            new PriceListSeed("STANDARD", "Tarif standard", true,
                new PriceListLineSeed("PRT-001", 249m),
                new PriceListLineSeed("PRT-002", 149m),
                new PriceListLineSeed("PRT-003", 199m),
                new PriceListLineSeed("CONS-ROLL", 12m),
                new PriceListLineSeed("SERV-SUP", 90m))
        });

        var quote = await EnsureDocumentAsync(context, tenant.Id, "DEV-2026-0101", CommercialDocumentType.SalesQuote, CommercialDocumentStatus.Open, partners["CUS-001"], warehouses["MAIN"], today.AddDays(-6), today.AddDays(7), tenant.CurrencyCode, "Devis pilote point de vente", null, products, new[]
        {
            new DocumentLineSeed("PRT-001", 1m, 249m, 5m),
            new DocumentLineSeed("PRT-002", 1m, 149m, 5m)
        });

        var order = await EnsureDocumentAsync(context, tenant.Id, "CMD-2026-0101", CommercialDocumentType.SalesOrder, CommercialDocumentStatus.Open, partners["CUS-001"], warehouses["MAIN"], today.AddDays(-5), today.AddDays(7), tenant.CurrencyCode, "Commande pilote", quote, products, new[]
        {
            new DocumentLineSeed("PRT-001", 1m, 249m, 5m),
            new DocumentLineSeed("PRT-002", 1m, 149m, 5m)
        });

        var delivery = await EnsureDocumentAsync(context, tenant.Id, "BL-2026-0101", CommercialDocumentType.DeliveryNote, CommercialDocumentStatus.Completed, partners["CUS-001"], warehouses["MAIN"], today.AddDays(-4), null, tenant.CurrencyCode, "Livraison pilote", order, products, new[]
        {
            new DocumentLineSeed("PRT-001", 1m, 249m, 5m)
        });

        await EnsureDocumentAsync(context, tenant.Id, "FAC-2026-0101", CommercialDocumentType.SalesInvoice, CommercialDocumentStatus.Open, partners["CUS-001"], warehouses["MAIN"], today.AddDays(-3), today.AddDays(4), tenant.CurrencyCode, "Facture pilote ouverte", delivery, products, new[]
        {
            new DocumentLineSeed("PRT-001", 1m, 249m, 5m),
            new DocumentLineSeed("SERV-SUP", 1m, 90m, 5m)
        });

        await EnsureDocumentAsync(context, tenant.Id, "ACH-2026-0101", CommercialDocumentType.PurchaseOrder, CommercialDocumentStatus.Open, partners["SUP-001"], warehouses["MAIN"], today.AddDays(-2), null, tenant.CurrencyCode, "Commande fournisseur pilote", null, products, new[]
        {
            new DocumentLineSeed("PRT-003", 2m, 120m, 5m),
            new DocumentLineSeed("CONS-ROLL", 20m, 6m, 5m)
        });

        await EnsureDocumentAsync(context, tenant.Id, "FAF-2026-0101", CommercialDocumentType.PurchaseInvoice, CommercialDocumentStatus.Open, partners["SUP-001"], warehouses["MAIN"], today.AddDays(-1), today.AddDays(6), tenant.CurrencyCode, "Facture fournisseur pilote", null, products, new[]
        {
            new DocumentLineSeed("PRT-003", 1m, 120m, 5m),
            new DocumentLineSeed("CONS-ROLL", 10m, 6m, 5m)
        });

        await EnsureStockMovementsAsync(context, tenant.Id, products, warehouses, new[]
        {
            new StockMovementSeed("PRT-001", "MAIN", StockMovementType.OpeningBalance, today.AddDays(-20), 2m, 180m, "PILOT-OUV-001"),
            new StockMovementSeed("PRT-002", "MAIN", StockMovementType.OpeningBalance, today.AddDays(-20), 3m, 90m, "PILOT-OUV-002"),
            new StockMovementSeed("PRT-003", "MAIN", StockMovementType.OpeningBalance, today.AddDays(-20), 2m, 120m, "PILOT-OUV-003"),
            new StockMovementSeed("CONS-ROLL", "MAIN", StockMovementType.OpeningBalance, today.AddDays(-20), 25m, 6m, "PILOT-OUV-004"),
            new StockMovementSeed("PRT-001", "MAIN", StockMovementType.Issue, today.AddDays(-4), -1m, 180m, "BL-2026-0101")
        });

        await EnsurePlatformInvoicesAsync(context, tenant.Id, subscription.Id, tenant.CurrencyCode, new[]
        {
            new PlatformInvoiceSeed("SAS-2026-0101", today.AddDays(-18), today.AddDays(-3), new DateOnly(today.Year, today.Month, 1).AddMonths(-1), new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)).AddMonths(-1), PlatformInvoiceStatus.Overdue, null, "Facture pilote en retard", 79m, 0m, 79m)
        });
    }

    private static async Task<Dictionary<string, PaymentTerm>> EnsurePaymentTermsAsync(ApplicationDbContext context, Guid tenantId, IReadOnlyCollection<PaymentTermSeed> seeds)
    {
        var items = await context.PaymentTerms.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Code);

        foreach (var seed in seeds)
        {
            if (!items.TryGetValue(seed.Code, out var item))
            {
                item = new PaymentTerm { TenantId = tenantId, Code = seed.Code };
                context.PaymentTerms.Add(item);
                items[seed.Code] = item;
            }

            item.Label = seed.Label;
            item.DueInDays = seed.DueInDays;
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        return items;
    }

    private static async Task<Dictionary<string, TaxCode>> EnsureTaxCodesAsync(ApplicationDbContext context, Guid tenantId, IReadOnlyCollection<TaxCodeSeed> seeds)
    {
        var items = await context.TaxCodes.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Code);

        foreach (var seed in seeds)
        {
            if (!items.TryGetValue(seed.Code, out var item))
            {
                item = new TaxCode { TenantId = tenantId, Code = seed.Code };
                context.TaxCodes.Add(item);
                items[seed.Code] = item;
            }

            item.Label = seed.Label;
            item.Rate = seed.Rate;
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        return items;
    }

    private static async Task<Dictionary<string, ProductCategory>> EnsureProductCategoriesAsync(ApplicationDbContext context, Guid tenantId, IReadOnlyCollection<CategorySeed> seeds)
    {
        var items = await context.ProductCategories.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Code);

        foreach (var seed in seeds)
        {
            if (!items.TryGetValue(seed.Code, out var item))
            {
                item = new ProductCategory { TenantId = tenantId, Code = seed.Code };
                context.ProductCategories.Add(item);
                items[seed.Code] = item;
            }

            item.Label = seed.Label;
            item.StockValuationMethod = seed.StockValuationMethod;
            item.StockIdentityTrackingMode = seed.StockIdentityTrackingMode;
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        return items;
    }

    private static async Task<Dictionary<string, Warehouse>> EnsureWarehousesAsync(ApplicationDbContext context, Guid tenantId, IReadOnlyCollection<WarehouseSeed> seeds)
    {
        var items = await context.Warehouses.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Code);

        foreach (var seed in seeds)
        {
            if (!items.TryGetValue(seed.Code, out var item))
            {
                item = new Warehouse { TenantId = tenantId, Code = seed.Code };
                context.Warehouses.Add(item);
                items[seed.Code] = item;
            }

            item.Label = seed.Label;
            item.IsDefault = seed.IsDefault;
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        return items;
    }

    private static async Task<Dictionary<string, Product>> EnsureProductsAsync(
        ApplicationDbContext context,
        Guid tenantId,
        IReadOnlyDictionary<string, ProductCategory> categories,
        IReadOnlyDictionary<string, TaxCode> taxCodes,
        IReadOnlyCollection<ProductSeed> seeds)
    {
        var items = await context.Products.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Sku);

        foreach (var seed in seeds)
        {
            if (!items.TryGetValue(seed.Sku, out var item))
            {
                item = new Product { TenantId = tenantId, Sku = seed.Sku };
                context.Products.Add(item);
                items[seed.Sku] = item;
            }

            item.Label = seed.Label;
            item.Description = seed.Description;
            item.ProductType = seed.ProductType;
            item.TrackStock = seed.TrackStock;
            item.StockValuationMethod = seed.StockValuationMethod;
            item.StockIdentityTrackingMode = seed.StockIdentityTrackingMode;
            item.IsActive = true;
            item.UnitOfMeasure = seed.UnitOfMeasure;
            item.ProductCategoryId = categories[seed.CategoryCode].Id;
            item.TaxCodeId = taxCodes[seed.TaxCodeCode].Id;
            item.PurchasePrice = seed.PurchasePrice;
            item.SalesPrice = seed.SalesPrice;
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        return items;
    }

    private static async Task<Dictionary<string, BusinessPartner>> EnsurePartnersAsync(
        ApplicationDbContext context,
        Guid tenantId,
        IReadOnlyDictionary<string, PaymentTerm> paymentTerms,
        IReadOnlyCollection<PartnerSeed> seeds)
    {
        var items = await context.BusinessPartners.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Code);

        foreach (var seed in seeds)
        {
            if (!items.TryGetValue(seed.Code, out var item))
            {
                item = new BusinessPartner { TenantId = tenantId, Code = seed.Code };
                context.BusinessPartners.Add(item);
                items[seed.Code] = item;
            }

            item.Name = seed.Name;
            item.PartnerType = seed.PartnerType;
            item.Email = seed.Email;
            item.CreditLimit = seed.CreditLimit;
            item.IsActive = seed.IsActive;
            item.PaymentTermId = paymentTerms[seed.PaymentTermCode].Id;
            item.BillingAddress = new Address
            {
                Recipient = seed.Name,
                StreetLine1 = "125 rue de la Demonstration",
                PostalCode = "H2X 1Y4",
                City = seed.City,
                Country = seed.Country
            };
            item.ShippingAddress = new Address
            {
                Recipient = seed.Name,
                StreetLine1 = "125 rue de la Demonstration",
                PostalCode = "H2X 1Y4",
                City = seed.City,
                Country = seed.Country
            };
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        return items;
    }

    private static async Task EnsureDocumentSequencesAsync(ApplicationDbContext context, Guid tenantId, IReadOnlyCollection<DocumentSequenceSeed> seeds)
    {
        var items = await context.DocumentSequences.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.DocumentType);

        foreach (var seed in seeds)
        {
            if (!items.TryGetValue(seed.DocumentType, out var item))
            {
                item = new DocumentSequence { TenantId = tenantId, DocumentType = seed.DocumentType };
                context.DocumentSequences.Add(item);
                items[seed.DocumentType] = item;
            }

            item.Prefix = seed.Prefix;
            item.NextValue = Math.Max(item.NextValue, seed.NextValue);
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsurePriceListsAsync(
        ApplicationDbContext context,
        Guid tenantId,
        string currencyCode,
        IReadOnlyDictionary<string, Product> products,
        IReadOnlyCollection<PriceListSeed> seeds)
    {
        var existingCodes = await context.PriceLists
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Code)
            .ToListAsync();

        foreach (var seed in seeds)
        {
            if (existingCodes.Contains(seed.Code, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var priceList = new PriceList
            {
                TenantId = tenantId,
                Code = seed.Code,
                Label = seed.Label,
                CurrencyCode = currencyCode,
                IsDefault = seed.IsDefault
            };

            foreach (var line in seed.Lines)
            {
                priceList.Lines.Add(new PriceListLine
                {
                    ProductId = products[line.ProductSku].Id,
                    UnitPrice = line.UnitPrice,
                    ValidFrom = line.ValidFrom,
                    ValidTo = line.ValidTo
                });
            }

            context.PriceLists.Add(priceList);
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task<CommercialDocument> EnsureDocumentAsync(
        ApplicationDbContext context,
        Guid tenantId,
        string number,
        CommercialDocumentType documentType,
        CommercialDocumentStatus status,
        BusinessPartner partner,
        Warehouse? warehouse,
        DateOnly documentDate,
        DateOnly? dueDate,
        string currencyCode,
        string? notes,
        CommercialDocument? sourceDocument,
        IReadOnlyDictionary<string, Product> products,
        IReadOnlyCollection<DocumentLineSeed> lines)
    {
        var existing = await context.CommercialDocuments.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Number == number);
        if (existing is not null)
        {
            return existing;
        }

        var document = new CommercialDocument
        {
            TenantId = tenantId,
            Number = number,
            DocumentType = documentType,
            Status = status,
            PartnerId = partner.Id,
            WarehouseId = warehouse?.Id,
            DocumentDate = documentDate,
            DueDate = dueDate,
            CurrencyCode = currencyCode,
            Notes = notes,
            SourceDocumentId = sourceDocument?.Id
        };

        foreach (var seed in lines)
        {
            var product = products[seed.ProductSku];
            var netAmount = decimal.Round(seed.Quantity * seed.UnitPriceExcludingTax * (1m - (seed.DiscountRate / 100m)), 2);
            var taxAmount = decimal.Round(netAmount * (seed.TaxRate / 100m), 2);

            document.Lines.Add(new CommercialDocumentLine
            {
                ProductId = product.Id,
                Description = string.IsNullOrWhiteSpace(seed.Description) ? product.Label : seed.Description,
                Quantity = seed.Quantity,
                UnitPriceExcludingTax = seed.UnitPriceExcludingTax,
                DiscountRate = seed.DiscountRate,
                TaxRate = seed.TaxRate,
                LineTotalExcludingTax = netAmount,
                LineTaxAmount = taxAmount,
                LineTotalIncludingTax = netAmount + taxAmount,
                LotNumber = seed.LotNumber,
                SerialNumber = seed.SerialNumber,
                ExpirationDate = seed.ExpirationDate
            });
        }

        document.TotalExcludingTax = document.Lines.Sum(x => x.LineTotalExcludingTax);
        document.TotalTax = document.Lines.Sum(x => x.LineTaxAmount);
        document.TotalIncludingTax = document.Lines.Sum(x => x.LineTotalIncludingTax);

        context.CommercialDocuments.Add(document);
        await context.SaveChangesAsync();
        return document;
    }

    private static async Task EnsureStockMovementsAsync(
        ApplicationDbContext context,
        Guid tenantId,
        IReadOnlyDictionary<string, Product> products,
        IReadOnlyDictionary<string, Warehouse> warehouses,
        IReadOnlyCollection<StockMovementSeed> seeds)
    {
        var existingKeys = await context.StockMovements
            .Where(x => x.TenantId == tenantId)
            .Select(x => new StockMovementKey(x.ProductId, x.WarehouseId, x.MovementType, x.MovementDate, x.Quantity, x.ReferenceNumber ?? string.Empty, x.LotNumber ?? string.Empty, x.SerialNumber ?? string.Empty))
            .ToListAsync();

        var keySet = existingKeys.ToHashSet();

        foreach (var seed in seeds)
        {
            var key = new StockMovementKey(
                products[seed.ProductSku].Id,
                warehouses[seed.WarehouseCode].Id,
                seed.MovementType,
                seed.MovementDate,
                seed.Quantity,
                seed.ReferenceNumber,
                seed.LotNumber ?? string.Empty,
                seed.SerialNumber ?? string.Empty);

            if (keySet.Contains(key))
            {
                continue;
            }

            context.StockMovements.Add(new StockMovement
            {
                TenantId = tenantId,
                ProductId = key.ProductId,
                WarehouseId = key.WarehouseId,
                MovementType = seed.MovementType,
                MovementDate = seed.MovementDate,
                Quantity = seed.Quantity,
                UnitCost = seed.UnitCost,
                ReferenceNumber = seed.ReferenceNumber,
                LotNumber = seed.LotNumber,
                SerialNumber = seed.SerialNumber,
                ExpirationDate = seed.ExpirationDate
            });

            keySet.Add(key);
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsurePaymentsAsync(
        ApplicationDbContext context,
        Guid tenantId,
        string currencyCode,
        IReadOnlyDictionary<string, BusinessPartner> partners,
        IReadOnlyCollection<PaymentSeed> seeds)
    {
        var documents = await context.CommercialDocuments.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Number);
        var existingReferences = await context.Payments.Where(x => x.TenantId == tenantId).Select(x => x.ReferenceNumber).ToListAsync();

        foreach (var seed in seeds)
        {
            if (existingReferences.Contains(seed.ReferenceNumber, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var payment = new Payment
            {
                TenantId = tenantId,
                PaymentDate = seed.PaymentDate,
                Direction = seed.Direction,
                Method = seed.Method,
                ReferenceNumber = seed.ReferenceNumber,
                CurrencyCode = currencyCode,
                Amount = seed.Amount,
                Notes = seed.Notes,
                PartnerId = partners[seed.PartnerCode].Id
            };

            foreach (var allocation in seed.Allocations)
            {
                payment.Allocations.Add(new PaymentAllocation
                {
                    CommercialDocumentId = documents[allocation.DocumentNumber].Id,
                    AllocatedAmount = allocation.AllocatedAmount
                });
            }

            context.Payments.Add(payment);
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureReminderLogsAsync(ApplicationDbContext context, Guid tenantId, IReadOnlyCollection<ReminderSeed> seeds)
    {
        var documents = await context.CommercialDocuments.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Number);
        var existingKeys = await context.ReminderLogs
            .Where(x => x.TenantId == tenantId)
            .Select(x => new ReminderKey(x.CommercialDocumentId, x.ReminderLevel, x.Channel))
            .ToListAsync();

        var keySet = existingKeys.ToHashSet();

        foreach (var seed in seeds)
        {
            var key = new ReminderKey(documents[seed.DocumentNumber].Id, seed.ReminderLevel, seed.Channel);
            if (keySet.Contains(key))
            {
                continue;
            }

            context.ReminderLogs.Add(new ReminderLog
            {
                TenantId = tenantId,
                CommercialDocumentId = key.DocumentId,
                ReminderLevel = seed.ReminderLevel,
                SentOnUtc = seed.SentOnUtc,
                Channel = seed.Channel,
                Notes = seed.Notes
            });

            keySet.Add(key);
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsurePlatformInvoicesAsync(
        ApplicationDbContext context,
        Guid tenantId,
        Guid tenantSubscriptionId,
        string currencyCode,
        IReadOnlyCollection<PlatformInvoiceSeed> seeds)
    {
        var existingNumbers = await context.PlatformInvoices.Where(x => x.TenantId == tenantId).Select(x => x.InvoiceNumber).ToListAsync();

        foreach (var seed in seeds)
        {
            if (existingNumbers.Contains(seed.InvoiceNumber, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var invoice = new PlatformInvoice
            {
                TenantId = tenantId,
                TenantSubscriptionId = tenantSubscriptionId,
                InvoiceNumber = seed.InvoiceNumber,
                IssueDate = seed.IssueDate,
                DueDate = seed.DueDate,
                PeriodStart = seed.PeriodStart,
                PeriodEnd = seed.PeriodEnd,
                Status = seed.Status,
                CurrencyCode = currencyCode,
                PaidOn = seed.PaidOn,
                TotalExcludingTax = seed.TotalExcludingTax,
                TotalTax = seed.TotalTax,
                TotalIncludingTax = seed.TotalIncludingTax,
                Notes = seed.Description
            };

            invoice.Lines.Add(new PlatformInvoiceLine
            {
                Description = seed.Description,
                Quantity = 1m,
                UnitPriceExcludingTax = seed.TotalExcludingTax,
                TaxRate = 0m,
                LineTotalExcludingTax = seed.TotalExcludingTax,
                LineTaxAmount = seed.TotalTax,
                LineTotalIncludingTax = seed.TotalIncludingTax
            });

            context.PlatformInvoices.Add(invoice);
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureApplicationUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string firstName,
        string lastName,
        Guid? tenantId,
        IReadOnlyCollection<string> roles)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                TenantId = tenantId
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                var message = string.Join(", ", createResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Impossible de creer l'utilisateur {email}: {message}");
            }
        }
        else if (user.FirstName != firstName || user.LastName != lastName || user.TenantId != tenantId)
        {
            user.FirstName = firstName;
            user.LastName = lastName;
            user.TenantId = tenantId;

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var message = string.Join(", ", updateResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Impossible de mettre a jour l'utilisateur {email}: {message}");
            }
        }

        foreach (var role in roles)
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }

    private static async Task EnsureInvitationAsync(
        ApplicationDbContext context,
        Guid tenantId,
        string email,
        string firstName,
        string lastName,
        string requestedRoles,
        UserInvitationStatus status,
        DateTime expiresOnUtc,
        string notes)
    {
        var invitation = await context.UserInvitations.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email == email);
        if (invitation is null)
        {
            invitation = new UserInvitation
            {
                TenantId = tenantId,
                Email = email,
                InvitationToken = Guid.NewGuid().ToString("N")
            };

            context.UserInvitations.Add(invitation);
        }

        invitation.FirstName = firstName;
        invitation.LastName = lastName;
        invitation.RequestedRoles = requestedRoles;
        invitation.Status = status;
        invitation.ExpiresOnUtc = expiresOnUtc;
        invitation.Notes = notes;
        invitation.CancelledOnUtc = status == UserInvitationStatus.Cancelled ? DateTime.UtcNow : null;
        invitation.AcceptedOnUtc = status == UserInvitationStatus.Accepted ? DateTime.UtcNow : null;

        await context.SaveChangesAsync();
    }

    private sealed record SubscriptionPlanSpec(string Code, string Label, TenantEdition Edition, decimal MonthlyPrice, int MaxUsers, int MaxCustomers, int MaxSuppliers, int MaxProducts, int MaxWarehouses, int MaxMonthlyDocuments, decimal OverageUserPrice, decimal OverageProductPrice, decimal OverageDocumentPrice, bool IncludesAdvancedStock, bool IncludesPurchasing, bool IncludesBusinessIntelligence);
    private sealed record PaymentTermSeed(string Code, string Label, int DueInDays);
    private sealed record TaxCodeSeed(string Code, string Label, decimal Rate);
    private sealed record CategorySeed(string Code, string Label, StockValuationMethod StockValuationMethod = StockValuationMethod.Cmup, StockIdentityTrackingMode StockIdentityTrackingMode = StockIdentityTrackingMode.None);
    private sealed record WarehouseSeed(string Code, string Label, bool IsDefault);
    private sealed record ProductSeed(string Sku, string Label, ProductType ProductType, bool TrackStock, string UnitOfMeasure, string CategoryCode, string TaxCodeCode, decimal PurchasePrice, decimal SalesPrice, string? Description, StockValuationMethod StockValuationMethod = StockValuationMethod.Cmup, StockIdentityTrackingMode StockIdentityTrackingMode = StockIdentityTrackingMode.None);
    private sealed record PartnerSeed(string Code, string Name, BusinessPartnerType PartnerType, string Email, string PaymentTermCode, decimal CreditLimit, string City, string Country, bool IsActive = true);
    private sealed record DocumentSequenceSeed(CommercialDocumentType DocumentType, string Prefix, int NextValue);
    private sealed record PriceListSeed(string Code, string Label, bool IsDefault, params PriceListLineSeed[] Lines);
    private sealed record PriceListLineSeed(string ProductSku, decimal UnitPrice, DateOnly? ValidFrom = null, DateOnly? ValidTo = null);
    private sealed record DocumentLineSeed(string ProductSku, decimal Quantity, decimal UnitPriceExcludingTax, decimal TaxRate, decimal DiscountRate = 0m, string? Description = null, string? LotNumber = null, string? SerialNumber = null, DateOnly? ExpirationDate = null);
    private sealed record StockMovementSeed(string ProductSku, string WarehouseCode, StockMovementType MovementType, DateOnly MovementDate, decimal Quantity, decimal UnitCost, string ReferenceNumber, string? LotNumber = null, string? SerialNumber = null, DateOnly? ExpirationDate = null);
    private sealed record StockMovementKey(Guid ProductId, Guid WarehouseId, StockMovementType MovementType, DateOnly MovementDate, decimal Quantity, string ReferenceNumber, string LotNumber, string SerialNumber);
    private sealed record PaymentSeed(string ReferenceNumber, DateOnly PaymentDate, PaymentDirection Direction, PaymentMethod Method, string PartnerCode, decimal Amount, string Notes, IReadOnlyCollection<PaymentAllocationSeed> Allocations);
    private sealed record PaymentAllocationSeed(string DocumentNumber, decimal AllocatedAmount);
    private sealed record ReminderSeed(string DocumentNumber, ReminderLevel ReminderLevel, DateTime SentOnUtc, string Channel, string Notes);
    private sealed record ReminderKey(Guid DocumentId, ReminderLevel ReminderLevel, string Channel);
    private sealed record PlatformInvoiceSeed(string InvoiceNumber, DateOnly IssueDate, DateOnly DueDate, DateOnly PeriodStart, DateOnly PeriodEnd, PlatformInvoiceStatus Status, DateOnly? PaidOn, string Description, decimal TotalExcludingTax, decimal TotalTax, decimal TotalIncludingTax);
}
