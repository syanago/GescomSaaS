using System.ComponentModel.DataAnnotations;

namespace GescomSaas.Web.Pages.Finance;

public class PaymentAllocationEntryInputModel
{
    [Required]
    [Display(Name = "Facture a affecter")]
    public Guid? CommercialDocumentId { get; set; }

    [Display(Name = "Montant a affecter")]
    public decimal Amount { get; set; }

    [Display(Name = "Notes")]
    public string? Notes { get; set; }
}
