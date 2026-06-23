using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Pages.Settings;

/// <summary>
/// Préférences locales d'apparence et d'affichage. Tout est stocké côté client
/// (localStorage) — pas de persistance serveur, pas de permission tenant requise.
/// </summary>
[Authorize]
public class AppearanceModel : PageModel
{
}
