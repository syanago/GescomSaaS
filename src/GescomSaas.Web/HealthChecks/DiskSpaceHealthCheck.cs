using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GescomSaas.Web.HealthChecks;

/// <summary>
/// Verifie qu'il reste assez d'espace disque pour les operations critiques :
///   - Ecriture des Data Protection keys (sinon les sessions cassent au reboot)
///   - Rotation des logs Serilog (sinon plus aucun log)
///   - Ecriture de la base SQLite LocalNode (mode offline)
///
/// Renvoie Degraded sous 1 Go libre, Unhealthy sous 100 Mo.
/// </summary>
public sealed class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly IWebHostEnvironment _environment;

    private const long DegradedThresholdBytes = 1_000_000_000L; // 1 Go
    private const long UnhealthyThresholdBytes = 100_000_000L;  // 100 Mo

    public DiskSpaceHealthCheck(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var path = _environment.ContentRootPath;
        var driveRoot = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(driveRoot))
        {
            return Task.FromResult(HealthCheckResult.Healthy("Chemin sans drive identifiable - skip."));
        }

        DriveInfo drive;
        try
        {
            drive = new DriveInfo(driveRoot);
            if (!drive.IsReady)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Drive {driveRoot} non disponible."));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Impossible de lire les infos du drive.", ex));
        }

        var freeBytes = drive.AvailableFreeSpace;
        var data = new Dictionary<string, object>
        {
            ["path"] = path,
            ["drive"] = drive.Name,
            ["freeBytes"] = freeBytes,
            ["freeGB"] = Math.Round(freeBytes / 1_000_000_000d, 2),
        };

        if (freeBytes < UnhealthyThresholdBytes)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Espace disque critique : {freeBytes / 1_000_000} Mo restants.",
                data: data));
        }

        if (freeBytes < DegradedThresholdBytes)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Espace disque faible : {freeBytes / 1_000_000} Mo restants.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Espace disque OK ({data["freeGB"]} Go).",
            data: data));
    }
}
