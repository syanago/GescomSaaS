namespace GescomSaas.Domain.Exceptions;

/// <summary>
/// Acces a une ressource d'un autre tenant ou tenant absent du contexte.
/// Traduit en HTTP 403 (Forbidden) - jamais en 404 pour eviter l'enumeration.
/// </summary>
public sealed class TenantAccessDeniedException : AppException
{
    public TenantAccessDeniedException(string? reason = null)
        : base(
            reason ?? "Acces refuse a cette ressource pour le tenant courant.",
            "TENANT_ACCESS_DENIED",
            403)
    {
    }
}
