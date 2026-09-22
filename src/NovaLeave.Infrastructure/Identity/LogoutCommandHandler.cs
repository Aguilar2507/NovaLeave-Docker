using Microsoft.AspNetCore.Identity;
using NovaLeave.Application.Features.Authentication.Contracts;
using NovaLeave.Application.Features.Authentication.Logout;

namespace NovaLeave.Infrastructure.Identity;

// Handler for logout command
// Orchestrates Identity sign-out and audit event recording (FR-009, D-009)
public sealed class LogoutCommandHandler
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthenticationAuditService _auditService;

    public LogoutCommandHandler(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuthenticationAuditService auditService)
    {
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task<LogoutResult> HandleAsync(
        LogoutCommand command,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Get user for audit details
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            // User not found, but still sign out to be safe
            await _signInManager.SignOutAsync();
            return new LogoutResult(Success: false, ErrorMessage: "User not found");
        }

        // Sign out user (invalidates authentication cookie)
        await _signInManager.SignOutAsync();

        // Record audit event
        await _auditService.LogLogoutAsync(
            userId: user.Id,
            email: user.Email!,
            cancellationToken: cancellationToken);

        return new LogoutResult(Success: true);
    }
}

// Result type for logout command
public sealed record LogoutResult(
    bool Success,
    string? ErrorMessage = null);
