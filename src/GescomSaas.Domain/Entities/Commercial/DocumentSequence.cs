using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.Commercial;

public class DocumentSequence : TenantEntity
{
    public CommercialDocumentType DocumentType { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public int NextValue { get; set; } = 1;
}
