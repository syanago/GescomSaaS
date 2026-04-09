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

    [Display(Name = "Lot")]
    public string? LotNumber { get; set; }

    [Display(Name = "Mode de saisie lot")]
    public string LotEntryMode { get; set; } = "Single";

    [Display(Name = "Repartition par lots")]
    public string? LotBreakdown { get; set; }

    [Display(Name = "Numero de serie")]
    public string? SerialNumber { get; set; }

    [Display(Name = "Mode de saisie serie")]
    public string SerialEntryMode { get; set; } = "Single";

    [Display(Name = "Liste des series")]
    public string? SerialNumberList { get; set; }

    [Display(Name = "Serie debut")]
    public string? SerialRangeStart { get; set; }

    [Display(Name = "Serie fin")]
    public string? SerialRangeEnd { get; set; }

    [Display(Name = "Peremption")]
    [DataType(DataType.Date)]
    public DateOnly? ExpirationDate { get; set; }

    public void ApplyTo(CommercialDocumentLine entity)
    {
        entity.ProductId = ProductId;
        entity.Description = Description.Trim();
        entity.Quantity = Quantity;
        entity.UnitPriceExcludingTax = UnitPriceExcludingTax;
        entity.DiscountRate = DiscountRate;
        entity.TaxRate = TaxRate;
        entity.LotNumber = string.IsNullOrWhiteSpace(LotNumber) ? null : LotNumber.Trim().ToUpperInvariant();
        entity.SerialNumber = string.IsNullOrWhiteSpace(SerialNumber) ? null : SerialNumber.Trim().ToUpperInvariant();
        entity.ExpirationDate = ExpirationDate;
    }
}
