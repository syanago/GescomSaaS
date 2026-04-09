using System.Security.Claims;
using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Api;

public static class ApiMappingHelpers
{
    public static ApiPartnerResponse MapPartner(BusinessPartner partner) =>
        new(
            partner.Id,
            partner.Code,
            partner.Name,
            partner.PartnerType,
            partner.Email,
            partner.PhoneNumber,
            partner.VatNumber,
            partner.CreditLimit,
            partner.IsActive,
            partner.PaymentTermId,
            partner.PaymentTerm?.Label,
            MapAddress(partner.BillingAddress),
            MapAddress(partner.ShippingAddress));

    public static ApiProductResponse MapProduct(Product product) =>
        new(
            product.Id,
            product.Sku,
            product.Label,
            product.Description,
            product.ProductType,
            product.UnitOfMeasure,
            product.TrackStock,
            product.IsActive,
            product.ProductCategoryId,
            product.ProductCategory?.Label,
            product.TaxCodeId,
            product.TaxCode?.Label,
            product.PurchasePrice,
            product.SalesPrice);

    public static ApiAddressResponse MapAddress(Address address) =>
        new(
            address.Recipient,
            address.StreetLine1,
            address.StreetLine2,
            address.PostalCode,
            address.City,
            address.State,
            address.Country);

    public static void ApplyPartnerRequest(BusinessPartner partner, ApiPartnerRequest request)
    {
        partner.Code = request.Code.Trim().ToUpperInvariant();
        partner.Name = request.Name.Trim();
        partner.PartnerType = request.PartnerType;
        partner.Email = NormalizeOptional(request.Email);
        partner.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        partner.VatNumber = NormalizeOptional(request.VatNumber);
        partner.CreditLimit = request.CreditLimit;
        partner.IsActive = request.IsActive;
        partner.PaymentTermId = request.PaymentTermId;
        partner.BillingAddress = request.BillingAddress.ToEntity();
        partner.ShippingAddress = request.ShippingAddress.ToEntity();
    }

    public static void ApplyProductRequest(Product product, ApiProductRequest request)
    {
        product.Sku = request.Sku.Trim().ToUpperInvariant();
        product.Label = request.Label.Trim();
        product.Description = NormalizeOptional(request.Description);
        product.ProductType = request.ProductType;
        product.UnitOfMeasure = request.UnitOfMeasure.Trim().ToUpperInvariant();
        product.TrackStock = request.TrackStock;
        product.IsActive = request.IsActive;
        product.ProductCategoryId = request.ProductCategoryId;
        product.TaxCodeId = request.TaxCodeId;
        product.PurchasePrice = request.PurchasePrice;
        product.SalesPrice = request.SalesPrice;
    }

    public static void ApplyDocumentHeader(
        CommercialDocument document,
        Guid partnerId,
        Guid? warehouseId,
        DateOnly documentDate,
        DateOnly? dueDate,
        string? currencyCode,
        CommercialDocumentStatus status,
        string? notes)
    {
        document.PartnerId = partnerId;
        document.WarehouseId = warehouseId;
        document.DocumentDate = documentDate;
        document.DueDate = dueDate;
        document.CurrencyCode = string.IsNullOrWhiteSpace(currencyCode)
            ? document.CurrencyCode
            : currencyCode.Trim().ToUpperInvariant();
        document.Status = status;
        document.Notes = NormalizeOptional(notes);
    }

    public static void ApplyDocumentLines(CommercialDocument document, IReadOnlyList<ApiDocumentLineRequest> lines)
    {
        foreach (var line in lines)
        {
            document.Lines.Add(new CommercialDocumentLine
            {
                ProductId = line.ProductId,
                Description = line.Description.Trim(),
                Quantity = line.Quantity,
                UnitPriceExcludingTax = line.UnitPriceExcludingTax,
                DiscountRate = line.DiscountRate,
                TaxRate = line.TaxRate
            });
        }
    }

    public static async Task<ApiDocumentResponse?> LoadDocumentResponseAsync(
        ApplicationDbContext dbContext,
        Guid tenantId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        return await dbContext.CommercialDocuments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.Warehouse)
            .Include(x => x.SourceDocument)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Where(x => x.Id == documentId && x.TenantId == tenantId)
            .Select(x => new ApiDocumentResponse(
                x.Id,
                x.Number,
                x.DocumentType,
                x.Status,
                x.DocumentDate,
                x.DueDate,
                x.CurrencyCode,
                x.PartnerId,
                x.Partner != null ? x.Partner.Name : "-",
                x.WarehouseId,
                x.Warehouse != null ? x.Warehouse.Label : null,
                x.SourceDocumentId,
                x.SourceDocument != null ? x.SourceDocument.Number : null,
                x.Notes,
                x.TotalExcludingTax,
                x.TotalTax,
                x.TotalIncludingTax,
                x.Lines
                    .OrderBy(line => line.CreatedOnUtc)
                    .Select(line => new ApiDocumentLineResponse(
                        line.Id,
                        line.ProductId,
                        line.Product != null ? line.Product.Sku : null,
                        line.Description,
                        line.Quantity,
                        line.UnitPriceExcludingTax,
                        line.DiscountRate,
                        line.TaxRate,
                        line.LineTotalExcludingTax,
                        line.LineTaxAmount,
                        line.LineTotalIncludingTax))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<ResolvedTenantResult> ResolveTenantAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(principal);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new ResolvedTenantResult(null, null, Results.Unauthorized());
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new ResolvedTenantResult(null, null, Results.Unauthorized());
        }

        var tenantId = currentTenantAccessor.GetTenantId() ?? user.TenantId;
        if (!tenantId.HasValue)
        {
            return new ResolvedTenantResult(null, user, Results.Problem("Aucun tenant n'est associe a ce compte API.", statusCode: StatusCodes.Status403Forbidden));
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId.Value, cancellationToken);

        if (tenant is null)
        {
            return new ResolvedTenantResult(null, user, Results.Problem("Le tenant associe au compte est introuvable.", statusCode: StatusCodes.Status403Forbidden));
        }

        return new ResolvedTenantResult(tenant, user, null);
    }

    public static async Task CreateStockIssuesAsync(ApplicationDbContext dbContext, CommercialDocument deliveryNote, Guid tenantId, CancellationToken cancellationToken)
    {
        if (!deliveryNote.WarehouseId.HasValue)
        {
            return;
        }

        foreach (var line in deliveryNote.Lines.Where(x => x.ProductId.HasValue))
        {
            var product = await dbContext.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == line.ProductId && x.TenantId == tenantId, cancellationToken);

            if (product is null || !product.TrackStock)
            {
                continue;
            }

            dbContext.StockMovements.Add(new StockMovement
            {
                TenantId = tenantId,
                ProductId = product.Id,
                WarehouseId = deliveryNote.WarehouseId.Value,
                MovementDate = deliveryNote.DocumentDate,
                MovementType = StockMovementType.Issue,
                Quantity = -line.Quantity,
                UnitCost = product.PurchasePrice,
                ReferenceNumber = deliveryNote.Number
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static async Task CreateStockReceiptsAsync(ApplicationDbContext dbContext, CommercialDocument goodsReceipt, Guid tenantId, CancellationToken cancellationToken)
    {
        if (!goodsReceipt.WarehouseId.HasValue)
        {
            return;
        }

        foreach (var line in goodsReceipt.Lines.Where(x => x.ProductId.HasValue))
        {
            var product = await dbContext.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == line.ProductId && x.TenantId == tenantId, cancellationToken);

            if (product is null || !product.TrackStock)
            {
                continue;
            }

            dbContext.StockMovements.Add(new StockMovement
            {
                TenantId = tenantId,
                ProductId = product.Id,
                WarehouseId = goodsReceipt.WarehouseId.Value,
                MovementDate = goodsReceipt.DocumentDate,
                MovementType = StockMovementType.Receipt,
                Quantity = line.Quantity,
                UnitCost = line.UnitPriceExcludingTax,
                ReferenceNumber = goodsReceipt.Number
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static string NormalizePartnerScope(string? scope) => scope?.Trim().ToLowerInvariant() switch
    {
        "customers" => "customers",
        "suppliers" => "suppliers",
        _ => "all"
    };

    public static string NormalizeFinanceScope(string? scope) => scope?.Trim().ToLowerInvariant() switch
    {
        "payables" => "payables",
        _ => "receivables"
    };

    public static string NormalizeDocumentFamily(string? family) => family?.Trim().ToLowerInvariant() switch
    {
        "sales" => "sales",
        "purchases" => "purchases",
        _ => "all"
    };

    public static bool IsSalesDocument(CommercialDocumentType type) => type is
        CommercialDocumentType.SalesQuote or
        CommercialDocumentType.SalesOrder or
        CommercialDocumentType.DeliveryNote or
        CommercialDocumentType.SalesInvoice or
        CommercialDocumentType.SalesCreditNote;

    public static bool IsPurchaseDocument(CommercialDocumentType type) => type is
        CommercialDocumentType.PurchaseRequest or
        CommercialDocumentType.PurchaseOrder or
        CommercialDocumentType.GoodsReceipt or
        CommercialDocumentType.PurchaseInvoice or
        CommercialDocumentType.SupplierCreditNote;

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record ResolvedTenantResult(Domain.Entities.SaaS.Tenant? Tenant, ApplicationUser? User, IResult? Result);
}
