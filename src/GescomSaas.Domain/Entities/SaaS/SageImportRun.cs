using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.SaaS;

public class SageImportRun : TenantEntity
{
    public bool IsDryRun { get; set; }
    public bool IsSuccessful { get; set; }
    public string SourceServer { get; set; } = string.Empty;
    public string SourceDatabase { get; set; } = string.Empty;
    public SageImportMode ImportMode { get; set; } = SageImportMode.Partial;
    public int TotalImported { get; set; }
    public int TotalUpdated { get; set; }
    public int TotalSkipped { get; set; }
    public string WarningSummary { get; set; } = string.Empty;

    public ICollection<SageImportRunModule> Modules { get; set; } = new List<SageImportRunModule>();
}
