using Microsoft.AspNetCore.Identity;

namespace NovaLeave.Infrastructure.Configuration;

// Identity configuration for ASP.NET Core Identity
// Implements security requirements: lockout 5 attempts, 15-min duration (FR-015)
public static class IdentityConfiguration
{
    public static void ConfigureIdentityOptions(IdentityOptions options)
    {
        // Password settings (strong password policy)
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        // Lockout settings (FR-015: 5 intentos, 15 minutos de bloqueo)
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.RequireUniqueEmail = true;

        // Sign-in settings
        options.SignIn.RequireConfirmedAccount = false; // Para desarrollo
        options.SignIn.RequireConfirmedEmail = false;
    }
}
