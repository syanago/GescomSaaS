namespace GescomSaas.Application.Models;

public sealed record QuotaUsageItem(
    string Label,
    int Used,
    int Limit,
    bool IsExceeded);
