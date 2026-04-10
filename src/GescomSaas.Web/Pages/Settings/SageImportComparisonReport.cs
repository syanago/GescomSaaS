namespace GescomSaas.Web.Pages.Settings;

public class SageImportComparisonReport
{
    public string LeftLabel { get; set; } = string.Empty;
    public string RightLabel { get; set; } = string.Empty;
    public int DifferenceCount { get; set; }
    public IReadOnlyList<SageImportComparisonSection> Sections { get; set; } = [];
}

public class SageImportComparisonSection
{
    public string SectionLabel { get; set; } = string.Empty;
    public IReadOnlyList<SageImportComparisonItem> Items { get; set; } = [];
}

public class SageImportComparisonItem
{
    public string FieldLabel { get; set; } = string.Empty;
    public string LeftValue { get; set; } = string.Empty;
    public string RightValue { get; set; } = string.Empty;
}
