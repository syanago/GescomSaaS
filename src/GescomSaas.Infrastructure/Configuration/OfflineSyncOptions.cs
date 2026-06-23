namespace GescomSaas.Infrastructure.Configuration;

public sealed class OfflineSyncOptions
{
    public const string SectionName = "OfflineSync";

    public bool Enabled { get; set; }
    public bool RequireManualTrigger { get; set; } = true;
    public bool AllowPush { get; set; } = true;
    public bool AllowPull { get; set; } = true;
    public string LocalNodeId { get; set; } = string.Empty;
    public string CentralBaseUrl { get; set; } = string.Empty;
    public string SharedAccessKey { get; set; } = string.Empty;
    public string StateFileName { get; set; } = "offline-sync-state.json";
}
