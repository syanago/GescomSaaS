namespace GescomSaas.Application.Contracts;

public interface ICurrentTenantAccessor
{
    Guid? GetTenantId();
}
