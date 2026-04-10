namespace GescomSaas.Domain.Enums;

public enum SageMissingReferencePolicy
{
    CreateMissing = 0,
    SkipDependentRecords = 1,
    BlockTransfer = 2
}
