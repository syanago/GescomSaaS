namespace GescomSaas.Application.Models;

public sealed record InvitationAcceptanceRequest(
    string FirstName,
    string LastName,
    string? Password);
