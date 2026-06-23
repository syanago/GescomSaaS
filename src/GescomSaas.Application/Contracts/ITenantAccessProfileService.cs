using GescomSaas.Application.Models;

namespace GescomSaas.Application.Contracts;

public interface ITenantAccessProfileService
{
    Task<TenantAccessProfileSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> SaveProfileAsync(Guid tenantId, TenantAccessProfileUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(Guid tenantId, Guid profileId, CancellationToken cancellationToken = default);
    Task UpdateUserAssignmentsAsync(Guid tenantId, TenantAccessUserAssignmentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> GetEffectivePermissionKeysAsync(Guid tenantId, string userId, CancellationToken cancellationToken = default);
    Task<int> EnsureStandardProfilesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
