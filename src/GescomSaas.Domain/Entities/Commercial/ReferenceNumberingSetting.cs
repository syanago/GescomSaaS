using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.Commercial;

public class ReferenceNumberingSetting : TenantEntity
{
    public ReferenceNumberingScope Scope { get; set; }
    public NumberingMode Mode { get; set; } = NumberingMode.AutomaticWithPrefix;
    public string Prefix { get; set; } = string.Empty;
    public int NumberLength { get; set; } = 4;
    public int NextValue { get; set; } = 1;
}
