using System.Security.Claims;
using Asp.Versioning;
using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GescomSaas.Web.Api;

public static class RestApiEndpoints
{
    private const string OfflineSyncHeaderName = "X-LigCom-Offline-Key";

    public static IEndpointRouteBuilder MapGescomApi(this IEndpointRouteBuilder app)
    {
        // ApiVersionSet partage : permet d'ajouter ulterieurement v2 / deprecation
        // sans toucher a chaque MapGet/MapPost individuellement.
        var versionSet = app.CreateGescomVersionSet();

        var auth = app.MapGroup("/api/auth").WithTags("API Authentication");
        auth.MapPost("/logout", LogoutAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = $"{IdentityConstants.ApplicationScheme},{IdentityConstants.BearerScheme}"
            });

        // Le {version:apiVersion} dans le path est substitue par "v1" via
        // SubstituteApiVersionInUrl. On garde HasApiVersion explicite pour que
        // l'ApiExplorer (et donc Swagger) sache lister cet endpoint sous le groupe v1.
        var api = app.MapGroup("/api/v{version:apiVersion}")
            .WithTags("Gescom REST API")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = $"{IdentityConstants.ApplicationScheme},{IdentityConstants.BearerScheme}"
            })
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0));

        api.MapGet("/context", GetContextAsync);
        api.MapGet("/dashboard", GetDashboardAsync);

        // === Mode hors ligne forcé : exige re-authentification + permission admin ===
        api.MapPost("/offline-mode/verify", VerifyOfflineModeToggleAsync);

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

        // Sous-API offline-sync : meme version set, deja path-versioned.
        var offline = app.MapGroup("/api/offline-sync/v{version:apiVersion}")
            .WithTags("LigCom Offline Sync")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0));
        offline.MapGet("/bootstrap", PullOfflineBootstrapAsync);
        offline.MapGet("/reference-data/pull", PullReferenceDataOfflineAsync);
        offline.MapPost("/products/push", PushProductsAsync);
        offline.MapGet("/products/pull", PullProductsAsync);
        offline.MapPost("/documents/push", PushDocumentsAsync);
        offline.MapGet("/documents/pull", PullDocumentsAsync);
        offline.MapPost("/stock-documents/push", PushStockDocumentsAsync);
        offline.MapGet("/stock-documents/pull", PullStockDocumentsAsync);
        offline.MapPost("/payments/push", PushPaymentsOfflineAsync);
        offline.MapGet("/payments/pull", PullPaymentsOfflineAsync);

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

    /// <summary>
    /// Bascule REELLE du mode d'execution via le toggle : apres verification du droit
    /// et du mot de passe administrateur, ecrit l'override (Hors ligne / En ligne) puis
    /// declenche un redemarrage de l'application. Sous un superviseur (IIS, service
    /// Windows, Docker restart), l'instance repart automatiquement dans le nouveau mode.
    /// </summary>
    private static async Task<IResult> VerifyOfflineModeToggleAsync(
        OfflineModeVerifyRequest request,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        IUserPermissionService permissionService,
        IHostEnvironment hostEnvironment,
        IHostApplicationLifetime applicationLifetime,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrEmpty(request.Password))
        {
            return Results.Json(new { ok = false, error = "Mot de passe requis." }, statusCode: 400);
        }

        var dbUser = await userManager.GetUserAsync(user);
        if (dbUser is null)
        {
            return Results.Unauthorized();
        }

        // Permission admin requise (settings.offline_sync.manage)
        var hasPermission = await permissionService.HasAnyPermissionAsync(
            user,
            new[] { TenantPermissionKeys.SettingsOfflineSyncManage },
            cancellationToken);

        if (!hasPermission)
        {
            return Results.Json(
                new { ok = false, error = "Action reservee aux administrateurs (permission settings.offline_sync.manage)." },
                statusCode: 403);
        }

        // Re-authentification : vérification du mot de passe sans changer la session
        var passwordOk = await userManager.CheckPasswordAsync(dbUser, request.Password);
        if (!passwordOk)
        {
            return Results.Json(new { ok = false, error = "Mot de passe incorrect." }, statusCode: 401);
        }

        // Persiste le nouveau mode : Enable=true => Hors ligne (SQLite), false => En ligne (SQL Server).
        await GescomSaas.Web.LocalRuntimeSettingsStore.SaveStartupModeAsync(
            hostEnvironment,
            offline: request.Enable,
            sqliteDatabasePath: null,
            cancellationToken);

        // Redemarrage differe pour laisser la reponse partir avant l'arret du processus.
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            applicationLifetime.StopApplication();
        });

        return Results.Ok(new
        {
            ok = true,
            enable = request.Enable,
            restarting = true,
            authorizedBy = dbUser.UserName,
            authorizedAt = DateTime.UtcNow
        });
    }

    public sealed record OfflineModeVerifyRequest(bool Enable, string Password);

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
        ISettlementService settlementService,
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
            await settlementService.EnsureSalesDocumentAllowedAsync(tenantId, request.PartnerId, request.DocumentType, cancellationToken);

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
                    request.PartnerId,
                    request.Direction,
                    request.PaymentDate,
                    request.Amount,
                    request.Type,
                    request.AllocationMode,
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

    private static async Task<IResult> PushProductsAsync(
        OfflineProductPushRequest request,
        HttpRequest httpRequest,
        IOptions<OfflineSyncOptions> offlineOptions,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateOfflineSyncAccess(httpRequest, offlineOptions.Value);
        if (accessError is not null)
        {
            return accessError;
        }

        if (request.TenantId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Le tenant de synchronisation est obligatoire." });
        }

        var tenantExists = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.TenantId, cancellationToken);

        if (!tenantExists)
        {
            return Results.NotFound(new { message = "Tenant introuvable pour la synchronisation hors connexion." });
        }

        var categories = await dbContext.ProductCategories
            .Where(x => x.TenantId == request.TenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var taxCodes = await dbContext.TaxCodes
            .Where(x => x.TenantId == request.TenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingProducts = await dbContext.Products
            .Where(x => x.TenantId == request.TenantId)
            .ToDictionaryAsync(x => x.Sku, StringComparer.OrdinalIgnoreCase, cancellationToken);

        List<string> notes = [];
        var created = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var item in request.Products)
        {
            if (string.IsNullOrWhiteSpace(item.Sku))
            {
                continue;
            }

            var categoryId = ResolveCentralCategoryId(item, categories, notes);
            var taxCodeId = ResolveCentralTaxCodeId(item, taxCodes, notes);

            if (!existingProducts.TryGetValue(item.Sku.Trim().ToUpperInvariant(), out var product))
            {
                product = new Product { TenantId = request.TenantId };
                ApplyOfflineProduct(product, item, categoryId, taxCodeId);
                dbContext.Products.Add(product);
                existingProducts[product.Sku] = product;
                created++;
                continue;
            }

            if (ApplyOfflineProduct(product, item, categoryId, taxCodeId))
            {
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new OfflineProductPushResponse(
            request.Products.Count,
            created,
            updated,
            unchanged,
            notes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
    }

    private static async Task<IResult> PullProductsAsync(
        Guid tenantId,
        string? nodeId,
        HttpRequest httpRequest,
        IOptions<OfflineSyncOptions> offlineOptions,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateOfflineSyncAccess(httpRequest, offlineOptions.Value);
        if (accessError is not null)
        {
            return accessError;
        }

        if (tenantId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Le tenant de synchronisation est obligatoire." });
        }

        var tenantExists = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Id == tenantId, cancellationToken);

        if (!tenantExists)
        {
            return Results.NotFound(new { message = "Tenant introuvable pour la synchronisation hors connexion." });
        }

        var products = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.ProductCategory)
            .Include(x => x.TaxCode)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Sku)
            .Select(x => new OfflineProductSyncItem(
                x.Sku,
                x.Label,
                x.Description,
                x.ProductType.ToString(),
                x.UnitOfMeasure,
                x.TrackStock,
                x.StockValuationMethod.ToString(),
                x.StockIdentityTrackingMode.ToString(),
                x.IsActive,
                x.ProductCategory != null ? x.ProductCategory.Code : null,
                x.ProductCategory != null ? x.ProductCategory.Label : null,
                x.TaxCode != null ? x.TaxCode.Code : null,
                x.TaxCode != null ? x.TaxCode.Label : null,
                x.TaxCode != null ? x.TaxCode.Rate : null,
                x.PurchasePrice,
                x.SalesPrice,
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new OfflineProductPullResponse(
            tenantId,
            string.IsNullOrWhiteSpace(nodeId) ? "unknown-node" : nodeId.Trim(),
            DateTime.UtcNow,
            products));
    }

    private static async Task<IResult> PullOfflineBootstrapAsync(
        Guid? tenantId,
        string? tenantSlug,
        string? nodeId,
        HttpRequest httpRequest,
        IOptions<OfflineSyncOptions> offlineOptions,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateOfflineSyncAccess(httpRequest, offlineOptions.Value);
        if (accessError is not null)
        {
            return accessError;
        }

        if (!tenantId.HasValue && string.IsNullOrWhiteSpace(tenantSlug))
        {
            return Results.BadRequest(new { message = "Le tenantId ou le tenantSlug est obligatoire pour initialiser le noeud local." });
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => tenantId.HasValue
                    ? x.Id == tenantId.Value
                    : x.Slug == tenantSlug!.Trim(),
                cancellationToken);

        if (tenant is null)
        {
            return Results.NotFound(new { message = "Tenant introuvable pour le bootstrap du noeud local." });
        }

        return Results.Ok(new OfflineTenantBootstrapPackage(
            tenant.Id,
            tenant.Slug,
            tenant.CompanyName,
            tenant.CompanyLegalName,
            tenant.PrimaryContactEmail,
            tenant.PhoneNumber,
            tenant.AddressLine1,
            tenant.AddressLine2,
            tenant.PostalCode,
            tenant.City,
            tenant.State,
            tenant.CountryCode,
            tenant.CurrencyCode,
            tenant.CashCurrencyCode,
            tenant.CurrencySymbol,
            tenant.CurrencySymbolPosition.ToString(),
            tenant.MoneyDecimalSeparator,
            tenant.MoneyGroupSeparator,
            tenant.MoneyDecimalPlaces,
            tenant.QuantityDecimalSeparator,
            tenant.QuantityGroupSeparator,
            tenant.QuantityDecimalPlaces,
            tenant.PaymentMethodsJson,
            tenant.PartnerLookupMode.ToString(),
            tenant.IncomingPaymentAllocationMode.ToString(),
            tenant.ReminderFriendlyDelayDays,
            tenant.ReminderFormalDelayDays,
            tenant.ReminderFinalNoticeDelayDays,
            tenant.BlockSalesOrdersOnCreditLimit,
            tenant.BlockSalesOrdersOnOverdue,
            tenant.BlockDeliveriesOnCreditLimit,
            tenant.BlockDeliveriesOnOverdue,
            tenant.AllowNegativeStock,
            tenant.DefaultStockValuationMethod.ToString(),
            tenant.VisualTheme.ToString(),
            tenant.IsActive,
            DateTime.UtcNow,
            string.IsNullOrWhiteSpace(nodeId) ? "unknown-node" : nodeId.Trim()));
    }

    private static IResult? ValidateOfflineSyncAccess(HttpRequest httpRequest, OfflineSyncOptions options)
    {
        if (!options.Enabled)
        {
            return Results.Problem("La synchronisation hors connexion est desactivee sur cette instance.", statusCode: StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(options.SharedAccessKey))
        {
            return Results.Problem("La cle partagee de synchronisation n'est pas configuree.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!httpRequest.Headers.TryGetValue(OfflineSyncHeaderName, out var suppliedKey))
        {
            return Results.Unauthorized();
        }

        return string.Equals(suppliedKey.ToString(), options.SharedAccessKey.Trim(), StringComparison.Ordinal)
            ? null
            : Results.Unauthorized();
    }

    private static async Task<IResult> PullReferenceDataOfflineAsync(
        Guid tenantId,
        string? nodeId,
        HttpRequest httpRequest,
        IOptions<OfflineSyncOptions> offlineOptions,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateOfflineSyncAccess(httpRequest, offlineOptions.Value);
        if (accessError is not null)
        {
            return accessError;
        }

        if (tenantId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Le tenant de synchronisation est obligatoire." });
        }

        var tenantExists = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Id == tenantId, cancellationToken);

        if (!tenantExists)
        {
            return Results.NotFound(new { message = "Tenant introuvable pour la synchronisation des referentiels." });
        }

        var paymentTerms = await dbContext.PaymentTerms
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new OfflinePaymentTermSyncItem(
                x.Code,
                x.Label,
                x.DueInDays))
            .ToListAsync(cancellationToken);

        var productCategories = await dbContext.ProductCategories
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new OfflineProductCategorySyncItem(
                x.Code,
                x.Label,
                x.StockValuationMethod.ToString(),
                x.StockIdentityTrackingMode.ToString()))
            .ToListAsync(cancellationToken);

        var taxCodes = await dbContext.TaxCodes
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new OfflineTaxCodeSyncItem(
                x.Code,
                x.Label,
                x.Rate))
            .ToListAsync(cancellationToken);

        var warehouses = await dbContext.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new OfflineWarehouseSyncItem(
                x.Code,
                x.Label,
                x.IsDefault))
            .ToListAsync(cancellationToken);

        var partners = await dbContext.BusinessPartners
            .AsNoTracking()
            .Include(x => x.PaymentTerm)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new OfflineBusinessPartnerSyncItem(
                x.Code,
                x.Name,
                x.PartnerType.ToString(),
                x.Email,
                x.PhoneNumber,
                x.VatNumber,
                x.CreditLimit,
                x.IsActive,
                x.PaymentTerm != null ? x.PaymentTerm.Code : null,
                new OfflineAddressSyncItem(
                    x.BillingAddress.Recipient,
                    x.BillingAddress.StreetLine1,
                    x.BillingAddress.StreetLine2,
                    x.BillingAddress.PostalCode,
                    x.BillingAddress.City,
                    x.BillingAddress.State,
                    x.BillingAddress.Country),
                new OfflineAddressSyncItem(
                    x.ShippingAddress.Recipient,
                    x.ShippingAddress.StreetLine1,
                    x.ShippingAddress.StreetLine2,
                    x.ShippingAddress.PostalCode,
                    x.ShippingAddress.City,
                    x.ShippingAddress.State,
                    x.ShippingAddress.Country),
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        var documentSequences = await dbContext.DocumentSequences
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.DocumentType)
            .Select(x => new OfflineDocumentSequenceSyncItem(
                x.DocumentType.ToString(),
                x.Mode.ToString(),
                x.Prefix,
                x.NumberLength,
                x.NextValue))
            .ToListAsync(cancellationToken);

        var referenceNumberingSettings = await dbContext.ReferenceNumberingSettings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Scope)
            .Select(x => new OfflineReferenceNumberingSettingSyncItem(
                x.Scope.ToString(),
                x.Mode.ToString(),
                x.Prefix,
                x.NumberLength,
                x.NextValue))
            .ToListAsync(cancellationToken);

        var journalAccounts = await dbContext.JournalAccounts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new OfflineJournalAccountSyncItem(
                x.Code,
                x.Label,
                x.CounterpartAccountCode))
            .ToListAsync(cancellationToken);

        return Results.Ok(new OfflineReferenceDataPullResponse(
            tenantId,
            string.IsNullOrWhiteSpace(nodeId) ? "unknown-node" : nodeId.Trim(),
            DateTime.UtcNow,
            paymentTerms,
            productCategories,
            taxCodes,
            warehouses,
            partners,
            documentSequences,
            referenceNumberingSettings,
            journalAccounts));
    }

    private static Guid? ResolveCentralCategoryId(
        OfflineProductSyncItem item,
        Dictionary<string, ProductCategory> categories,
        List<string> notes)
    {
        if (string.IsNullOrWhiteSpace(item.ProductCategoryCode))
        {
            return null;
        }

        var code = item.ProductCategoryCode.Trim().ToUpperInvariant();
        if (!categories.TryGetValue(code, out var category))
        {
            notes.Add($"Famille article absente cote central pour le code {code}.");
            return null;
        }

        return category.Id;
    }

    private static Guid? ResolveCentralTaxCodeId(
        OfflineProductSyncItem item,
        Dictionary<string, TaxCode> taxCodes,
        List<string> notes)
    {
        if (string.IsNullOrWhiteSpace(item.TaxCodeCode))
        {
            return null;
        }

        var code = item.TaxCodeCode.Trim().ToUpperInvariant();
        if (!taxCodes.TryGetValue(code, out var taxCode))
        {
            notes.Add($"Code taxe absent cote central pour le code {code}.");
            return null;
        }

        return taxCode.Id;
    }

    private static async Task<IResult> PushDocumentsAsync(
        OfflineCommercialDocumentPushRequest request,
        HttpRequest httpRequest,
        IOptions<OfflineSyncOptions> offlineOptions,
        ApplicationDbContext dbContext,
        ICommercialDocumentWorkflowService workflowService,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateOfflineSyncAccess(httpRequest, offlineOptions.Value);
        if (accessError is not null)
        {
            return accessError;
        }

        if (request.TenantId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Le tenant de synchronisation est obligatoire." });
        }

        var tenantExists = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.TenantId, cancellationToken);

        if (!tenantExists)
        {
            return Results.NotFound(new { message = "Tenant introuvable pour la synchronisation des documents." });
        }

        var partners = await dbContext.BusinessPartners
            .Where(x => x.TenantId == request.TenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var warehouses = await dbContext.Warehouses
            .Where(x => x.TenantId == request.TenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var products = await dbContext.Products
            .Where(x => x.TenantId == request.TenantId)
            .ToDictionaryAsync(x => x.Sku, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingDocuments = await dbContext.CommercialDocuments
            .Include(x => x.Lines)
            .Include(x => x.PaymentAllocations)
            .Include(x => x.DerivedDocuments)
            .Where(x => x.TenantId == request.TenantId)
            .ToDictionaryAsync(x => x.Number, StringComparer.OrdinalIgnoreCase, cancellationToken);

        List<string> notes = [];
        var created = 0;
        var updated = 0;
        var unchanged = 0;
        var skipped = 0;

        foreach (var item in request.Documents)
        {
            if (!TryResolveCentralDocumentDependencies(item, partners, warehouses, products, out var resolution, out var note))
            {
                skipped++;
                if (!string.IsNullOrWhiteSpace(note))
                {
                    notes.Add(note);
                }

                continue;
            }

            var documentNumber = item.Number.Trim().ToUpperInvariant();
            if (!existingDocuments.TryGetValue(documentNumber, out var document))
            {
                document = new CommercialDocument
                {
                    TenantId = request.TenantId,
                    Number = documentNumber
                };

                ApplyOfflineDocumentHeader(document, item, resolution);
                ReplaceOfflineDocumentLines(document, item, products);
                workflowService.RecalculateTotals(document);
                ApplyOfflineFinancialFlags(document, item);
                dbContext.CommercialDocuments.Add(document);
                existingDocuments[document.Number] = document;
                created++;
                continue;
            }

            if (document.PaymentAllocations.Count > 0 || document.DerivedDocuments.Count > 0)
            {
                skipped++;
                notes.Add($"Document {document.Number} ignore car deja engage cote central.");
                continue;
            }

            var changed = ApplyOfflineDocumentHeader(document, item, resolution);
            changed |= ReplaceOfflineDocumentLines(document, item, products);
            workflowService.RecalculateTotals(document);
            changed |= ApplyOfflineFinancialFlags(document, item);

            if (changed)
            {
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new OfflineCommercialDocumentPushResponse(
            request.Documents.Count,
            created,
            updated,
            unchanged,
            skipped,
            notes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
    }

    private static async Task<IResult> PullDocumentsAsync(
        Guid tenantId,
        string? nodeId,
        HttpRequest httpRequest,
        IOptions<OfflineSyncOptions> offlineOptions,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateOfflineSyncAccess(httpRequest, offlineOptions.Value);
        if (accessError is not null)
        {
            return accessError;
        }

        if (tenantId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Le tenant de synchronisation est obligatoire." });
        }

        var tenantExists = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Id == tenantId, cancellationToken);

        if (!tenantExists)
        {
            return Results.NotFound(new { message = "Tenant introuvable pour la synchronisation des documents." });
        }

        var documents = await dbContext.CommercialDocuments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.Warehouse)
            .Include(x => x.SourceDocument)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Number)
            .Select(x => new OfflineCommercialDocumentSyncItem(
                x.Number,
                x.DocumentType.ToString(),
                x.Status.ToString(),
                x.DocumentDate,
                x.DueDate,
                x.CurrencyCode,
                x.Partner != null ? x.Partner.Code : string.Empty,
                x.Warehouse != null ? x.Warehouse.Code : null,
                x.Notes,
                x.PaymentStatus.ToString(),
                x.PaidAmount,
                x.BalanceAmount,
                x.InDispute,
                x.PromiseToPayDate,
                x.SourceDocument != null ? x.SourceDocument.Number : null,
                x.Lines
                    .OrderBy(line => line.CreatedOnUtc)
                    .Select(line => new OfflineCommercialDocumentLineSyncItem(
                        line.Product != null ? line.Product.Sku : null,
                        line.Description,
                        line.Quantity,
                        line.UnitPriceExcludingTax,
                        line.DiscountRate,
                        line.TaxRate,
                        line.LotNumber,
                        line.SerialNumber,
                        line.ExpirationDate))
                    .ToList(),
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new OfflineCommercialDocumentPullResponse(
            tenantId,
            string.IsNullOrWhiteSpace(nodeId) ? "unknown-node" : nodeId.Trim(),
            DateTime.UtcNow,
            documents));
    }

    private static bool ApplyOfflineProduct(Product product, OfflineProductSyncItem item, Guid? categoryId, Guid? taxCodeId)
    {
        var changed = false;
        var normalizedSku = item.Sku.Trim().ToUpperInvariant();
        var normalizedLabel = item.Label.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim();
        var normalizedUnit = item.UnitOfMeasure.Trim().ToUpperInvariant();
        var productType = ParseEnum(item.ProductType, ProductType.StockItem);
        var valuationMethod = ParseEnum(item.StockValuationMethod, StockValuationMethod.Cmup);
        var trackingMode = ParseEnum(item.StockIdentityTrackingMode, StockIdentityTrackingMode.None);

        if (!string.Equals(product.Sku, normalizedSku, StringComparison.Ordinal))
        {
            product.Sku = normalizedSku;
            changed = true;
        }

        if (!string.Equals(product.Label, normalizedLabel, StringComparison.Ordinal))
        {
            product.Label = normalizedLabel;
            changed = true;
        }

        if (!string.Equals(product.Description, normalizedDescription, StringComparison.Ordinal))
        {
            product.Description = normalizedDescription;
            changed = true;
        }

        if (product.ProductType != productType)
        {
            product.ProductType = productType;
            changed = true;
        }

        if (!string.Equals(product.UnitOfMeasure, normalizedUnit, StringComparison.Ordinal))
        {
            product.UnitOfMeasure = normalizedUnit;
            changed = true;
        }

        if (product.TrackStock != item.TrackStock)
        {
            product.TrackStock = item.TrackStock;
            changed = true;
        }

        if (product.StockValuationMethod != valuationMethod)
        {
            product.StockValuationMethod = valuationMethod;
            changed = true;
        }

        if (product.StockIdentityTrackingMode != trackingMode)
        {
            product.StockIdentityTrackingMode = trackingMode;
            changed = true;
        }

        if (product.IsActive != item.IsActive)
        {
            product.IsActive = item.IsActive;
            changed = true;
        }

        if (product.ProductCategoryId != categoryId)
        {
            product.ProductCategoryId = categoryId;
            changed = true;
        }

        if (product.TaxCodeId != taxCodeId)
        {
            product.TaxCodeId = taxCodeId;
            changed = true;
        }

        if (product.PurchasePrice != item.PurchasePrice)
        {
            product.PurchasePrice = item.PurchasePrice;
            changed = true;
        }

        if (product.SalesPrice != item.SalesPrice)
        {
            product.SalesPrice = item.SalesPrice;
            changed = true;
        }

        return changed;
    }

    private static bool TryResolveCentralDocumentDependencies(
        OfflineCommercialDocumentSyncItem item,
        IReadOnlyDictionary<string, BusinessPartner> partners,
        IReadOnlyDictionary<string, Warehouse> warehouses,
        IReadOnlyDictionary<string, Product> products,
        out ResolvedCentralDocumentDependencies resolution,
        out string? note)
    {
        resolution = default;
        note = null;

        if (string.IsNullOrWhiteSpace(item.PartnerCode))
        {
            note = $"Document {item.Number} ignore car le tiers n'est pas renseigne.";
            return false;
        }

        var partnerCode = item.PartnerCode.Trim().ToUpperInvariant();
        if (!partners.TryGetValue(partnerCode, out var partner))
        {
            note = $"Document {item.Number} ignore car le tiers {partnerCode} est absent cote central.";
            return false;
        }

        Warehouse? warehouse = null;
        if (!string.IsNullOrWhiteSpace(item.WarehouseCode))
        {
            var warehouseCode = item.WarehouseCode.Trim().ToUpperInvariant();
            if (!warehouses.TryGetValue(warehouseCode, out warehouse))
            {
                note = $"Document {item.Number} ignore car le depot {warehouseCode} est absent cote central.";
                return false;
            }
        }

        foreach (var line in item.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ProductSku))
            {
                continue;
            }

            var sku = line.ProductSku.Trim().ToUpperInvariant();
            if (!products.ContainsKey(sku))
            {
                note = $"Document {item.Number} ignore car l'article {sku} est absent cote central.";
                return false;
            }
        }

        resolution = new ResolvedCentralDocumentDependencies(partner.Id, warehouse?.Id);
        return true;
    }

    private static bool ApplyOfflineDocumentHeader(
        CommercialDocument document,
        OfflineCommercialDocumentSyncItem item,
        ResolvedCentralDocumentDependencies resolution)
    {
        var changed = false;
        var documentType = ParseEnum(item.DocumentType, CommercialDocumentType.SalesQuote);
        var status = ParseEnum(item.Status, CommercialDocumentStatus.Draft);
        var currencyCode = string.IsNullOrWhiteSpace(item.CurrencyCode) ? "CAD" : item.CurrencyCode.Trim().ToUpperInvariant();
        var notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();

        if (document.DocumentType != documentType)
        {
            document.DocumentType = documentType;
            changed = true;
        }

        if (document.Status != status)
        {
            document.Status = status;
            changed = true;
        }

        if (document.DocumentDate != item.DocumentDate)
        {
            document.DocumentDate = item.DocumentDate;
            changed = true;
        }

        if (document.DueDate != item.DueDate)
        {
            document.DueDate = item.DueDate;
            changed = true;
        }

        if (!string.Equals(document.CurrencyCode, currencyCode, StringComparison.Ordinal))
        {
            document.CurrencyCode = currencyCode;
            changed = true;
        }

        if (document.PartnerId != resolution.PartnerId)
        {
            document.PartnerId = resolution.PartnerId;
            changed = true;
        }

        if (document.WarehouseId != resolution.WarehouseId)
        {
            document.WarehouseId = resolution.WarehouseId;
            changed = true;
        }

        if (!string.Equals(document.Notes, notes, StringComparison.Ordinal))
        {
            document.Notes = notes;
            changed = true;
        }

        return changed;
    }

    private static bool ReplaceOfflineDocumentLines(
        CommercialDocument document,
        OfflineCommercialDocumentSyncItem item,
        IReadOnlyDictionary<string, Product> products)
    {
        var changed = true;
        document.Lines.Clear();

        foreach (var line in item.Lines)
        {
            Guid? productId = null;
            if (!string.IsNullOrWhiteSpace(line.ProductSku))
            {
                var sku = line.ProductSku.Trim().ToUpperInvariant();
                if (products.TryGetValue(sku, out var product))
                {
                    productId = product.Id;
                }
            }

            document.Lines.Add(new CommercialDocumentLine
            {
                ProductId = productId,
                Description = line.Description.Trim(),
                Quantity = line.Quantity,
                UnitPriceExcludingTax = line.UnitPriceExcludingTax,
                DiscountRate = line.DiscountRate,
                TaxRate = line.TaxRate,
                LotNumber = string.IsNullOrWhiteSpace(line.LotNumber) ? null : line.LotNumber.Trim().ToUpperInvariant(),
                SerialNumber = string.IsNullOrWhiteSpace(line.SerialNumber) ? null : line.SerialNumber.Trim().ToUpperInvariant(),
                ExpirationDate = line.ExpirationDate
            });
        }

        return changed;
    }

    private static bool ApplyOfflineFinancialFlags(CommercialDocument document, OfflineCommercialDocumentSyncItem item)
    {
        var changed = false;
        var paymentStatus = ParseEnum(item.PaymentStatus, CommercialPaymentStatus.Unpaid);

        if (document.PaymentStatus != paymentStatus)
        {
            document.PaymentStatus = paymentStatus;
            changed = true;
        }

        if (document.PaidAmount != item.PaidAmount)
        {
            document.PaidAmount = item.PaidAmount;
            changed = true;
        }

        if (document.BalanceAmount != item.BalanceAmount)
        {
            document.BalanceAmount = item.BalanceAmount;
            changed = true;
        }

        if (document.InDispute != item.InDispute)
        {
            document.InDispute = item.InDispute;
            changed = true;
        }

        if (document.PromiseToPayDate != item.PromiseToPayDate)
        {
            document.PromiseToPayDate = item.PromiseToPayDate;
            changed = true;
        }

        return changed;
    }

    private readonly record struct ResolvedCentralDocumentDependencies(Guid PartnerId, Guid? WarehouseId);

    private static async Task<IResult> PushStockDocumentsAsync(
        OfflineStockDocumentPushRequest request,
        HttpRequest httpRequest,
        IOptions<OfflineSyncOptions> offlineOptions,
        ApplicationDbContext dbContext,
        IStockDocumentService stockDocumentService,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateOfflineSyncAccess(httpRequest, offlineOptions.Value);
        if (accessError is not null)
        {
            return accessError;
        }

        if (request.TenantId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Le tenant de synchronisation est obligatoire." });
        }

        var tenantExists = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.TenantId, cancellationToken);

        if (!tenantExists)
        {
            return Results.NotFound(new { message = "Tenant introuvable pour la synchronisation des documents de stock." });
        }

        var warehouses = await dbContext.Warehouses
            .Where(x => x.TenantId == request.TenantId)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var products = await dbContext.Products
            .Where(x => x.TenantId == request.TenantId)
            .ToDictionaryAsync(x => x.Sku, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingDocuments = await dbContext.StockDocuments
            .Include(x => x.Lines)
            .Where(x => x.TenantId == request.TenantId)
            .ToDictionaryAsync(x => x.Number, StringComparer.OrdinalIgnoreCase, cancellationToken);

        List<string> notes = [];
        var created = 0;
        var updated = 0;
        var posted = 0;
        var unchanged = 0;
        var skipped = 0;

        foreach (var item in request.Documents)
        {
            if (!TryResolveCentralStockDependencies(item, warehouses, products, out var resolution, out var note))
            {
                skipped++;
                if (!string.IsNullOrWhiteSpace(note))
                {
                    notes.Add(note);
                }

                continue;
            }

            var documentNumber = item.Number.Trim().ToUpperInvariant();
            if (!existingDocuments.TryGetValue(documentNumber, out var document))
            {
                document = new StockDocument
                {
                    TenantId = request.TenantId,
                    Number = documentNumber
                };

                ApplyOfflineStockHeader(document, item, resolution);
                ReplaceOfflineStockLines(document, item, products);
                dbContext.StockDocuments.Add(document);
                existingDocuments[document.Number] = document;
                created++;

                if (ParseEnum(item.Status, StockDocumentStatus.Draft) == StockDocumentStatus.Posted)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await stockDocumentService.PostAsync(request.TenantId, document.Id, cancellationToken);
                    posted++;
                }

                continue;
            }

            if (document.Status == StockDocumentStatus.Posted)
            {
                unchanged++;
                continue;
            }

            var changed = ApplyOfflineStockHeader(document, item, resolution);
            changed |= ReplaceOfflineStockLines(document, item, products);

            var targetStatus = ParseEnum(item.Status, StockDocumentStatus.Draft);
            if (targetStatus == StockDocumentStatus.Posted)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await stockDocumentService.PostAsync(request.TenantId, document.Id, cancellationToken);
                posted++;
                continue;
            }

            if (changed)
            {
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new OfflineStockDocumentPushResponse(
            request.Documents.Count,
            created,
            updated,
            posted,
            unchanged,
            skipped,
            notes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
    }

    private static async Task<IResult> PullStockDocumentsAsync(
        Guid tenantId,
        string? nodeId,
        HttpRequest httpRequest,
        IOptions<OfflineSyncOptions> offlineOptions,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateOfflineSyncAccess(httpRequest, offlineOptions.Value);
        if (accessError is not null)
        {
            return accessError;
        }

        if (tenantId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Le tenant de synchronisation est obligatoire." });
        }

        var tenantExists = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Id == tenantId, cancellationToken);

        if (!tenantExists)
        {
            return Results.NotFound(new { message = "Tenant introuvable pour la synchronisation des documents de stock." });
        }

        var documents = await dbContext.StockDocuments
            .AsNoTracking()
            .Include(x => x.SourceWarehouse)
            .Include(x => x.DestinationWarehouse)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Number)
            .Select(x => new OfflineStockDocumentSyncItem(
                x.Number,
                x.DocumentType.ToString(),
                x.Status.ToString(),
                x.DocumentDate,
                x.SourceWarehouse != null ? x.SourceWarehouse.Code : null,
                x.DestinationWarehouse != null ? x.DestinationWarehouse.Code : null,
                x.Notes,
                x.PostedOnUtc,
                x.Lines
                    .OrderBy(line => line.CreatedOnUtc)
                    .Select(line => new OfflineStockDocumentLineSyncItem(
                        line.Product != null ? line.Product.Sku : null,
                        line.Description,
                        line.Quantity,
                        line.UnitCost,
                        line.LotNumber,
                        line.SerialNumber,
                        line.ExpirationDate))
                    .ToList(),
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new OfflineStockDocumentPullResponse(
            tenantId,
            string.IsNullOrWhiteSpace(nodeId) ? "unknown-node" : nodeId.Trim(),
            DateTime.UtcNow,
            documents));
    }

    private static bool TryResolveCentralStockDependencies(
        OfflineStockDocumentSyncItem item,
        IReadOnlyDictionary<string, Warehouse> warehouses,
        IReadOnlyDictionary<string, Product> products,
        out ResolvedCentralStockDependencies resolution,
        out string? note)
    {
        resolution = default;
        note = null;

        Warehouse? sourceWarehouse = null;
        if (!string.IsNullOrWhiteSpace(item.SourceWarehouseCode))
        {
            var sourceCode = item.SourceWarehouseCode.Trim().ToUpperInvariant();
            if (!warehouses.TryGetValue(sourceCode, out sourceWarehouse))
            {
                note = $"Document de stock {item.Number} ignore car le depot source {sourceCode} est absent cote central.";
                return false;
            }
        }

        Warehouse? destinationWarehouse = null;
        if (!string.IsNullOrWhiteSpace(item.DestinationWarehouseCode))
        {
            var destinationCode = item.DestinationWarehouseCode.Trim().ToUpperInvariant();
            if (!warehouses.TryGetValue(destinationCode, out destinationWarehouse))
            {
                note = $"Document de stock {item.Number} ignore car le depot destination {destinationCode} est absent cote central.";
                return false;
            }
        }

        foreach (var line in item.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ProductSku))
            {
                continue;
            }

            var sku = line.ProductSku.Trim().ToUpperInvariant();
            if (!products.ContainsKey(sku))
            {
                note = $"Document de stock {item.Number} ignore car l'article {sku} est absent cote central.";
                return false;
            }
        }

        resolution = new ResolvedCentralStockDependencies(sourceWarehouse?.Id, destinationWarehouse?.Id);
        return true;
    }

    private static bool ApplyOfflineStockHeader(
        StockDocument document,
        OfflineStockDocumentSyncItem item,
        ResolvedCentralStockDependencies resolution)
    {
        var changed = false;
        var documentType = ParseEnum(item.DocumentType, StockDocumentType.Entry);
        var status = ParseEnum(item.Status, StockDocumentStatus.Draft);
        var notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();

        if (document.DocumentType != documentType)
        {
            document.DocumentType = documentType;
            changed = true;
        }

        if (document.Status != status)
        {
            document.Status = status == StockDocumentStatus.Posted ? StockDocumentStatus.Draft : status;
            changed = true;
        }

        if (document.DocumentDate != item.DocumentDate)
        {
            document.DocumentDate = item.DocumentDate;
            changed = true;
        }

        if (document.SourceWarehouseId != resolution.SourceWarehouseId)
        {
            document.SourceWarehouseId = resolution.SourceWarehouseId;
            changed = true;
        }

        if (document.DestinationWarehouseId != resolution.DestinationWarehouseId)
        {
            document.DestinationWarehouseId = resolution.DestinationWarehouseId;
            changed = true;
        }

        if (!string.Equals(document.Notes, notes, StringComparison.Ordinal))
        {
            document.Notes = notes;
            changed = true;
        }

        return changed;
    }

    private static bool ReplaceOfflineStockLines(
        StockDocument document,
        OfflineStockDocumentSyncItem item,
        IReadOnlyDictionary<string, Product> products)
    {
        var changed = true;
        document.Lines.Clear();

        foreach (var line in item.Lines)
        {
            Guid? productId = null;
            if (!string.IsNullOrWhiteSpace(line.ProductSku))
            {
                var sku = line.ProductSku.Trim().ToUpperInvariant();
                if (products.TryGetValue(sku, out var product))
                {
                    productId = product.Id;
                }
            }

            document.Lines.Add(new StockDocumentLine
            {
                ProductId = productId,
                Description = line.Description.Trim(),
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                LotNumber = string.IsNullOrWhiteSpace(line.LotNumber) ? null : line.LotNumber.Trim().ToUpperInvariant(),
                SerialNumber = string.IsNullOrWhiteSpace(line.SerialNumber) ? null : line.SerialNumber.Trim().ToUpperInvariant(),
                ExpirationDate = line.ExpirationDate
            });
        }

        return changed;
    }

    private readonly record struct ResolvedCentralStockDependencies(Guid? SourceWarehouseId, Guid? DestinationWarehouseId);

    private static async Task<IResult> PushPaymentsOfflineAsync(
        OfflinePaymentPushRequest request,
        HttpRequest httpRequest,
        IOptions<OfflineSyncOptions> offlineOptions,
        ISettlementService settlementService,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateOfflineSyncAccess(httpRequest, offlineOptions.Value);
        if (accessError is not null)
        {
            return accessError;
        }

        if (request.TenantId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Le tenant de synchronisation est obligatoire." });
        }

        var tenantExists = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.TenantId, cancellationToken);

        if (!tenantExists)
        {
            return Results.NotFound(new { message = "Tenant introuvable pour la synchronisation des reglements." });
        }

        List<string> notes = [];
        var created = 0;
        var updated = 0;
        var unchanged = 0;
        var allocationRefreshCount = 0;
        var skipped = 0;

        foreach (var item in request.Payments)
        {
            if (string.IsNullOrWhiteSpace(item.ReferenceNumber) || string.IsNullOrWhiteSpace(item.PartnerCode))
            {
                skipped++;
                continue;
            }

            try
            {
                var result = await settlementService.UpsertOfflinePaymentAsync(request.TenantId, item, cancellationToken);
                await settlementService.ReplaceOfflineAllocationsAsync(request.TenantId, result.PaymentId, item.Allocations, cancellationToken);

                if (result.Created)
                {
                    created++;
                }
                else if (result.Updated)
                {
                    updated++;
                }
                else
                {
                    unchanged++;
                }

                allocationRefreshCount += item.Allocations.Count;
            }
            catch (InvalidOperationException exception)
            {
                skipped++;
                notes.Add(exception.Message);
            }
        }

        return Results.Ok(new OfflinePaymentPushResponse(
            request.Payments.Count,
            created,
            updated,
            unchanged,
            allocationRefreshCount,
            skipped,
            notes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
    }

    private static async Task<IResult> PullPaymentsOfflineAsync(
        Guid tenantId,
        string? nodeId,
        HttpRequest httpRequest,
        IOptions<OfflineSyncOptions> offlineOptions,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateOfflineSyncAccess(httpRequest, offlineOptions.Value);
        if (accessError is not null)
        {
            return accessError;
        }

        if (tenantId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Le tenant de synchronisation est obligatoire." });
        }

        var tenantExists = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Id == tenantId, cancellationToken);

        if (!tenantExists)
        {
            return Results.NotFound(new { message = "Tenant introuvable pour la synchronisation des reglements." });
        }

        var payments = await dbContext.Payments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.SourceCommercialDocument)
            .Include(x => x.Allocations)
                .ThenInclude(x => x.CommercialDocument)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.PaymentDate)
            .ThenBy(x => x.ReferenceNumber)
            .Select(x => new OfflinePaymentSyncItem(
                x.PaymentDate,
                x.Direction.ToString(),
                x.Type.ToString(),
                x.Method.ToString(),
                x.AllocationStatus.ToString(),
                x.ReferenceNumber,
                x.CurrencyCode,
                x.Amount,
                x.AllocatedAmount,
                x.AvailableAmount,
                x.Notes,
                x.Partner != null ? x.Partner.Code : string.Empty,
                x.SourceCommercialDocument != null ? x.SourceCommercialDocument.Number : null,
                x.Allocations
                    .OrderBy(a => a.AllocatedOnUtc)
                    .Select(a => new OfflinePaymentAllocationSyncItem(
                        a.CommercialDocument != null ? a.CommercialDocument.Number : string.Empty,
                        a.AllocatedAmount,
                        a.AllocatedOnUtc,
                        a.Notes))
                    .ToList(),
                x.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new OfflinePaymentPullResponse(
            tenantId,
            string.IsNullOrWhiteSpace(nodeId) ? "unknown-node" : nodeId.Trim(),
            DateTime.UtcNow,
            payments));
    }

    private static TEnum ParseEnum<TEnum>(string rawValue, TEnum fallback)
        where TEnum : struct
        => Enum.TryParse<TEnum>(rawValue, true, out var parsed) ? parsed : fallback;
}
