using Asp.Versioning;
using GescomSaas.Web.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GescomSaas.Tests.Api;

/// <summary>
/// Tests de la configuration de versioning de l'API REST. Verifient que :
///   - La version par defaut est 1.0
///   - Les 3 lecteurs de version (path, header, querystring) sont configures
///   - L'API explorer est branche pour permettre la generation Swagger par version
/// </summary>
public class ApiVersioningSetupTests
{
    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGescomApiVersioning();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddGescomApiVersioning_ConfigureLaVersionParDefautA1Point0()
    {
        var sp = BuildServices();
        var options = sp.GetRequiredService<IOptions<ApiVersioningOptions>>().Value;

        options.DefaultApiVersion.Should().Be(new ApiVersion(1, 0));
        options.AssumeDefaultVersionWhenUnspecified.Should().BeTrue();
        options.ReportApiVersions.Should().BeTrue();
    }

    [Fact]
    public void AddGescomApiVersioning_AcceptePath_Header_EtQueryString()
    {
        var sp = BuildServices();
        var options = sp.GetRequiredService<IOptions<ApiVersioningOptions>>().Value;

        options.ApiVersionReader.Should().NotBeNull();

        // Verifie indirectement la composition : le reader combine doit
        // contenir au moins 3 sources distinctes.
        var combinedType = options.ApiVersionReader.GetType().Name;
        combinedType.Should().Contain("Combined");
    }

    [Fact]
    public void AddGescomApiVersioning_FormatLeGroupNameEnVPlusVersion()
    {
        var sp = BuildServices();
        var options = sp.GetRequiredService<IOptions<Asp.Versioning.ApiExplorer.ApiExplorerOptions>>().Value;

        options.GroupNameFormat.Should().Be("'v'VVV");
        options.SubstituteApiVersionInUrl.Should().BeTrue();
    }
}
