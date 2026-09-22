using Microsoft.AspNetCore.Identity;
using NovaLeave.Application.Features.Authentication.Contracts;
using NovaLeave.Application.Features.Authentication.Login;

namespace NovaLeave.Infrastructure.Identity;

// Result type for login command
public sealed record LoginResult(
    bool Success,
    string? ErrorMessage = null,
    string? RedirectUrl = null);

// Handler for login command
// Orchestrates Identity sign-in, status check, audit, role resolution (FR-012, FR-013)
public sealed class LoginCommandHandler
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountStatusValidator _statusValidator;
    private readonly IAuthenticationAuditService _auditService;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

    public LoginCommandHandler(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAccountStatusValidator statusValidator,
        IAuthenticationAuditService auditService,
        IPasswordHasher<ApplicationUser> passwordHasher)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _statusValidator = statusValidator;
        _auditService = auditService;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResult> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        const string GenericError = "Invalid email or password.";

        // Find user by email
        var user = await _userManager.FindByEmailAsync(command.Email);

        // Timing attack mitigation (T033b): execute dummy hash verification if user not found
        // to equalize response time with valid-email-wrong-password path
        if (user == null)
        {
            _passwordHasher.HashPassword(new ApplicationUser(), command.Password);

            await _auditService.LogLoginFailureAsync(
                email: command.Email,
                reason: "UnknownEmail",
                cancellationToken: cancellationToken);

            return new LoginResult(false, GenericError);
        }

        // Check if account is locked (FR-015)
        if (await _userManager.IsLockedOutAsync(user))
        {
            await _auditService.LogAccountLockedAsync(
                userId: user.Id,
                email: user.Email!,
                cancellationToken: cancellationToken);

            return new LoginResult(false, GenericError);
        }

        // Check if account is active (FR-003)
        var isActive = await _statusValidator.IsAccountActiveAsync(
            user.Id,
            cancellationToken);

        if (!isActive)
        {
            await _auditService.LogLoginFailureAsync(
                email: user.Email!,
                userId: user.Id,
                reason: "InactiveAccount",
                cancellationToken: cancellationToken);

            return new LoginResult(false, GenericError);
        }

        // Attempt password sign-in
        var result = await _signInManager.PasswordSignInAsync(
            user,
            command.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                await _auditService.LogAccountLockedAsync(
                    userId: user.Id,
                    email: user.Email!,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await _auditService.LogLoginFailureAsync(
                    email: user.Email!,
                    userId: user.Id,
                    reason: "InvalidPassword",
                    cancellationToken: cancellationToken);
            }

            return new LoginResult(false, GenericError);
        }

        // Login successful - audit and determine redirect
        var roles = await _userManager.GetRolesAsync(user);
        var role = string.Join(",", roles);

        await _auditService.LogLoginSuccessAsync(
            userId: user.Id,
            email: user.Email!,
            role: role,
            cancellationToken: cancellationToken);

        // Determine redirect URL (FR-012, FR-013, D-007)
        var redirectUrl = DetermineRedirectUrl(command.ReturnUrl, roles);

        return new LoginResult(true, null, redirectUrl);
    }

    // Determines the redirect URL after successful login
    // Priority: valid ReturnUrl > role-based default dashboard
    // FR-012: ReturnUrl support
    // FR-013: Role-based redirection
    // D-007: Approver-only users redirect to Approver Dashboard
    private string DetermineRedirectUrl(string? returnUrl, IList<string> roles)
    {
        // Validate ReturnUrl (FR-020: reject external URLs)
        if (!string.IsNullOrWhiteSpace(returnUrl) && IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        // Role-based redirection
        // If user has ONLY Approver role (not Employee), redirect to Approver Dashboard
        if (roles.Contains("Approver") && !roles.Contains("Employee"))
        {
            return "/Approver/Dashboard";
        }

        // Default: Employee Dashboard
        return "/Employee/Dashboard";
    }

    // Validates that a URL is local (not external)
    // Prevents open redirect attacks (SR-014)
    private bool IsLocalUrl(string url)
    {
        return !string.IsNullOrEmpty(url)
            && url.StartsWith('/')
            && !url.StartsWith("//")
            && !url.StartsWith("/\\");
    }
}
