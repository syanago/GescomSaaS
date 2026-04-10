using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.SaaS;

public class SageImportProfile : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsArchived { get; set; }

    public ICollection<SageImportProfileVersion> Versions { get; set; } = new List<SageImportProfileVersion>();
}
