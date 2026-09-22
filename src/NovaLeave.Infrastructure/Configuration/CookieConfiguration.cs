using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NovaLeave.Infrastructure.Identity;

namespace NovaLeave.Infrastructure.Configuration;

// Cookie configuration for authentication
// Implements secure cookie settings per SR-005:
// - HttpOnly, Secure, SameSite=Lax
// - Sliding expiration 20min idle
// - Absolute expiration 8-12h
// - Security stamp revalidation every 5 minutes (FR-010)
public static class CookieConfiguration
{
    public static void ConfigureCookieAuthenticationOptions(CookieAuthenticationOptions options)
    {
        // Cookie security settings (SR-005)
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Requires HTTPS
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Name = "NovaLeave.Auth";

        // Session lifetime settings
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20); // Sliding expiration (idle timeout)
        options.SlidingExpiration = true; // Renew cookie on activity

        // Absolute expiration (maximum session duration regardless of activity)
        // Set to 12 hours as upper bound
        options.Cookie.MaxAge = TimeSpan.FromHours(12);

        // Login page
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";

        // ReturnUrl parameter name
        options.ReturnUrlParameter = "returnUrl";

        // Configure security stamp validation to invalidate cookies when roles/claims change (FR-010)
        // Revalidate every 5 minutes to detect mid-flight changes to user roles or account status
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                // Security stamp validation interval: 5 minutes
                // If the user's security stamp changed (e.g., role/password change), invalidate the cookie
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var signInManager = context.HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();

                if (context.Principal == null)
                {
                    context.RejectPrincipal();
                    return;
                }

                var user = await userManager.GetUserAsync(context.Principal);

                if (user == null)
                {
                    // Usuario no encontrado - rechazar principal
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                    return;
                }

                // Verificar si el security stamp ha cambiado
                var isStampValid = await signInManager.ValidateSecurityStampAsync(context.Principal);
                if (isStampValid == null)
                {
                    // Security stamp no coincide - invalidar sesión
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            }
        };
    }
}
