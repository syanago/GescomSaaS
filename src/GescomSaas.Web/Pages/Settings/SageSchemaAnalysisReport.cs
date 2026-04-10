namespace GescomSaas.Web.Pages.Settings;

public class SageSchemaAnalysisReport
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string InferenceNote { get; set; } = string.Empty;
    public int TableCount { get; set; }
    public IReadOnlyList<SageSchemaTableProfile> KeyTables { get; set; } = [];
    public IReadOnlyList<SageSchemaMappingSuggestion> Suggestions { get; set; } = [];
}

public class SageSchemaTableProfile
{
    public string TableName { get; set; } = string.Empty;
    public int ColumnCount { get; set; }
    public IReadOnlyList<string> SampleColumns { get; set; } = [];
}

public class SageSchemaMappingSuggestion
{
    public string LigComTarget { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public string ConfidenceLabel { get; set; } = string.Empty;
    public string MappingSummary { get; set; } = string.Empty;
    public IReadOnlyList<string> SuggestedColumns { get; set; } = [];
}
