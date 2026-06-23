namespace GescomSaas.Domain.Exceptions;

/// <summary>
/// Regle metier violee : transition de document impossible, etat incompatible, etc.
/// Traduit en HTTP 422 (Unprocessable Entity).
/// </summary>
public sealed class BusinessRuleException : AppException
{
    public BusinessRuleException(string message, string errorCode = "BUSINESS_RULE_VIOLATION")
        : base(message, errorCode, 422)
    {
    }
}
