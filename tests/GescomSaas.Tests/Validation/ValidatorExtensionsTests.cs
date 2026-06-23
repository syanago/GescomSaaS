using FluentValidation;
using GescomSaas.Application.Validation;
// Alias pour eviter le conflit avec FluentValidation.ValidationException
using AppValidationException = GescomSaas.Domain.Exceptions.ValidationException;

namespace GescomSaas.Tests.Validation;

/// <summary>
/// Verifie que le pont FluentValidation -> ValidationException convertit
/// correctement les erreurs et qu'elles atteignent le client en HTTP 400
/// avec le dictionnaire d'erreurs par champ.
/// </summary>
public class ValidatorExtensionsTests
{
    private sealed record DummyDto(string? Name, int Age);

    private sealed class DummyValidator : AbstractValidator<DummyDto>
    {
        public DummyValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Le nom est requis.");
            RuleFor(x => x.Age).GreaterThan(0).WithMessage("L'age doit etre positif.");
        }
    }

    [Fact]
    public async Task EnsureValid_QuandValide_NeJettePasException()
    {
        var validator = new DummyValidator();
        var dto = new DummyDto("Alice", 30);

        var act = async () => await validator.EnsureValidAsync(dto);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureValid_QuandUneErreur_LeveValidationExceptionAvec400()
    {
        var validator = new DummyValidator();
        var dto = new DummyDto(null, 30);

        var act = async () => await validator.EnsureValidAsync(dto);

        var ex = await act.Should().ThrowAsync<AppValidationException>();
        ex.Which.HttpStatusCode.Should().Be(400);
        ex.Which.ErrorCode.Should().Be("VALIDATION_FAILED");
        ex.Which.Errors.Should().ContainKey("Name");
        ex.Which.Errors["Name"].Single().Should().Be("Le nom est requis.");
    }

    [Fact]
    public async Task EnsureValid_QuandPlusieursErreurs_LesRegroupeParChamp()
    {
        var validator = new DummyValidator();
        var dto = new DummyDto(null, -5);

        var act = async () => await validator.EnsureValidAsync(dto);

        var ex = await act.Should().ThrowAsync<AppValidationException>();
        ex.Which.Errors.Should().HaveCount(2);
        ex.Which.Errors.Should().ContainKey("Name");
        ex.Which.Errors.Should().ContainKey("Age");
    }

    [Fact]
    public async Task EnsureValid_DeduplicLesMessagesIdentiquesParChamp()
    {
        // Verifie qu'on ne pollue pas le dictionnaire avec des doublons quand
        // plusieurs regles produisent le meme message d'erreur.
        var validator = new InlineValidator<DummyDto>();
        validator.RuleFor(x => x.Name).NotEmpty().WithMessage("Obligatoire.");
        validator.RuleFor(x => x.Name).Must(_ => false).WithMessage("Obligatoire.");

        var act = async () => await validator.EnsureValidAsync(new DummyDto(null, 1));
        var ex = await act.Should().ThrowAsync<AppValidationException>();

        ex.Which.Errors["Name"].Should().HaveCount(1);
    }
}
