using Microsoft.AspNetCore.Identity;

namespace GescomSaas.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public Guid? TenantId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string DisplayName => string.Join(' ', new[] { FirstName, LastName }.Where(static value => !string.IsNullOrWhiteSpace(value)));
}
