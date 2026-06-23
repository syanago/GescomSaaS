namespace GescomSaas.Web.Pages;

public sealed record PartnerAssistResult(
    Guid? PartnerId,
    string? LookupValue,
    bool Created,
    string? ErrorMessage);
