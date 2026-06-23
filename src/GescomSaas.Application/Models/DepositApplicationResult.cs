namespace GescomSaas.Application.Models;

public sealed record DepositApplicationResult(
    decimal AppliedAmount,
    int PaymentCount,
    decimal RemainingBalance);
