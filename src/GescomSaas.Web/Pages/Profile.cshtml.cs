using System.ComponentModel.DataAnnotations;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages;

[Authorize]
public class ProfileModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext dbContext) : PageModel
{
    [TempData]
    public string? StatusMessage { get; set; }

    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Initials { get; private set; } = "?";
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public string TenantName { get; private set; } = "—";

    [BindProperty]
    public ProfileInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound("Utilisateur introuvable.");
        }

        await PopulateAsync(user);
        Input.FirstName = user.FirstName;
        Input.LastName = user.LastName;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound("Utilisateur introuvable.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(user);
            return Page();
        }

        user.FirstName = Input.FirstName?.Trim() ?? string.Empty;
        user.LastName = Input.LastName?.Trim() ?? string.Empty;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await PopulateAsync(user);
            return Page();
        }

        await signInManager.RefreshSignInAsync(user);
        StatusMessage = "Votre profil a ete mis a jour.";
        return RedirectToPage();
    }

    private async Task PopulateAsync(ApplicationUser user)
    {
        Email = user.Email ?? user.UserName ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? Email : user.DisplayName;
        Initials = BuildInitials(user.FirstName, user.LastName, Email);
        Roles = (await userManager.GetRolesAsync(user)).OrderBy(x => x).ToArray();

        if (user.TenantId.HasValue)
        {
            var name = await dbContext.Tenants
                .AsNoTracking()
                .Where(x => x.Id == user.TenantId.Value)
                .Select(x => x.CompanyName)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);
            if (!string.IsNullOrWhiteSpace(name))
            {
                TenantName = name;
            }
        }
        else
        {
            TenantName = "Plateforme (aucun tenant)";
        }
    }

    private static string BuildInitials(string first, string last, string email)
    {
        var a = !string.IsNullOrWhiteSpace(first) ? first[0].ToString() : string.Empty;
        var b = !string.IsNullOrWhiteSpace(last) ? last[0].ToString() : string.Empty;
        var initials = (a + b).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(initials) && !string.IsNullOrWhiteSpace(email))
        {
            initials = email[0].ToString().ToUpperInvariant();
        }

        return string.IsNullOrWhiteSpace(initials) ? "?" : initials;
    }

    public sealed class ProfileInput
    {
        [Display(Name = "Prenom")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Nom")]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;
    }
}
