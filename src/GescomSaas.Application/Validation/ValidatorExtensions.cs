using FluentValidation;
// Alias pour eviter le conflit avec FluentValidation.ValidationException
using AppValidationException = GescomSaas.Domain.Exceptions.ValidationException;

namespace GescomSaas.Application.Validation;

/// <summary>
/// Pont entre FluentValidation et la hierarchie d'exceptions metier.
///
/// Convertit les <see cref="ValidationResult"/> de FluentValidation en
/// <see cref="GescomSaas.Domain.Exceptions.ValidationException"/> (HTTP 400 +
/// dictionnaire d'erreurs par champ), automatiquement transforme en
/// ProblemDetails par le middleware global.
///
/// Usage typique dans un service ou un page handler :
///   await _validator.EnsureValidAsync(request, ct);
/// </summary>
public static class ValidatorExtensions
{
    public static async Task EnsureValidAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
        {
            return;
        }

        var errorsByField = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        throw new AppValidationException(errorsByField);
    }
}
