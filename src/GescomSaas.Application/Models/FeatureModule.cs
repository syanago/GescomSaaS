namespace GescomSaas.Application.Models;

public sealed record FeatureModule(
    string Title,
    string Description,
    IReadOnlyList<string> Features);
