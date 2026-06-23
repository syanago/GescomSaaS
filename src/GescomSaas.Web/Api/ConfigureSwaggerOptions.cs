using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GescomSaas.Web.Api;

/// <summary>
/// Genere un SwaggerDoc par version d'API decouverte par l'ApiExplorer.
/// Resultat : ajouter une v2 ne demande aucun changement dans Program.cs,
/// il suffit de declarer .HasApiVersion(new ApiVersion(2, 0)) sur le groupe
/// et le doc "v2" apparait automatiquement dans Swagger UI.
/// </summary>
public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, BuildOpenApiInfo(description));
        }
    }

    private static OpenApiInfo BuildOpenApiInfo(ApiVersionDescription description)
    {
        var info = new OpenApiInfo
        {
            Title = "LigCom API",
            Version = description.ApiVersion.ToString(),
            Description = "API REST SaaS de gestion commerciale inspiree de Sage Gescom 100.",
        };

        if (description.IsDeprecated)
        {
            info.Description += " ⚠ Cette version est depreciee et sera retiree.";
        }

        return info;
    }
}
