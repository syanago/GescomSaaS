namespace GescomSaas.Domain.Exceptions;

/// <summary>
/// Quota du plan SaaS depasse (utilisateurs, partenaires, articles, documents...).
/// Traduit en HTTP 402 (Payment Required) pour signaler une montee de plan.
/// </summary>
public sealed class QuotaExceededException : AppException
{
    public QuotaExceededException(string quotaName, int limit, int current)
        : base(
            $"Quota '{quotaName}' atteint ({current}/{limit}). Passez a un plan superieur pour continuer.",
            "QUOTA_EXCEEDED",
            402)
    {
        QuotaName = quotaName;
        Limit = limit;
        Current = current;
    }

    public string QuotaName { get; }
    public int Limit { get; }
    public int Current { get; }
}
