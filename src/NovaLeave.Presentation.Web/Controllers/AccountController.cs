using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NovaLeave.Application.Features.Authentication.Login;
using NovaLeave.Application.Features.Authentication.Logout;
using NovaLeave.Infrastructure.Identity;
using NovaLeave.Presentation.Web.ViewModels;

namespace NovaLeave.Presentation.Web.Controllers;

// Account controller for authentication operations
// Login uses MVC controller to leverage rate limiting and antiforgery
public class AccountController : Controller
{
    private readonly LoginCommandHandler _loginHandler;
    private readonly LogoutCommandHandler _logoutHandler;

    public AccountController(
        LoginCommandHandler loginHandler,
        LogoutCommandHandler logoutHandler)
    {
        _loginHandler = loginHandler;
        _logoutHandler = logoutHandler;
    }

    // GET: /Account/Login
    // FR-014: Redirect if already authenticated
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        // If user is already authenticated, redirect to dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            var redirectUrl = DetermineAuthenticatedUserRedirect();
            return Redirect(redirectUrl);
        }

        var model = new LoginViewModel
        {
            ReturnUrl = returnUrl
        };

        return View(model);
    }

    // POST: /Account/Login
    // FR-006, SR-006: Rate limiting applied via middleware
    // SR-004: Antiforgery token validated automatically by global filter
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("LoginRateLimit")]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        CancellationToken cancellationToken)
    {
        // Model validation (client-side already applied)
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Execute login command
        var command = new LoginCommand(
            Email: model.Email,
            Password: model.Password,
            ReturnUrl: model.ReturnUrl);

        var result = await _loginHandler.HandleAsync(command, cancellationToken);

        if (!result.Success)
        {
            // Re-render form with generic error (FR-016, SR-007)
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Invalid email or password.");
            return View(model);
        }

        // Success: redirect to dashboard (FR-012, FR-013)
        return Redirect(result.RedirectUrl ?? "/Employee/Dashboard");
    }

    // GET: /Account/Logout
    [HttpGet]
    [Authorize]
    public IActionResult Logout()
    {
        // Render logout confirmation page
        return View();
    }

    // POST: /Account/Logout
    // SR-001, SR-004: Antiforgery enforced by global filter
    // Patrón PRG: POST → SignOut → Redirect to Login (FR-009)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        // Get current user ID from claims
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            // User not authenticated, just redirect to login
            return RedirectToAction(nameof(Login));
        }

        // Execute logout command
        var command = new LogoutCommand();
        await _logoutHandler.HandleAsync(command, userId, cancellationToken);

        // PRG pattern: redirect to login page
        return RedirectToAction(nameof(Login));
    }

    // Determines redirect URL for already-authenticated user
    // Same logic as LoginCommandHandler but without DB access
    private string DetermineAuthenticatedUserRedirect()
    {
        var isApprover = User.IsInRole("Approver");
        var isEmployee = User.IsInRole("Employee");

        // Approver-only user → Approver Dashboard
        if (isApprover && !isEmployee)
        {
            return "/Approver/Dashboard";
        }

        // Default: Employee Dashboard
        return "/Employee/Dashboard";
    }
}
