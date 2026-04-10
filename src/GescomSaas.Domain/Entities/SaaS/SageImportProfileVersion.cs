using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.SaaS;

public class SageImportProfileVersion : TenantEntity
{
    public Guid SageImportProfileId { get; set; }
    public SageImportProfile SageImportProfile { get; set; } = null!;

    public int VersionNumber { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string ProfileJson { get; set; } = string.Empty;
}
