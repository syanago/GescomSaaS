using System.ComponentModel.DataAnnotations;

namespace GescomSaas.Web.Pages;

public class AssistedPartnerEntryInputModel
{
    [Display(Name = "Recherche tiers")]
    public string? Lookup { get; set; }

    [Display(Name = "Creer le tiers s'il n'existe pas")]
    public bool CreateIfMissing { get; set; }

    [Display(Name = "Code du nouveau tiers")]
    public string? NewCode { get; set; }

    [Display(Name = "Nom du nouveau tiers")]
    public string? NewName { get; set; }

    [Display(Name = "E-mail du tiers")]
    public string? Email { get; set; }

    [Display(Name = "Telephone du tiers")]
    public string? PhoneNumber { get; set; }
}
