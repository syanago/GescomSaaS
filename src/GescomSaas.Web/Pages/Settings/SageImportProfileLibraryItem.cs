namespace GescomSaas.Web.Pages.Settings;

public class SageImportProfileLibraryItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public IReadOnlyList<SageImportProfileVersionItem> Versions { get; set; } = [];
}

public class SageImportProfileVersionItem
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedOnUtc { get; set; }
}
