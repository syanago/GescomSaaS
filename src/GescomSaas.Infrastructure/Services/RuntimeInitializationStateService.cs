using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GescomSaas.Infrastructure.Services;

public sealed class RuntimeInitializationStateService(
    ApplicationDbContext dbContext,
    IOptions<LigComRuntimeOptions> runtimeOptions,
    IOptions<OfflineSyncOptions> offlineSyncOptions) : IRuntimeInitializationStateService
{
    private readonly LigComRuntimeOptions runtime = runtimeOptions.Value;
    private readonly OfflineSyncOptions offline = offlineSyncOptions.Value;

    public async Task<RuntimeInitializationState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var configuredForLocalNode = runtime.Mode == LigComNodeMode.LocalNode
            && runtime.DatabaseProvider == LigComDatabaseProvider.Sqlite;

        var offlineConfigured = offline.Enabled
            && !string.IsNullOrWhiteSpace(offline.CentralBaseUrl)
            && !string.IsNullOrWhiteSpace(offline.SharedAccessKey);

        if (!configuredForLocalNode)
        {
            return new RuntimeInitializationState(false, offlineConfigured, false, false, false);
        }

        try
        {
            var hasTenantData = await dbContext.Tenants.AsNoTracking().AnyAsync(cancellationToken);
            var hasAdminUser = await dbContext.Users.AsNoTracking().AnyAsync(cancellationToken);
            return new RuntimeInitializationState(
                true,
                offlineConfigured,
                hasTenantData,
                hasAdminUser,
                offlineConfigured && hasTenantData && hasAdminUser);
        }
        catch
        {
            return new RuntimeInitializationState(configuredForLocalNode, offlineConfigured, false, false, false);
        }
    }
}
