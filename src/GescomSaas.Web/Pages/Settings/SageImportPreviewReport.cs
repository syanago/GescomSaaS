namespace GescomSaas.Web.Pages.Settings;

public class SageImportPreviewReport
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<SageImportPreviewModule> Modules { get; set; } = [];
}

public class SageImportPreviewModule
{
    public string ModuleName { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public int SourceRowCount { get; set; }
    public IReadOnlyList<string> SampleColumns { get; set; } = [];
    public IReadOnlyList<SageImportPreviewRow> Rows { get; set; } = [];
}

public class SageImportPreviewRow
{
    public IReadOnlyList<SageImportPreviewCell> Cells { get; set; } = [];
}

public class SageImportPreviewCell
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
