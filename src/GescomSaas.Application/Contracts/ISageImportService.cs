using GescomSaas.Application.Models;

namespace GescomSaas.Application.Contracts;

public interface ISageImportService
{
    Task<SageImportExecutionReport> ExecuteAsync(SageImportExecutionRequest request, CancellationToken cancellationToken = default);
}
