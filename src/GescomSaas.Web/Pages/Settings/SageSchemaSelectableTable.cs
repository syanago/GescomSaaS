namespace GescomSaas.Web.Pages.Settings;

public sealed class SageSchemaSelectableTable
{
    public string TableName { get; set; } = string.Empty;
    public IReadOnlyList<string> Columns { get; set; } = [];
}
