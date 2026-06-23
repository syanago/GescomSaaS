# Validation des entrees

GescomSaas utilise **FluentValidation** pour formaliser les regles de validation des DTO d'entree (commandes API, models de pages Razor). Le framework est cable a la hierarchie d'exceptions : un echec de validation produit automatiquement un HTTP **400** avec un dictionnaire d'erreurs par champ au format ProblemDetails.

## Pourquoi FluentValidation et pas Data Annotations

| Aspect | Data Annotations | FluentValidation |
|---|---|---|
| Lisibilite des regles complexes | Faible (attributs verbeux) | Forte (DSL fluide) |
| Tests unitaires | Difficile | First-class via `TestHelper` |
| Regles cross-fields | Hack via `IValidatableObject` | Native (`When`, `RuleFor(...).Must(...)`) |
| Reutilisation entre Razor / API / services | Non | Oui (un validator pour tout) |
| Integration DI | Limitee | Native via `AddValidatorsFromAssembly` |
| Localisation des messages | Limitee | Native |

## Architecture

```
+-------------------------------------+
|  GescomSaas.Application/Validation  |   AbstractValidator<T>
|    StockAdjustmentRequestValidator  |   ne depend QUE de Application + Domain
|    PaymentRegistrationRequest...    |   pas d'ASP.NET Core ici
|    UserInvitationRequestValidator   |
|    InvitationAcceptanceRequest...   |
+------------------+------------------+
                   |
                   | enregistres via
                   | AddValidatorsFromAssembly (Scoped)
                   v
+-------------------------------------+
|  Service / Page handler             |
|    IValidator<TRequest> injecte     |
|    await _validator.EnsureValidAsync|
+------------------+------------------+
                   |
                   | si invalide -> throw
                   v
+-------------------------------------+
|  ValidationException (Domain)       |
|    HTTP 400 + Errors{ field: [...] }|
+------------------+------------------+
                   |
                   v
+-------------------------------------+
|  GlobalExceptionMiddleware          |
|    -> ValidationProblemDetails      |
|       (RFC 7807 application/problem+json)
+-------------------------------------+
```

## Ecrire un validator

```csharp
using FluentValidation;
using GescomSaas.Application.Models;

namespace GescomSaas.Application.Validation;

public sealed class StockAdjustmentRequestValidator : AbstractValidator<StockAdjustmentRequest>
{
    public StockAdjustmentRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("L'article est obligatoire.");
        RuleFor(x => x.Quantity).GreaterThan(0m).WithMessage("La quantite doit etre strictement positive.");

        // Regle cross-field
        RuleFor(x => x.LotNumber)
            .NotEmpty().WithMessage("Numero de lot obligatoire pour cet article.")
            .When(x => x.RequiresLotTracking);
    }
}
```

Tout `AbstractValidator<T>` declare dans `GescomSaas.Application` est automatiquement decouvert et enregistre par :

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<StockAdjustmentRequestValidator>(
    lifetime: ServiceLifetime.Scoped);
```

## Utiliser un validator dans un service

```csharp
public class InventoryService(
    ApplicationDbContext db,
    IValidator<StockAdjustmentRequest> validator) : IInventoryService
{
    public async Task RegisterAdjustmentAsync(Guid tenantId, StockAdjustmentRequest request, CancellationToken ct = default)
    {
        // Une seule ligne : si le DTO est invalide, ValidationException est levee
        // et le middleware retourne un 400 ProblemDetails au client.
        await validator.EnsureValidAsync(request, ct);

        // Le reste du service ne traite QUE les regles metier (pas la forme du DTO)
        var product = await db.Products.FindAsync(request.ProductId, ct);
        // ...
    }
}
```

## Le helper `EnsureValidAsync`

Defini dans [`ValidatorExtensions.cs`](../src/GescomSaas.Application/Validation/ValidatorExtensions.cs), il convertit les `ValidationFailure` de FluentValidation en `ValidationException` du domain, qui produira la reponse HTTP attendue :

```csharp
public static async Task EnsureValidAsync<T>(
    this IValidator<T> validator,
    T instance,
    CancellationToken cancellationToken = default)
{
    var result = await validator.ValidateAsync(instance, cancellationToken);
    if (result.IsValid) return;

    var errorsByField = result.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

    throw new ValidationException(errorsByField);
}
```

## Reponse HTTP cote client

Pour une requete `POST /api/inventory/adjustments` avec `Quantity: 0` :

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json
X-Correlation-Id: 0HNL3UK0QSQN3

{
  "type": "https://httpstatuses.com/400",
  "title": "Donnees invalides",
  "status": 400,
  "detail": "Une ou plusieurs erreurs de validation sont survenues.",
  "instance": "/api/inventory/adjustments",
  "errorCode": "VALIDATION_FAILED",
  "correlationId": "0HNL3UK0QSQN3",
  "errors": {
    "Quantity": [ "La quantite doit etre strictement positive." ]
  }
}
```

## Frontiere FluentValidation vs regles metier

| Type de regle | Ou ? |
|---|---|
| **Forme du DTO** : non vide, format e-mail, longueur max, plage de valeur, type | **Validator FluentValidation** |
| **Coherence cross-fields simple** : `Password requis si NewUser=true` | **Validator** (`When(...)`) |
| **Existence en base** : "ce produit existe pour ce tenant" | Service |
| **Regle metier dependant de la DB** : "stock suffisant", "tenant a un abonnement actif" | Service (`BusinessRuleException`) |
| **Quotas** | `TenantQuotaEnforcementService` (`QuotaExceededException`) |

> **Regle d'or :** un validator ne touche jamais a la base. S'il faut charger une entite, c'est du metier - utilise `BusinessRuleException` dans le service.

## Tester un validator

Le package `FluentValidation.TestHelper` rend les tests triviaux :

```csharp
public class StockAdjustmentRequestValidatorTests
{
    private readonly StockAdjustmentRequestValidator _sut = new();

    [Fact]
    public void Quantite_NulleOuNegative_Echoue()
    {
        var dto = ValidRequest() with { Quantity = 0 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }
}
```

Voir tests existants :
- [`StockAdjustmentRequestValidatorTests`](../tests/GescomSaas.Tests/Validation/StockAdjustmentRequestValidatorTests.cs)
- [`PaymentRegistrationRequestValidatorTests`](../tests/GescomSaas.Tests/Validation/PaymentRegistrationRequestValidatorTests.cs)
- [`UserInvitationRequestValidatorTests`](../tests/GescomSaas.Tests/Validation/UserInvitationRequestValidatorTests.cs)
- [`ValidatorExtensionsTests`](../tests/GescomSaas.Tests/Validation/ValidatorExtensionsTests.cs) (le pont vers `ValidationException`)

## DTOs avec validator existant

| DTO | Validator | Tests |
|---|---|---|
| `StockAdjustmentRequest` | `StockAdjustmentRequestValidator` | 8 tests |
| `PaymentRegistrationRequest` | `PaymentRegistrationRequestValidator` | 6 tests |
| `UserInvitationRequest` | `UserInvitationRequestValidator` | 5 tests |
| `InvitationAcceptanceRequest` | `InvitationAcceptanceRequestValidator` | (pending) |

## Ajouter un nouveau validator

1. Creer `GescomSaas.Application/Validation/MonDtoValidator.cs` heritant de `AbstractValidator<MonDto>`.
2. **Aucun wiring DI a faire** : `AddValidatorsFromAssemblyContaining<...>` dans `Program.cs` decouvre tous les validators de l'assembly automatiquement.
3. Injecter `IValidator<MonDto>` dans le service / page handler.
4. Appeler `await validator.EnsureValidAsync(request, ct)` au plus tot dans le handler.
5. Ecrire les tests dans `tests/GescomSaas.Tests/Validation/`.

## References

- [FluentValidation docs](https://docs.fluentvalidation.net/)
- [Codes d'erreur stables](./ApiErrorCodes.md) - notamment `VALIDATION_FAILED`
- Pont vers les exceptions : [`ValidatorExtensions.cs`](../src/GescomSaas.Application/Validation/ValidatorExtensions.cs)
