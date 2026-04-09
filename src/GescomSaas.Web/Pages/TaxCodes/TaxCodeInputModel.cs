using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.Commercial;

namespace GescomSaas.Web.Pages.TaxCodes;

public class TaxCodeInputModel
{
    [Required]
    [Display(Name = "Code taxe")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Libelle")]
    public string Label { get; set; } = string.Empty;

    [Display(Name = "Taux")]
    public decimal Rate { get; set; }

    public static TaxCodeInputModel FromEntity(TaxCode entity) =>
        new()
        {
            Code = entity.Code,
            Label = entity.Label,
            Rate = entity.Rate
        };

    public void ApplyTo(TaxCode entity)
    {
        entity.Code = Code.Trim().ToUpperInvariant();
        entity.Label = Label.Trim();
        entity.Rate = Rate;
    }
}
