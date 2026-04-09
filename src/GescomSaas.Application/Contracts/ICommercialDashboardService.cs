using GescomSaas.Application.Models;

namespace GescomSaas.Application.Contracts;

public interface ICommercialDashboardService
{
    Task<DashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default);
}
