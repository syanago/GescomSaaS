namespace GescomSaas.Web.Pages.Settings;

public class SageConnectionReport
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string LoginName { get; set; } = string.Empty;
    public string SqlVersion { get; set; } = string.Empty;
    public int TableCount { get; set; }
    public IReadOnlyList<string> SampleTables { get; set; } = [];
}
