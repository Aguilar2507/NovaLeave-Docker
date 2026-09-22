using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace NovaLeave.Infrastructure.Identity;

// Fábrica de claims personalizada (SR-007, FR-012).
// El factory por defecto de Identity solo emite NameIdentifier/roles; el claim "EmployeeId"
// es la clave que usan controllers, policies y dashboards para resolver al Employee actual.
// Registrado con AddUserClaimsPrincipalFactory para que PasswordSignInAsync y la revalidación
// del security stamp (CookieConfiguration) incluyan siempre este claim en el principal.
public class ApplicationUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (user.EmployeeId.HasValue)
        {
            identity.AddClaim(new Claim("EmployeeId", user.EmployeeId.Value.ToString()));
        }

        return identity;
    }
}
