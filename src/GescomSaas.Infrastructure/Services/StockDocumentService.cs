using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Infrastructure.Services;

public class StockDocumentService(ApplicationDbContext dbContext, IInventoryService inventoryService) : IStockDocumentService
{
    public async Task<StockDocument> InitializeDraftAsync(Guid tenantId, StockDocumentType documentType, CancellationToken cancellationToken = default)
    {
        var documentDate = DateOnly.FromDateTime(DateTime.UtcNow);
        return new StockDocument
        {
            TenantId = tenantId,
            DocumentType = documentType,
            Status = StockDocumentStatus.Draft,
            DocumentDate = documentDate,
            Number = await GenerateNumberAsync(tenantId, documentType, documentDate.Year, cancellationToken)
        };
    }

    public async Task PostAsync(Guid tenantId, Guid stockDocumentId, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.StockDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == stockDocumentId && x.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            throw new InvalidOperationException("Document de stock introuvable.");
        }

        if (document.Status != StockDocumentStatus.Draft)
        {
            throw new InvalidOperationException("Seuls les documents de stock en brouillon peuvent etre valides.");
        }

        await inventoryService.PostStockDocumentAsync(tenantId, document, cancellationToken);
    }

    private async Task<string> GenerateNumberAsync(Guid tenantId, StockDocumentType documentType, int year, CancellationToken cancellationToken)
    {
        var prefix = documentType switch
        {
            StockDocumentType.Entry => $"ENT-{year}-",
            StockDocumentType.Exit => $"SOR-{year}-",
            _ => $"TRF-{year}-"
        };

        var numbers = await dbContext.StockDocuments
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.DocumentType == documentType && x.Number.StartsWith(prefix))
            .Select(x => x.Number)
            .ToListAsync(cancellationToken);

        var maxSequence = 0;
        foreach (var number in numbers)
        {
            if (number.Length <= prefix.Length)
            {
                continue;
            }

            if (int.TryParse(number[prefix.Length..], out var sequence))
            {
                maxSequence = Math.Max(maxSequence, sequence);
            }
        }

        return $"{prefix}{(maxSequence + 1):0000}";
    }
}
