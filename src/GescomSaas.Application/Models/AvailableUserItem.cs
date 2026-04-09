namespace GescomSaas.Application.Models;

public sealed record AvailableUserItem(
    string UserId,
    string Email,
    string DisplayName);
