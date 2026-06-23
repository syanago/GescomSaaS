using GescomSaas.Application.Models;

namespace GescomSaas.Application.Contracts;

public interface IRuntimeInitializationStateService
{
    Task<RuntimeInitializationState> GetStateAsync(CancellationToken cancellationToken = default);
}
