using System.ComponentModel.DataAnnotations;
using GescomSaas.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Areas.Identity.Pages.Account;

// Page de connexion personnalisee (remplace l'UI Identity par defaut) : entierement
// en francais, avec bascule d'affichage du mot de passe et sans inscription /
// renvoi de confirmation.
[AllowAnonymous]
public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "L'identifiant est obligatoire.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Se souvenir ?")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(
            Input.Email,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            logger.LogInformation("Utilisateur connecte.");
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            return RedirectToPage("./Lockout");
        }

        ModelState.AddModelError(string.Empty, "Identifiant ou mot de passe incorrect.");
        return Page();
    }
}
