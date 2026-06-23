using GescomSaas.Application.Contracts;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.MultiTenancy;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GescomSaas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, LigComDatabaseProvider databaseProvider, string connectionString)
    {
        services.AddHttpClient();
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (databaseProvider == LigComDatabaseProvider.Sqlite)
            {
                options.UseSqlite(connectionString);
                return;
            }

            options.UseSqlServer(connectionString);
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantAccessor, HttpContextTenantAccessor>();
        services.AddScoped<ICommercialDashboardService, CommercialDashboardService>();
        services.AddScoped<ICommercialDocumentWorkflowService, CommercialDocumentWorkflowService>();
        services.AddScoped<ICommercialDocumentPdfService, CommercialDocumentPdfService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IStockDocumentService, StockDocumentService>();
        services.AddScoped<INumberingService, NumberingService>();
        services.AddScoped<IPlatformAdministrationService, PlatformAdministrationService>();
        services.AddScoped<IPlatformInvoicePdfService, PlatformInvoicePdfService>();
        services.AddScoped<PlatformNotificationEmailService>();
        services.AddScoped<IPlatformUserAdministrationService, PlatformUserAdministrationService>();
        services.AddScoped<IRuntimeInitializationStateService, RuntimeInitializationStateService>();
        services.AddScoped<ISageImportService, SageImportService>();
        services.AddScoped<ISettlementService, SettlementService>();
        services.AddScoped<IOfflineSyncService, OfflineSyncService>();
        services.AddScoped<ITenantAccessProfileService, TenantAccessProfileService>();
        services.AddScoped<ITenantDisplayFormatter, TenantDisplayFormatter>();
        services.AddScoped<ITenantQuotaEnforcementService, TenantQuotaEnforcementService>();
        services.AddScoped<IUserPermissionService, UserPermissionService>();

        return services;
    }

    public static async Task InitializeRuntimeAsync(this IServiceProvider services, LigComRuntimeOptions runtimeOptions)
    {
        if (runtimeOptions.DatabaseProvider != LigComDatabaseProvider.Sqlite
            && !runtimeOptions.InitializeDatabaseOnStartup)
        {
            return;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (runtimeOptions.DatabaseProvider == LigComDatabaseProvider.Sqlite)
        {
            // LocalNode offline : pas de migrations versionnees, schema cree en une passe.
            // Le mode SQLite est concu pour des installations clients standalone, pas pour
            // une evolution de schema continue.
            await dbContext.Database.EnsureCreatedAsync();
            return;
        }

        // SQL Server (cloud / multi-tenant) : migrations versionnees obligatoires.
        // Si la base existante a ete creee via EnsureCreated (legacy), executez
        // scripts/migrate-from-ensurecreated.sql avant le premier deploiement avec migrations.
        await dbContext.Database.MigrateAsync();
    }

    public static async Task SeedApplicationAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await ApplicationDbContextSeed.SeedAsync(scope.ServiceProvider);
    }
}
