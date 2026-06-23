using GescomSaas.Application.Models;

namespace GescomSaas.Application.Contracts;

public interface IOfflineSyncService
{
    Task<OfflineSyncDashboard> GetDashboardAsync(Guid tenantId, string tenantName, CancellationToken cancellationToken = default);
    Task<OfflineSyncExecutionResult> PushToCentralAsync(Guid tenantId, string triggeredBy, CancellationToken cancellationToken = default);
    Task<OfflineSyncExecutionResult> PullFromCentralAsync(Guid tenantId, string triggeredBy, CancellationToken cancellationToken = default);
    Task<OfflineSyncExecutionResult> RefreshLocalFromCentralAsync(Guid tenantId, string tenantSlug, string triggeredBy, CancellationToken cancellationToken = default);
    Task<bool> ResolveConflictAsync(Guid tenantId, Guid conflictId, string resolvedBy, string? resolutionNote, bool ignored, CancellationToken cancellationToken = default);
    Task<OfflineNodeBootstrapResult> BootstrapLocalNodeAsync(OfflineNodeBootstrapRequest request, CancellationToken cancellationToken = default);
}
