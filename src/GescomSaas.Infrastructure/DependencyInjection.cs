using GescomSaas.Application.Contracts;
using GescomSaas.Infrastructure.MultiTenancy;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GescomSaas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
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
        services.AddScoped<ISageImportService, SageImportService>();
        services.AddScoped<ISettlementService, SettlementService>();
        services.AddScoped<ITenantDisplayFormatter, TenantDisplayFormatter>();
        services.AddScoped<ITenantQuotaEnforcementService, TenantQuotaEnforcementService>();

        return services;
    }

    public static async Task SeedApplicationAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await ApplicationDbContextSeed.SeedAsync(scope.ServiceProvider);
    }
}
