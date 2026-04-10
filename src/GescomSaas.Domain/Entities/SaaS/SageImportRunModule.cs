using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.SaaS;

public class SageImportRunModule : AuditableEntity
{
    public Guid SageImportRunId { get; set; }
    public SageImportRun? SageImportRun { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public int Imported { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string NoteSummary { get; set; } = string.Empty;
}
