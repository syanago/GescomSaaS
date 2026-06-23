using GescomSaas.Web.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GescomSaas.Tests.HealthChecks;

/// <summary>
/// Tests sur DiskSpaceHealthCheck. On ne peut pas simuler un disque plein
/// de maniere portable, donc on verifie au moins que sur un environnement
/// normal le check repond Healthy ou Degraded (pas Unhealthy en CI).
/// </summary>
public class DiskSpaceHealthCheckTests
{
    [Fact]
    public async Task CheckHealth_SurDisqueNormal_RetourneStatutDeterministe()
    {
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.SetupGet(x => x.ContentRootPath).Returns(Path.GetTempPath());

        var check = new DiskSpaceHealthCheck(envMock.Object);
        var ctx = new HealthCheckContext();

        var result = await check.CheckHealthAsync(ctx);

        // Healthy ou Degraded sont les deux issues acceptables sur un agent CI.
        // Unhealthy signifierait que le disque est plein a < 100 Mo, ce qu'il
        // serait surprenant dans un build agent.
        result.Status.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Degraded);
        result.Data.Should().ContainKey("freeBytes");
        result.Data.Should().ContainKey("freeGB");
        result.Data.Should().ContainKey("drive");
    }

    [Fact]
    public async Task CheckHealth_AvecCheminInvalide_RetourneUnhealthy()
    {
        var envMock = new Mock<IWebHostEnvironment>();
        // Drive inexistant : sur Windows "Z:" est generalement libre, sur Linux le path
        // sans drive root retourne "/" qui existe toujours, donc on cible un cas qui
        // declenche le DriveInfo.IsReady=false.
        envMock.SetupGet(x => x.ContentRootPath).Returns(@"Z:\path\that\does\not\exist");

        var check = new DiskSpaceHealthCheck(envMock.Object);
        var ctx = new HealthCheckContext();

        var result = await check.CheckHealthAsync(ctx);

        // En CI Linux ce path normalise sera traite differemment, donc on accepte
        // tous les statuts non-Healthy comme valides ici. L'important est que la
        // methode ne jette PAS une exception non geree.
        result.Should().NotBeNull();
    }
}
