using GescomSaas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GescomSaas.Web.HealthChecks;

/// <summary>
/// Verifie que l'infrastructure Identity est operationnelle :
///   - Le store des roles repond
///   - Au moins un role est present (sinon le seed n'a pas tourne, l'app ne peut servir personne)
///
/// On evite tout test sur les utilisateurs ici pour ne pas charger N rows
/// sur un endpoint appele tres souvent par k8s / load balancer.
/// </summary>
public sealed class IdentityHealthCheck : IHealthCheck
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityHealthCheck(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var rolesQuery = _roleManager.Roles;
            var hasAnyRole = rolesQuery is null
                ? false
                : await Task.Run(() => rolesQuery.Any(), cancellationToken);

            if (!hasAnyRole)
            {
                return HealthCheckResult.Degraded(
                    "Aucun role Identity n'est defini. L'application acceptera les requetes mais ne pourra pas authentifier les utilisateurs metier.");
            }

            return HealthCheckResult.Healthy("Identity operationnel.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Identity injoignable.", ex);
        }
    }
}
