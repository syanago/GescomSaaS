namespace GescomSaas.Web.Pages;

public sealed record PartnerLookupOption(
    Guid Id,
    string Code,
    string Name,
    string DisplayValue,
    string Caption);
