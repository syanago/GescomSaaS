namespace GescomSaas.Domain.Exceptions;

/// <summary>
/// Donnees d'entree invalides (champ manquant, format incorrect, etc.).
/// Traduit en HTTP 400 avec un dictionnaire d'erreurs par champ.
/// </summary>
public sealed class ValidationException : AppException
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Une ou plusieurs erreurs de validation sont survenues.", "VALIDATION_FAILED", 400)
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
