using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record PaymentRegistrationRequest(
    Guid? DocumentId,
    Guid PartnerId,
    PaymentDirection Direction,
    DateOnly PaymentDate,
    decimal Amount,
    PaymentType Type,
    PaymentAllocationMode AllocationMode,
    PaymentMethod Method,
    string? ReferenceNumber,
    string? Notes);
