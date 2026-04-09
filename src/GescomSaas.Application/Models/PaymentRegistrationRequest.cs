using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record PaymentRegistrationRequest(
    Guid DocumentId,
    DateOnly PaymentDate,
    decimal Amount,
    PaymentMethod Method,
    string? ReferenceNumber,
    string? Notes);
