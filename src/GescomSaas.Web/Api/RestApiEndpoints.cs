using System.Security.Claims;
using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Api;

public static class RestApiEndpoints
{
    public static IEndpointRouteBuilder MapGescomApi(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("API Authentication");
        auth.MapPost("/logout", LogoutAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = $"{IdentityConstants.ApplicationScheme},{IdentityConstants.BearerScheme}"
            });

        var api = app.MapGroup("/api/v1")
            .WithTags("Gescom REST API")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = $"{IdentityConstants.ApplicationScheme},{IdentityConstants.BearerScheme}"
            });

        api.MapGet("/context", GetContextAsync);
        api.MapGet("/dashboard", GetDashboardAsync);

        var partners = api.MapGroup("/partners");
        partners.MapGet("/", GetPartnersAsync);
        partners.MapGet("/{id:guid}", GetPartnerAsync);
        partners.MapPost("/", CreatePartnerAsync);
        partners.MapPut("/{id:guid}", UpdatePartnerAsync);
        partners.MapDelete("/{id:guid}", DeletePartnerAsync);

        var products = api.MapGroup("/products");
        products.MapGet("/", GetProductsAsync);
        products.MapGet("/{id:guid}", GetProductAsync);
        products.MapPost("/", CreateProductAsync);
        products.MapPut("/{id:guid}", UpdateProductAsync);
        products.MapDelete("/{id:guid}", DeleteProductAsync);

        api.MapGet("/warehouses", GetWarehousesAsync);

        var documents = api.MapGroup("/documents");
        documents.MapGet("/", GetDocumentsAsync);
        documents.MapGet("/{id:guid}", GetDocumentAsync);
        documents.MapPost("/", CreateDocumentAsync);
        documents.MapPut("/{id:guid}", UpdateDocumentAsync);
        documents.MapPost("/{id:guid}/transform", TransformDocumentAsync);
        documents.MapDelete("/{id:guid}", DeleteDocumentAsync);

        var finance = api.MapGroup("/finance");
        finance.MapGet("/open-items", GetOpenItemsAsync);
        finance.MapGet("/payments", GetPaymentsAsync);
        finance.MapPost("/payments", RegisterPaymentAsync);

        var inventory = api.MapGroup("/inventory");
        inventory.MapGet("/dashboard", GetInventoryDashboardAsync);
        inventory.MapGet("/movements", GetInventoryMovementsAsync);
        inventory.MapPost("/adjustments", RegisterInventoryAdjustmentAsync);

        return app;
    }
    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> GetContextAsync(
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ICommercialDashboardService dashboardService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var resolvedTenant = tenantResult.Tenant!;
        var applicationUser = tenantResult.User!;
        var dashboard = await dashboardService.GetDashboardAsync(cancellationToken);
        var quotas = dashboard.Quotas
            .Select(x => new ApiQuotaResponse(
                x.Label,
                x.Used,
                x.Limit,
                x.Limit - x.Used,
                x.IsExceeded))
            .ToArray();

        return Results.Ok(new ApiContextResponse(
            applicationUser.Id,
            applicationUser.Email ?? applicationUser.UserName ?? string.Empty,
            applicationUser.DisplayName,
            resolvedTenant.Id,
            resolvedTenant.CompanyName,
            resolvedTenant.CurrencyCode,
            resolvedTenant.AllowNegativeStock,
            resolvedTenant.DefaultStockValuationMethod,
            user.Claims
                .Where(x => x.Type == ClaimTypes.Role)
                .Select(x => x.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToArray(),
            quotas,
            quotas.Count(x => x.IsExceeded)));
    }

    private static async Task<IResult> GetDashboardAsync(
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ICommercialDashboardService dashboardService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        return tenantResult.Result ?? Results.Ok(await dashboardService.GetDashboardAsync(cancellationToken));
    }

    private static async Task<IResult> GetPartnersAsync(
        string? scope,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var normalizedScope = ApiMappingHelpers.NormalizePartnerScope(scope);
        var query = dbContext.BusinessPartners
            .AsNoTracking()
            .Include(x => x.PaymentTerm)
            .Where(x => x.TenantId == tenantResult.Tenant!.Id);

        query = normalizedScope switch
        {
            "customers" => query.Where(x => x.PartnerType == BusinessPartnerType.Customer || x.PartnerType == BusinessPartnerType.Both || x.PartnerType == BusinessPartnerType.Prospect),
            "suppliers" => query.Where(x => x.PartnerType == BusinessPartnerType.Supplier || x.PartnerType == BusinessPartnerType.Both),
            _ => query
        };

        var partners = await query
            .OrderBy(x => x.Code)
            .Select(x => ApiMappingHelpers.MapPartner(x))
            .ToListAsync(cancellationToken);

        return Results.Ok(partners);
    }

    private static async Task<IResult> GetPartnerAsync(
        Guid id,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var partner = await dbContext.BusinessPartners
            .AsNoTracking()
            .Include(x => x.PaymentTerm)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantResult.Tenant!.Id, cancellationToken);

        return partner is null ? Results.NotFound() : Results.Ok(ApiMappingHelpers.MapPartner(partner));
    }

    private static async Task<IResult> CreatePartnerAsync(
        ApiPartnerRequest request,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ITenantQuotaEnforcementService tenantQuotaEnforcementService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var tenantId = tenantResult.Tenant!.Id;
        var validation = await ApiValidationHelpers.ValidatePartnerRequestAsync(tenantId, request, dbContext, null, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            await tenantQuotaEnforcementService.EnsureCanCreatePartnerAsync(tenantId, request.PartnerType, request.IsActive, cancellationToken);

            var partner = new BusinessPartner { TenantId = tenantId };
            ApiMappingHelpers.ApplyPartnerRequest(partner, request);
            dbContext.BusinessPartners.Add(partner);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/v1/partners/{partner.Id}", ApiMappingHelpers.MapPartner(partner));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> UpdatePartnerAsync(
        Guid id,
        ApiPartnerRequest request,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ITenantQuotaEnforcementService tenantQuotaEnforcementService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var tenantId = tenantResult.Tenant!.Id;
        var partner = await dbContext.BusinessPartners
            .Include(x => x.PaymentTerm)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);

        if (partner is null)
        {
            return Results.NotFound();
        }

        var validation = await ApiValidationHelpers.ValidatePartnerRequestAsync(tenantId, request, dbContext, id, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            await tenantQuotaEnforcementService.EnsureCanUpdatePartnerAsync(tenantId, id, request.PartnerType, request.IsActive, cancellationToken);

            ApiMappingHelpers.ApplyPartnerRequest(partner, request);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(ApiMappingHelpers.MapPartner(partner));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> DeletePartnerAsync(
        Guid id,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var tenantId = tenantResult.Tenant!.Id;
        var partner = await dbContext.BusinessPartners
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);

        if (partner is null)
        {
            return Results.NotFound();
        }

        var isUsed = await dbContext.CommercialDocuments.AnyAsync(x => x.PartnerId == id && x.TenantId == tenantId, cancellationToken)
            || await dbContext.Payments.AnyAsync(x => x.PartnerId == id && x.TenantId == tenantId, cancellationToken);

        if (isUsed)
        {
            return Results.Conflict(new { message = "Ce tiers est deja utilise dans des documents ou reglements." });
        }

        dbContext.BusinessPartners.Remove(partner);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetProductsAsync(
        bool? trackedOnly,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var query = dbContext.Products
            .AsNoTracking()
            .Include(x => x.ProductCategory)
            .Include(x => x.TaxCode)
            .Where(x => x.TenantId == tenantResult.Tenant!.Id);

        if (trackedOnly == true)
        {
            query = query.Where(x => x.TrackStock);
        }

        var products = await query
            .OrderBy(x => x.Sku)
            .Select(x => ApiMappingHelpers.MapProduct(x))
            .ToListAsync(cancellationToken);

        return Results.Ok(products);
    }

    private static async Task<IResult> GetProductAsync(
        Guid id,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var product = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.ProductCategory)
            .Include(x => x.TaxCode)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantResult.Tenant!.Id, cancellationToken);

        return product is null ? Results.NotFound() : Results.Ok(ApiMappingHelpers.MapProduct(product));
    }

    private static async Task<IResult> CreateProductAsync(
        ApiProductRequest request,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ITenantQuotaEnforcementService tenantQuotaEnforcementService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var tenantId = tenantResult.Tenant!.Id;
        var validation = await ApiValidationHelpers.ValidateProductRequestAsync(tenantId, request, dbContext, null, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            await tenantQuotaEnforcementService.EnsureCanCreateProductAsync(tenantId, request.IsActive, cancellationToken);

            var product = new Product { TenantId = tenantId };
            ApiMappingHelpers.ApplyProductRequest(product, request);
            dbContext.Products.Add(product);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/v1/products/{product.Id}", ApiMappingHelpers.MapProduct(product));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> UpdateProductAsync(
        Guid id,
        ApiProductRequest request,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ITenantQuotaEnforcementService tenantQuotaEnforcementService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var tenantId = tenantResult.Tenant!.Id;
        var product = await dbContext.Products
            .Include(x => x.ProductCategory)
            .Include(x => x.TaxCode)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);

        if (product is null)
        {
            return Results.NotFound();
        }

        var validation = await ApiValidationHelpers.ValidateProductRequestAsync(tenantId, request, dbContext, id, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            await tenantQuotaEnforcementService.EnsureCanUpdateProductAsync(tenantId, id, request.IsActive, cancellationToken);

            ApiMappingHelpers.ApplyProductRequest(product, request);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(ApiMappingHelpers.MapProduct(product));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> DeleteProductAsync(
        Guid id,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var tenantId = tenantResult.Tenant!.Id;
        var product = await dbContext.Products
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);

        if (product is null)
        {
            return Results.NotFound();
        }

        var isUsed = await dbContext.CommercialDocumentLines.AnyAsync(x => x.ProductId == id, cancellationToken)
            || await dbContext.StockMovements.AnyAsync(x => x.ProductId == id && x.TenantId == tenantId, cancellationToken)
            || await dbContext.PriceListLines.AnyAsync(x => x.ProductId == id, cancellationToken);

        if (isUsed)
        {
            return Results.Conflict(new { message = "Cet article est deja utilise dans des lignes, tarifs ou mouvements de stock." });
        }

        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetWarehousesAsync(
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var warehouses = await dbContext.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantResult.Tenant!.Id)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Code)
            .Select(x => new ApiWarehouseResponse(x.Id, x.Code, x.Label, x.IsDefault))
            .ToListAsync(cancellationToken);

        return Results.Ok(warehouses);
    }

    private static async Task<IResult> GetDocumentsAsync(
        CommercialDocumentType? type,
        CommercialDocumentStatus? status,
        string? family,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var query = dbContext.CommercialDocuments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.SourceDocument)
            .Where(x => x.TenantId == tenantResult.Tenant!.Id);

        if (type.HasValue)
        {
            query = query.Where(x => x.DocumentType == type.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var normalizedFamily = ApiMappingHelpers.NormalizeDocumentFamily(family);
        if (normalizedFamily == "sales")
        {
            query = query.Where(x => ApiMappingHelpers.IsSalesDocument(x.DocumentType));
        }
        else if (normalizedFamily == "purchases")
        {
            query = query.Where(x => ApiMappingHelpers.IsPurchaseDocument(x.DocumentType));
        }

        var documents = await query
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Select(x => new ApiDocumentListItem(
                x.Id,
                x.Number,
                x.DocumentType,
                x.Status,
                x.DocumentDate,
                x.DueDate,
                x.Partner != null ? x.Partner.Name : "-",
                x.SourceDocument != null ? x.SourceDocument.Number : null,
                x.TotalIncludingTax,
                x.CurrencyCode))
            .ToListAsync(cancellationToken);

        return Results.Ok(documents);
    }

    private static async Task<IResult> GetDocumentAsync(
        Guid id,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var response = await ApiMappingHelpers.LoadDocumentResponseAsync(dbContext, tenantResult.Tenant!.Id, id, cancellationToken);
        return response is null ? Results.NotFound() : Results.Ok(response);
    }

    private static async Task<IResult> CreateDocumentAsync(
        ApiDocumentCreateRequest request,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ICommercialDocumentWorkflowService workflowService,
        ITenantQuotaEnforcementService tenantQuotaEnforcementService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var tenantId = tenantResult.Tenant!.Id;
        var validation = await ApiValidationHelpers.ValidateDocumentCreateRequestAsync(tenantId, request, dbContext, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            await tenantQuotaEnforcementService.EnsureCanCreateDocumentAsync(tenantId, request.DocumentDate, cancellationToken);

            var document = await workflowService.InitializeDraftAsync(tenantId, request.DocumentType, cancellationToken);
            ApiMappingHelpers.ApplyDocumentHeader(document, request.PartnerId, request.WarehouseId, request.DocumentDate, request.DueDate, request.CurrencyCode, request.Status, request.Notes);
            ApiMappingHelpers.ApplyDocumentLines(document, request.Lines);
            workflowService.RecalculateTotals(document);

            dbContext.CommercialDocuments.Add(document);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = await ApiMappingHelpers.LoadDocumentResponseAsync(dbContext, tenantId, document.Id, cancellationToken);
            return Results.Created($"/api/v1/documents/{document.Id}", response);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> UpdateDocumentAsync(
        Guid id,
        ApiDocumentUpdateRequest request,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ICommercialDocumentWorkflowService workflowService,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var tenantId = tenantResult.Tenant!.Id;
        var document = await dbContext.CommercialDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            return Results.NotFound();
        }

        var validation = await ApiValidationHelpers.ValidateDocumentUpdateRequestAsync(tenantId, document.DocumentType, request, dbContext, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        ApiMappingHelpers.ApplyDocumentHeader(document, request.PartnerId, request.WarehouseId, request.DocumentDate, request.DueDate, request.CurrencyCode, request.Status, request.Notes);
        dbContext.CommercialDocumentLines.RemoveRange(document.Lines);
        document.Lines.Clear();
        ApiMappingHelpers.ApplyDocumentLines(document, request.Lines);
        workflowService.RecalculateTotals(document);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ApiMappingHelpers.LoadDocumentResponseAsync(dbContext, tenantId, document.Id, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> TransformDocumentAsync(
        Guid id,
        ApiDocumentTransformRequest request,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ICommercialDocumentWorkflowService workflowService,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var tenantId = tenantResult.Tenant!.Id;
        var sourceExists = await dbContext.CommercialDocuments
            .AsNoTracking()
            .AnyAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);

        if (!sourceExists)
        {
            return Results.NotFound();
        }

        try
        {
            var target = await workflowService.CreateFromSourceAsync(tenantId, id, request.TargetDocumentType, cancellationToken);
            await workflowService.SynchronizeSourceStatusAsync(id, cancellationToken);

            if (target.DocumentType == CommercialDocumentType.DeliveryNote)
            {
                await inventoryService.CreateStockIssuesAsync(tenantId, target, cancellationToken);
            }

            if (target.DocumentType == CommercialDocumentType.GoodsReceipt)
            {
                await inventoryService.CreateStockReceiptsAsync(tenantId, target, cancellationToken);
            }

            var response = await ApiMappingHelpers.LoadDocumentResponseAsync(dbContext, tenantId, target.Id, cancellationToken);
            return Results.Created($"/api/v1/documents/{target.Id}", response);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> DeleteDocumentAsync(
        Guid id,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ICommercialDocumentWorkflowService workflowService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var tenantId = tenantResult.Tenant!.Id;
        var document = await dbContext.CommercialDocuments
            .Include(x => x.DerivedDocuments)
            .Include(x => x.Lines)
            .Include(x => x.PaymentAllocations)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            return Results.NotFound();
        }

        if (document.DerivedDocuments.Count > 0)
        {
            return Results.Conflict(new { message = "Cette piece a deja servi de source pour d'autres documents." });
        }

        if (document.PaymentAllocations.Count > 0)
        {
            return Results.Conflict(new { message = "Cette piece est deja liee a des reglements." });
        }

        var sourceDocumentId = document.SourceDocumentId;
        var stockMovements = await dbContext.StockMovements
            .Where(x => x.ReferenceNumber == document.Number && x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        dbContext.StockMovements.RemoveRange(stockMovements);
        dbContext.CommercialDocumentLines.RemoveRange(document.Lines);
        dbContext.CommercialDocuments.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (sourceDocumentId.HasValue)
        {
            await workflowService.SynchronizeSourceStatusAsync(sourceDocumentId.Value, cancellationToken);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> GetOpenItemsAsync(
        string? scope,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ISettlementService settlementService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var direction = ApiMappingHelpers.NormalizeFinanceScope(scope) == "payables"
            ? PaymentDirection.Outgoing
            : PaymentDirection.Incoming;

        var items = await settlementService.GetOpenItemsAsync(tenantResult.Tenant!.Id, direction, cancellationToken);
        return Results.Ok(items);
    }

    private static async Task<IResult> GetPaymentsAsync(
        string? scope,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ISettlementService settlementService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        var direction = ApiMappingHelpers.NormalizeFinanceScope(scope) == "payables"
            ? PaymentDirection.Outgoing
            : PaymentDirection.Incoming;

        var payments = await settlementService.GetPaymentsAsync(tenantResult.Tenant!.Id, direction, cancellationToken);
        return Results.Ok(payments);
    }

    private static async Task<IResult> RegisterPaymentAsync(
        ApiPaymentRequest request,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        ISettlementService settlementService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        try
        {
            await settlementService.RegisterPaymentAsync(
                tenantResult.Tenant!.Id,
                new PaymentRegistrationRequest(
                    request.DocumentId,
                    request.PaymentDate,
                    request.Amount,
                    request.Method,
                    request.ReferenceNumber,
                    request.Notes),
                cancellationToken);

            return Results.Created("/api/v1/finance/payments", new { message = "Reglement enregistre." });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> GetInventoryDashboardAsync(
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        return Results.Ok(await inventoryService.GetDashboardAsync(tenantResult.Tenant!.Id, cancellationToken));
    }

    private static async Task<IResult> GetInventoryMovementsAsync(
        Guid? productId,
        Guid? warehouseId,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        return Results.Ok(await inventoryService.GetMovementsAsync(tenantResult.Tenant!.Id, productId, warehouseId, cancellationToken));
    }

    private static async Task<IResult> RegisterInventoryAdjustmentAsync(
        ApiStockAdjustmentRequest request,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        var tenantResult = await ApiMappingHelpers.ResolveTenantAsync(user, userManager, dbContext, currentTenantAccessor, cancellationToken);
        if (tenantResult.Result is not null)
        {
            return tenantResult.Result;
        }

        try
        {
            await inventoryService.RegisterAdjustmentAsync(
                tenantResult.Tenant!.Id,
                new StockAdjustmentRequest(
                    request.ProductId,
                    request.WarehouseId,
                    request.MovementDate,
                    request.MovementType,
                    request.Quantity,
                    request.UnitCost,
                    request.ReferenceNumber,
                    request.LotNumber,
                    request.SerialNumber,
                    request.ExpirationDate),
                cancellationToken);

            return Results.Created("/api/v1/inventory/movements", new { message = "Ajustement d'inventaire enregistre." });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }
}
