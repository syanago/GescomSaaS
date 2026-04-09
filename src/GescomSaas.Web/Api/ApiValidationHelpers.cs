using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Api;

public static class ApiValidationHelpers
{
    public static async Task<IResult?> ValidatePartnerRequestAsync(
        Guid tenantId,
        ApiPartnerRequest request,
        ApplicationDbContext dbContext,
        Guid? currentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["code"] = ["Le code du tiers est obligatoire."],
                ["name"] = ["Le nom du tiers est obligatoire."]
            });
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var duplicateCode = await dbContext.BusinessPartners
            .AnyAsync(x => x.TenantId == tenantId && x.Code == normalizedCode && x.Id != currentId, cancellationToken);

        if (duplicateCode)
        {
            return Results.Conflict(new { message = "Un tiers avec ce code existe deja." });
        }

        if (request.PaymentTermId.HasValue)
        {
            var paymentTermExists = await dbContext.PaymentTerms
                .AnyAsync(x => x.Id == request.PaymentTermId.Value && x.TenantId == tenantId, cancellationToken);

            if (!paymentTermExists)
            {
                return Results.BadRequest(new { message = "La condition de reglement selectionnee est introuvable." });
            }
        }

        return null;
    }

    public static async Task<IResult?> ValidateProductRequestAsync(
        Guid tenantId,
        ApiProductRequest request,
        ApplicationDbContext dbContext,
        Guid? currentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.Label) || string.IsNullOrWhiteSpace(request.UnitOfMeasure))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["sku"] = ["Le code article est obligatoire."],
                ["label"] = ["Le libelle article est obligatoire."],
                ["unitOfMeasure"] = ["L'unite de mesure est obligatoire."]
            });
        }

        var normalizedSku = request.Sku.Trim().ToUpperInvariant();
        var duplicateSku = await dbContext.Products
            .AnyAsync(x => x.TenantId == tenantId && x.Sku == normalizedSku && x.Id != currentId, cancellationToken);

        if (duplicateSku)
        {
            return Results.Conflict(new { message = "Un article avec ce code existe deja." });
        }

        if (request.ProductCategoryId.HasValue)
        {
            var categoryExists = await dbContext.ProductCategories
                .AnyAsync(x => x.Id == request.ProductCategoryId.Value && x.TenantId == tenantId, cancellationToken);

            if (!categoryExists)
            {
                return Results.BadRequest(new { message = "La famille d'article selectionnee est introuvable." });
            }
        }

        if (request.TaxCodeId.HasValue)
        {
            var taxCodeExists = await dbContext.TaxCodes
                .AnyAsync(x => x.Id == request.TaxCodeId.Value && x.TenantId == tenantId, cancellationToken);

            if (!taxCodeExists)
            {
                return Results.BadRequest(new { message = "Le code taxe selectionne est introuvable." });
            }
        }

        return null;
    }

    public static async Task<IResult?> ValidateDocumentCreateRequestAsync(
        Guid tenantId,
        ApiDocumentCreateRequest request,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await ValidateDocumentRequestCoreAsync(
            tenantId,
            request.DocumentType,
            request.PartnerId,
            request.WarehouseId,
            request.Lines,
            dbContext,
            cancellationToken);
    }

    public static async Task<IResult?> ValidateDocumentUpdateRequestAsync(
        Guid tenantId,
        CommercialDocumentType documentType,
        ApiDocumentUpdateRequest request,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await ValidateDocumentRequestCoreAsync(
            tenantId,
            documentType,
            request.PartnerId,
            request.WarehouseId,
            request.Lines,
            dbContext,
            cancellationToken);
    }

    private static async Task<IResult?> ValidateDocumentRequestCoreAsync(
        Guid tenantId,
        CommercialDocumentType documentType,
        Guid partnerId,
        Guid? warehouseId,
        IReadOnlyList<ApiDocumentLineRequest> lines,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (lines.Any(x => string.IsNullOrWhiteSpace(x.Description) || x.Quantity <= 0m))
        {
            return Results.BadRequest(new { message = "Chaque ligne doit avoir une designation et une quantite strictement positive." });
        }

        var partner = await dbContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == partnerId && x.TenantId == tenantId && x.IsActive, cancellationToken);

        if (partner is null)
        {
            return Results.BadRequest(new { message = "Le tiers selectionne est introuvable ou inactif." });
        }

        if (ApiMappingHelpers.IsSalesDocument(documentType) &&
            partner.PartnerType is not (BusinessPartnerType.Customer or BusinessPartnerType.Both or BusinessPartnerType.Prospect))
        {
            return Results.BadRequest(new { message = "Le tiers doit etre un client, un prospect ou un tiers mixte pour une piece de vente." });
        }

        if (ApiMappingHelpers.IsPurchaseDocument(documentType) &&
            partner.PartnerType is not (BusinessPartnerType.Supplier or BusinessPartnerType.Both))
        {
            return Results.BadRequest(new { message = "Le tiers doit etre un fournisseur ou un tiers mixte pour une piece d'achat." });
        }

        if (warehouseId.HasValue)
        {
            var warehouseExists = await dbContext.Warehouses
                .AnyAsync(x => x.Id == warehouseId.Value && x.TenantId == tenantId, cancellationToken);

            if (!warehouseExists)
            {
                return Results.BadRequest(new { message = "Le depot selectionne est introuvable." });
            }
        }

        var productIds = lines.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value).Distinct().ToArray();
        if (productIds.Length > 0)
        {
            var validProductIds = await dbContext.Products
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && productIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (validProductIds.Count != productIds.Length)
            {
                return Results.BadRequest(new { message = "Au moins un article reference dans les lignes est introuvable." });
            }
        }

        return null;
    }
}
