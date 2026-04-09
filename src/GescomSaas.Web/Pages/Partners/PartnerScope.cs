using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.Partners;

public static class PartnerScope
{
    public const string Customers = "customers";
    public const string Suppliers = "suppliers";

    public static string Normalize(string? scope) =>
        string.Equals(scope, Suppliers, StringComparison.OrdinalIgnoreCase) ? Suppliers : Customers;

    public static string Title(string scope) =>
        Normalize(scope) == Suppliers ? "Fournisseurs" : "Clients";

    public static string CreateTitle(string scope) =>
        Normalize(scope) == Suppliers ? "Nouveau fournisseur" : "Nouveau client";

    public static bool MatchesScope(BusinessPartnerType partnerType, string scope)
    {
        scope = Normalize(scope);

        return scope == Suppliers
            ? partnerType is BusinessPartnerType.Supplier or BusinessPartnerType.Both
            : partnerType is BusinessPartnerType.Customer or BusinessPartnerType.Both or BusinessPartnerType.Prospect;
    }
}
