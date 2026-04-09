using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.Finance;

public class PaymentEntryInputModel
{
    [Required]
    [Display(Name = "Facture")]
    public Guid? DocumentId { get; set; }

    [Display(Name = "Date de reglement")]
    [DataType(DataType.Date)]
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Display(Name = "Montant")]
    public decimal Amount { get; set; }

    [Display(Name = "Mode")]
    public PaymentMethod Method { get; set; } = PaymentMethod.BankTransfer;

    [Display(Name = "Reference")]
    public string? ReferenceNumber { get; set; }

    [Display(Name = "Notes")]
    public string? Notes { get; set; }
}
