namespace GescomSaas.Web.Pages.Settings;

public class SageImportHistoryItem
{
    public Guid Id { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public bool IsDryRun { get; set; }
    public bool IsSuccessful { get; set; }
    public string SourceServer { get; set; } = string.Empty;
    public string SourceDatabase { get; set; } = string.Empty;
    public string ImportModeLabel { get; set; } = string.Empty;
    public int TotalImported { get; set; }
    public int TotalUpdated { get; set; }
    public int TotalSkipped { get; set; }
    public string WarningSummary { get; set; } = string.Empty;
    public IReadOnlyList<SageImportHistoryModuleItem> Modules { get; set; } = [];
}

public class SageImportHistoryModuleItem
{
    public string ModuleName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public int Imported { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public string Summary { get; set; } = string.Empty;
}
