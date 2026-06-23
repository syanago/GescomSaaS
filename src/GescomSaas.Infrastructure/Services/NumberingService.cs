using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Domain.Exceptions;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Infrastructure.Services;

public class NumberingService(ApplicationDbContext dbContext) : INumberingService
{
    public async Task<NumberingRuleSnapshot> GetDocumentRuleAsync(Guid tenantId, CommercialDocumentType documentType, CancellationToken cancellationToken = default)
    {
        var sequence = await EnsureDocumentSequenceAsync(tenantId, documentType, cancellationToken);
        return BuildSnapshot(sequence.Mode, sequence.Prefix, sequence.NumberLength, sequence.NextValue);
    }

    public async Task<string> ResolveDocumentNumberAsync(Guid tenantId, CommercialDocumentType documentType, string? requestedValue, CancellationToken cancellationToken = default)
    {
        var sequence = await EnsureDocumentSequenceAsync(tenantId, documentType, cancellationToken);
        return await ResolveSequenceValueAsync(sequence, requestedValue, cancellationToken);
    }

    public async Task<NumberingRuleSnapshot> GetReferenceRuleAsync(Guid tenantId, ReferenceNumberingScope scope, CancellationToken cancellationToken = default)
    {
        var setting = await EnsureReferenceSettingAsync(tenantId, scope, cancellationToken);
        return BuildSnapshot(setting.Mode, setting.Prefix, setting.NumberLength, setting.NextValue);
    }

    public async Task<string> ResolveReferenceCodeAsync(Guid tenantId, ReferenceNumberingScope scope, string? requestedValue, CancellationToken cancellationToken = default)
    {
        var setting = await EnsureReferenceSettingAsync(tenantId, scope, cancellationToken);
        return await ResolveReferenceValueAsync(setting, requestedValue, cancellationToken);
    }

    private async Task<string> ResolveSequenceValueAsync(DocumentSequence sequence, string? requestedValue, CancellationToken cancellationToken)
    {
        return await ResolveValueCoreAsync(sequence.Mode, sequence.Prefix, sequence.NumberLength, sequence.NextValue, requestedValue, cancellationToken, nextValue =>
        {
            sequence.NextValue = nextValue;
        });
    }

    private async Task<string> ResolveReferenceValueAsync(ReferenceNumberingSetting setting, string? requestedValue, CancellationToken cancellationToken)
    {
        return await ResolveValueCoreAsync(setting.Mode, setting.Prefix, setting.NumberLength, setting.NextValue, requestedValue, cancellationToken, nextValue =>
        {
            setting.NextValue = nextValue;
        });
    }

    private async Task<string> ResolveValueCoreAsync(NumberingMode mode, string prefix, int numberLength, int nextValue, string? requestedValue, CancellationToken cancellationToken, Action<int> updateNextValue)
    {
        var safeLength = NormalizeLength(numberLength);
        var sanitized = (requestedValue ?? string.Empty).Trim();
        var generated = $"{prefix}{nextValue.ToString($"D{safeLength}")}";

        switch (mode)
        {
            case NumberingMode.AutomaticWithPrefix:
                updateNextValue(nextValue + 1);
                await dbContext.SaveChangesAsync(cancellationToken);
                return generated;

            case NumberingMode.ManualWithPrefix:
                if (string.IsNullOrWhiteSpace(sanitized))
                {
                    sanitized = generated;
                }
                else if (!string.IsNullOrWhiteSpace(prefix) && !sanitized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    sanitized = $"{prefix}{sanitized}";
                }

                if (string.Equals(sanitized, generated, StringComparison.OrdinalIgnoreCase))
                {
                    updateNextValue(nextValue + 1);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                return sanitized;

            case NumberingMode.Manual:
                if (string.IsNullOrWhiteSpace(sanitized))
                {
                    throw new ValidationException(new Dictionary<string, string[]>
                    {
                        ["Number"] = new[] { "Saisis une valeur manuelle pour cette numerotation." },
                    });
                }

                return sanitized;

            default:
                throw new BusinessRuleException(
                    "Mode de numerotation inconnu.",
                    errorCode: "NUMBERING_MODE_UNKNOWN");
        }
    }

    private async Task<DocumentSequence> EnsureDocumentSequenceAsync(Guid tenantId, CommercialDocumentType documentType, CancellationToken cancellationToken)
    {
        var sequence = await dbContext.DocumentSequences
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.DocumentType == documentType, cancellationToken);

        if (sequence is not null)
        {
            return sequence;
        }

        sequence = new DocumentSequence
        {
            TenantId = tenantId,
            DocumentType = documentType,
            Mode = NumberingMode.AutomaticWithPrefix,
            Prefix = GetDefaultDocumentPrefix(documentType),
            NumberLength = 4,
            NextValue = 1
        };

        dbContext.DocumentSequences.Add(sequence);
        await dbContext.SaveChangesAsync(cancellationToken);
        return sequence;
    }

    private async Task<ReferenceNumberingSetting> EnsureReferenceSettingAsync(Guid tenantId, ReferenceNumberingScope scope, CancellationToken cancellationToken)
    {
        var setting = await dbContext.ReferenceNumberingSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Scope == scope, cancellationToken);

        if (setting is not null)
        {
            return setting;
        }

        setting = new ReferenceNumberingSetting
        {
            TenantId = tenantId,
            Scope = scope,
            Mode = NumberingMode.AutomaticWithPrefix,
            Prefix = GetDefaultReferencePrefix(scope),
            NumberLength = 4,
            NextValue = 1
        };

        dbContext.ReferenceNumberingSettings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private static NumberingRuleSnapshot BuildSnapshot(NumberingMode mode, string prefix, int numberLength, int nextValue)
    {
        var safeLength = NormalizeLength(numberLength);
        var preview = mode == NumberingMode.Manual
            ? string.Empty
            : $"{prefix}{nextValue.ToString($"D{safeLength}")}";

        return new NumberingRuleSnapshot(mode, prefix, safeLength, nextValue, preview);
    }

    private static int NormalizeLength(int length) => Math.Clamp(length <= 0 ? 4 : length, 1, 12);

    public static string GetDefaultDocumentPrefix(CommercialDocumentType documentType) => documentType switch
    {
        CommercialDocumentType.SalesQuote => $"DEV-{DateTime.UtcNow:yyyy}-",
        CommercialDocumentType.SalesOrder => $"CMD-{DateTime.UtcNow:yyyy}-",
        CommercialDocumentType.DeliveryNote => $"BL-{DateTime.UtcNow:yyyy}-",
        CommercialDocumentType.SalesInvoice => $"FAC-{DateTime.UtcNow:yyyy}-",
        CommercialDocumentType.SalesCreditNote => $"AVO-{DateTime.UtcNow:yyyy}-",
        CommercialDocumentType.PurchaseRequest => $"DA-{DateTime.UtcNow:yyyy}-",
        CommercialDocumentType.PurchaseOrder => $"ACH-{DateTime.UtcNow:yyyy}-",
        CommercialDocumentType.GoodsReceipt => $"REC-{DateTime.UtcNow:yyyy}-",
        CommercialDocumentType.PurchaseInvoice => $"FAF-{DateTime.UtcNow:yyyy}-",
        CommercialDocumentType.SupplierCreditNote => $"AVF-{DateTime.UtcNow:yyyy}-",
        _ => $"DOC-{DateTime.UtcNow:yyyy}-"
    };

    public static string GetDefaultReferencePrefix(ReferenceNumberingScope scope) => scope switch
    {
        ReferenceNumberingScope.Customer => "CLI-",
        ReferenceNumberingScope.Supplier => "FOU-",
        ReferenceNumberingScope.Product => "ART-",
        ReferenceNumberingScope.Warehouse => "DEP-",
        ReferenceNumberingScope.PaymentTerm => "REG-",
        ReferenceNumberingScope.TaxCode => "TAX-",
        ReferenceNumberingScope.ProductCategory => "FAM-",
        ReferenceNumberingScope.PriceList => "TAR-",
        _ => "REF-"
    };
}
