namespace GescomSaas.Domain.Exceptions;

/// <summary>
/// Base de toutes les exceptions metier de GescomSaas.
/// Le middleware global d'exception sait les traduire en ProblemDetails (RFC 7807).
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message, string errorCode, int httpStatusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        HttpStatusCode = httpStatusCode;
    }

    /// <summary>Code court stable pour identification cote client (ex: "QUOTA_EXCEEDED").</summary>
    public string ErrorCode { get; }

    /// <summary>Statut HTTP recommande quand l'exception traverse une frontiere HTTP.</summary>
    public int HttpStatusCode { get; }
}
