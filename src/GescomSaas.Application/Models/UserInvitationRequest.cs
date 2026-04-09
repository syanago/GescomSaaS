namespace GescomSaas.Application.Models;

public sealed record UserInvitationRequest(
    string Email,
    string? FirstName,
    string? LastName,
    IReadOnlyList<string> Roles,
    string? Notes);
