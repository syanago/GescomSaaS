using System.ComponentModel.DataAnnotations;
using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Pages.Invitations;

[AllowAnonymous]
public class AcceptModel(IPlatformUserAdministrationService platformUserAdministrationService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = string.Empty;

    [BindProperty]
    public AcceptInvitationInput Input { get; set; } = new();

    public InvitationAcceptanceContext? Invitation { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var loadResult = await LoadAsync();
        if (loadResult is not PageResult)
        {
            return loadResult;
        }

        if (Invitation is null)
        {
            ErrorMessage = "Invitation introuvable.";
            return Page();
        }

        if (Invitation.Status != UserInvitationStatus.Pending)
        {
            ErrorMessage = Invitation.Status switch
            {
                UserInvitationStatus.Accepted => "Cette invitation a deja ete utilisee.",
                UserInvitationStatus.Cancelled => "Cette invitation a ete annulee.",
                UserInvitationStatus.Expired => "Cette invitation a expire.",
                _ => "Cette invitation n'est plus utilisable."
            };
            return Page();
        }

        if (Invitation.RequiresPassword)
        {
            if (string.IsNullOrWhiteSpace(Input.Password))
            {
                ModelState.AddModelError("Input.Password", "Le mot de passe est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(Input.ConfirmPassword))
            {
                ModelState.AddModelError("Input.ConfirmPassword", "Confirmez le mot de passe.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await platformUserAdministrationService.AcceptInvitationAsync(
                Token,
                new InvitationAcceptanceRequest(Input.FirstName, Input.LastName, Input.Password),
                HttpContext.RequestAborted);

            StatusMessage = "Invitation acceptee. Connectez-vous avec votre compte.";
            return Redirect("/Identity/Account/Login");
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
            return Page();
        }
    }

    private async Task<IActionResult> LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            ErrorMessage = "Le lien d'invitation est incomplet.";
            return Page();
        }

        Invitation = await platformUserAdministrationService.GetInvitationAsync(Token, HttpContext.RequestAborted);
        if (Invitation is null)
        {
            ErrorMessage = "Invitation introuvable.";
            return Page();
        }

        if (!Request.HasFormContentType)
        {
            Input = new AcceptInvitationInput
            {
                FirstName = Invitation.FirstName ?? string.Empty,
                LastName = Invitation.LastName ?? string.Empty
            };
        }

        return Page();
    }
}

public sealed class AcceptInvitationInput
{
    [Required]
    [StringLength(80)]
    [Display(Name = "Prenom")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    [Display(Name = "Nom")]
    public string LastName { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    [Display(Name = "Confirmation")]
    public string? ConfirmPassword { get; set; }
}
