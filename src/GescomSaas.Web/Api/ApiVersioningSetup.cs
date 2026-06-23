using Asp.Versioning;
using Asp.Versioning.Builder;

namespace GescomSaas.Web.Api;

/// <summary>
/// Centralise la configuration de versioning de l'API REST.
///
/// Strategie :
///   - Version par defaut : 1.0
///   - Version reportee dans le path : /api/v1/...
///   - Aussi acceptees : header "api-version" et querystring "?api-version=1.0"
///     (utile pour les clients SDK qui ne peuvent pas changer le path facilement)
///   - La version est exposee dans la reponse via le header "api-supported-versions"
///
/// Pour ajouter une v2 :
///   1. Augmenter ApiVersion dans HasApiVersion(...)
///   2. Mapper les endpoints sur le nouveau groupe /api/v2
///   3. Marquer les endpoints v1 obsoletes via .HasDeprecatedApiVersion(new ApiVersion(1, 0))
/// </summary>
public static class ApiVersioningSetup
{
    public static IServiceCollection AddGescomApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("api-version"),
                new QueryStringApiVersionReader("api-version"));
        })
        .AddApiExplorer(options =>
        {
            // Format pour les groupes Swagger : "v1", "v2", etc.
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    /// <summary>
    /// Cree un ApiVersionSet partage par tous les endpoints du domaine "Gescom".
    /// Permet de declarer des routes versionnees via .WithApiVersionSet(...).HasApiVersion(...).
    /// </summary>
    public static ApiVersionSet CreateGescomVersionSet(this IEndpointRouteBuilder app)
    {
        return app.NewApiVersionSet("Gescom")
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();
    }
}
