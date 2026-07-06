using GescomSaas.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Areas.Identity.Pages.Account;

// Logout personnalise : deconnecte l'utilisateur puis AFFICHE une page de
// confirmation contenant un lien "Se connecter" vers la page d'authentification.
[AllowAnonymous]
public class LogoutModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();
        return Page();
    }

    // Acces direct en GET : deconnecte aussi (si encore connecte) puis affiche la page.
    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            await signInManager.SignOutAsync();
        }

        return Page();
    }
}
