using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.Commercial;

public class PriceListLine : AuditableEntity
{
    public Guid PriceListId { get; set; }
    public PriceList? PriceList { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal UnitPrice { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
}
