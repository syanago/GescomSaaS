using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Infrastructure.Services;

public class CommercialDocumentWorkflowService(
    ApplicationDbContext dbContext,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService,
    INumberingService numberingService) : ICommercialDocumentWorkflowService
{
    public async Task<CommercialDocument> InitializeDraftAsync(Guid tenantId, CommercialDocumentType documentType, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstAsync(x => x.Id == tenantId, cancellationToken);
        var rule = await numberingService.GetDocumentRuleAsync(tenantId, documentType, cancellationToken);

        return new CommercialDocument
        {
            TenantId = tenantId,
            DocumentType = documentType,
            Status = CommercialDocumentStatus.Draft,
            Number = rule.Preview,
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CurrencyCode = tenant.CurrencyCode
        };
    }

    public async Task<CommercialDocument> CreateFromSourceAsync(Guid tenantId, Guid sourceDocumentId, CommercialDocumentType targetDocumentType, CancellationToken cancellationToken = default)
    {
        var source = await dbContext.CommercialDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == sourceDocumentId && x.TenantId == tenantId, cancellationToken);

        if (source is null)
        {
            throw new InvalidOperationException("Document source introuvable.");
        }

        ValidateTransition(source.DocumentType, targetDocumentType);
        await tenantQuotaEnforcementService.EnsureCanCreateDocumentAsync(tenantId, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        var numberingRule = await numberingService.GetDocumentRuleAsync(tenantId, targetDocumentType, cancellationToken);

        if (numberingRule.Mode == NumberingMode.Manual)
        {
            throw new InvalidOperationException("La numerotation de cette piece est en mode manuel. Cree d'abord le document depuis l'ecran Nouveau pour saisir son numero.");
        }

        var target = await InitializeDraftAsync(tenantId, targetDocumentType, cancellationToken);
        target.SourceDocumentId = source.Id;
        target.PartnerId = source.PartnerId;
        target.WarehouseId = source.WarehouseId;
        target.CurrencyCode = source.CurrencyCode;
        target.Notes = $"Cree a partir de {source.Number}";
        target.DueDate = CalculateDueDate(targetDocumentType, source.DueDate);

        foreach (var line in source.Lines)
        {
            target.Lines.Add(new CommercialDocumentLine
            {
                ProductId = line.ProductId,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPriceExcludingTax = line.UnitPriceExcludingTax,
                DiscountRate = line.DiscountRate,
                TaxRate = line.TaxRate,
                LineTotalExcludingTax = line.LineTotalExcludingTax,
                LineTaxAmount = line.LineTaxAmount,
                LineTotalIncludingTax = line.LineTotalIncludingTax,
                LotNumber = line.LotNumber,
                SerialNumber = line.SerialNumber,
                ExpirationDate = line.ExpirationDate
            });
        }

        target.Status = targetDocumentType is CommercialDocumentType.DeliveryNote or CommercialDocumentType.GoodsReceipt
            ? CommercialDocumentStatus.Open
            : CommercialDocumentStatus.Draft;

        target.Number = await numberingService.ResolveDocumentNumberAsync(tenantId, targetDocumentType, target.Number, cancellationToken);
        RecalculateTotals(target);

        dbContext.CommercialDocuments.Add(target);
        await dbContext.SaveChangesAsync(cancellationToken);

        source.Status = CommercialDocumentStatus.PartiallyProcessed;
        await dbContext.SaveChangesAsync(cancellationToken);

        return target;
    }

    public void RecalculateTotals(CommercialDocument document)
    {
        foreach (var line in document.Lines)
        {
            var baseTotal = decimal.Round(line.Quantity * line.UnitPriceExcludingTax, 2);
            var discounted = decimal.Round(baseTotal * (1 - (line.DiscountRate / 100m)), 2);
            var tax = decimal.Round(discounted * (line.TaxRate / 100m), 2);

            line.LineTotalExcludingTax = discounted;
            line.LineTaxAmount = tax;
            line.LineTotalIncludingTax = discounted + tax;
        }

        document.TotalExcludingTax = document.Lines.Sum(x => x.LineTotalExcludingTax);
        document.TotalTax = document.Lines.Sum(x => x.LineTaxAmount);
        document.TotalIncludingTax = document.Lines.Sum(x => x.LineTotalIncludingTax);
    }

    public async Task SynchronizeSourceStatusAsync(Guid sourceDocumentId, CancellationToken cancellationToken = default)
    {
        var source = await dbContext.CommercialDocuments
            .Include(x => x.DerivedDocuments)
            .FirstOrDefaultAsync(x => x.Id == sourceDocumentId, cancellationToken);

        if (source is null)
        {
            return;
        }

        source.Status = source.DerivedDocuments.Count == 0
            ? CommercialDocumentStatus.Open
            : CommercialDocumentStatus.PartiallyProcessed;

        if (source.DocumentType == CommercialDocumentType.SalesQuote &&
            source.DerivedDocuments.Any(x => x.DocumentType == CommercialDocumentType.SalesOrder))
        {
            source.Status = CommercialDocumentStatus.Completed;
        }

        if (source.DocumentType == CommercialDocumentType.SalesOrder &&
            source.DerivedDocuments.Any(x => x.DocumentType == CommercialDocumentType.SalesInvoice))
        {
            source.Status = CommercialDocumentStatus.Completed;
        }

        if (source.DocumentType == CommercialDocumentType.PurchaseRequest &&
            source.DerivedDocuments.Any(x => x.DocumentType == CommercialDocumentType.PurchaseOrder))
        {
            source.Status = CommercialDocumentStatus.Completed;
        }

        if (source.DocumentType == CommercialDocumentType.PurchaseOrder &&
            source.DerivedDocuments.Any(x => x.DocumentType == CommercialDocumentType.PurchaseInvoice))
        {
            source.Status = CommercialDocumentStatus.Completed;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateTransition(CommercialDocumentType sourceType, CommercialDocumentType targetType)
    {
        var valid = sourceType switch
        {
            CommercialDocumentType.SalesQuote => targetType == CommercialDocumentType.SalesOrder,
            CommercialDocumentType.SalesOrder => targetType is CommercialDocumentType.DeliveryNote or CommercialDocumentType.SalesInvoice,
            CommercialDocumentType.DeliveryNote => targetType == CommercialDocumentType.SalesInvoice,
            CommercialDocumentType.SalesInvoice => targetType == CommercialDocumentType.SalesCreditNote,
            CommercialDocumentType.PurchaseRequest => targetType == CommercialDocumentType.PurchaseOrder,
            CommercialDocumentType.PurchaseOrder => targetType is CommercialDocumentType.GoodsReceipt or CommercialDocumentType.PurchaseInvoice,
            CommercialDocumentType.GoodsReceipt => targetType == CommercialDocumentType.PurchaseInvoice,
            CommercialDocumentType.PurchaseInvoice => targetType == CommercialDocumentType.SupplierCreditNote,
            _ => false
        };

        if (!valid)
        {
            throw new InvalidOperationException("Transformation de document non autorisee.");
        }
    }

    private static DateOnly? CalculateDueDate(CommercialDocumentType targetDocumentType, DateOnly? sourceDueDate) =>
        targetDocumentType is CommercialDocumentType.SalesInvoice or CommercialDocumentType.PurchaseInvoice
            ? sourceDueDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
            : null;
}
