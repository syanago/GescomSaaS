using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.Commercial;

namespace GescomSaas.Web.Pages.PaymentTerms;

public class PaymentTermInputModel
{
    [Required]
    [Display(Name = "Code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Libelle")]
    public string Label { get; set; } = string.Empty;

    [Display(Name = "Echeance en jours")]
    public int DueInDays { get; set; }

    public static PaymentTermInputModel FromEntity(PaymentTerm entity) =>
        new()
        {
            Code = entity.Code,
            Label = entity.Label,
            DueInDays = entity.DueInDays
        };

    public void ApplyTo(PaymentTerm entity)
    {
        entity.Code = Code.Trim().ToUpperInvariant();
        entity.Label = Label.Trim();
        entity.DueInDays = DueInDays;
    }
}
