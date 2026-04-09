using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.Commercial;

namespace GescomSaas.Web.Pages.PriceLists;

public class PriceListLineInputModel
{
    [Required]
    [Display(Name = "Article")]
    public Guid? ProductId { get; set; }

    [Display(Name = "Prix unitaire")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "Valide du")]
    [DataType(DataType.Date)]
    public DateOnly? ValidFrom { get; set; }

    [Display(Name = "Valide au")]
    [DataType(DataType.Date)]
    public DateOnly? ValidTo { get; set; }

    public void ApplyTo(PriceListLine entity)
    {
        entity.ProductId = ProductId ?? Guid.Empty;
        entity.UnitPrice = UnitPrice;
        entity.ValidFrom = ValidFrom;
        entity.ValidTo = ValidTo;
    }
}
