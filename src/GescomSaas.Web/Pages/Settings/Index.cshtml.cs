using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Pages.Settings;

[Authorize(Roles = "TenantOwner,PlatformAdmin")]
public class IndexModel : PageModel
{
}
