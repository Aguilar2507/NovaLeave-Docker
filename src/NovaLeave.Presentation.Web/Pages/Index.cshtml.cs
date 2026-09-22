using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NovaLeave.Pages
{
    public class IndexModel : PageModel
    {
        // Redirect root path to login page
        // Users should land on /Account/Login by default
        public IActionResult OnGet()
        {
            return RedirectToAction("Login", "Account");
        }
    }
}
