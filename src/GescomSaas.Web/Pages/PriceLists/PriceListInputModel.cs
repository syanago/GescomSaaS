using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.Commercial;

namespace GescomSaas.Web.Pages.PriceLists;

public class PriceListInputModel
{
    [Required]
    [Display(Name = "Code tarif")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Libelle")]
    public string Label { get; set; } = string.Empty;

    [Required]
    [StringLength(3)]
    [Display(Name = "Devise")]
    public string CurrencyCode { get; set; } = "CAD";

    [Display(Name = "Tarif par defaut")]
    public bool IsDefault { get; set; }

    public static PriceListInputModel FromEntity(PriceList entity) =>
        new()
        {
            Code = entity.Code,
            Label = entity.Label,
            CurrencyCode = entity.CurrencyCode,
            IsDefault = entity.IsDefault
        };

    public void ApplyTo(PriceList entity)
    {
        entity.Code = Code.Trim().ToUpperInvariant();
        entity.Label = Label.Trim();
        entity.CurrencyCode = CurrencyCode.Trim().ToUpperInvariant();
        entity.IsDefault = IsDefault;
    }
}
