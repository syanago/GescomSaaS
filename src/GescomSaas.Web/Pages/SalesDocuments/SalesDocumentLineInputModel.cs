using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.Commercial;

namespace GescomSaas.Web.Pages.SalesDocuments;

public class SalesDocumentLineInputModel
{
    [Required]
    [Display(Name = "Article")]
    public Guid? ProductId { get; set; }

    [Required]
    [Display(Name = "Designation")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Quantite")]
    public decimal Quantity { get; set; } = 1m;

    [Display(Name = "PU HT")]
    public decimal UnitPriceExcludingTax { get; set; }

    [Display(Name = "Remise %")]
    public decimal DiscountRate { get; set; }

    [Display(Name = "Taxe %")]
    public decimal TaxRate { get; set; }

    public void ApplyTo(CommercialDocumentLine entity)
    {
        entity.ProductId = ProductId;
        entity.Description = Description.Trim();
        entity.Quantity = Quantity;
        entity.UnitPriceExcludingTax = UnitPriceExcludingTax;
        entity.DiscountRate = DiscountRate;
        entity.TaxRate = TaxRate;
    }
}
