namespace GescomSaas.Domain.Exceptions;

/// <summary>Ressource demandee introuvable. Traduit en HTTP 404.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} introuvable (cle: {key}).", "NOT_FOUND", 404)
    {
        EntityName = entityName;
        Key = key;
    }

    public string EntityName { get; }
    public object Key { get; }
}
