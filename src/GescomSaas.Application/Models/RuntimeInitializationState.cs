namespace GescomSaas.Application.Models;

public sealed record RuntimeInitializationState(
    bool IsConfiguredForLocalNode,
    bool IsOfflineSyncConfigured,
    bool HasTenantData,
    bool HasAdminUser,
    bool IsReady)
{
    public string RuntimeDisplayMode =>
        !IsConfiguredForLocalNode
            ? "Central"
            : IsReady
                ? "LocalNode"
                : "LocalNodePending";
}
