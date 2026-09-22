using Microsoft.AspNetCore.Mvc;

namespace NovaLeave.Presentation.Web.Controllers;

// HomeController - serves the 404 Not Found page (spec_003 §3.9).
// Unmatched routes are re-executed here by UseStatusCodePagesWithReExecute in Program.cs.
// Uses the unauthenticated shared layout (no sidebar) to avoid leaking path existence (SR-003).
public class HomeController : Controller
{
    // GET: /Home/Error (re-executed by status code middleware for HTTP 404)
    [Route("/Home/Error")]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View();
    }
}
