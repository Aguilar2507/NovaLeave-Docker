using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Identity;
using NovaLeave.Presentation.Tests.Fixtures;
using Xunit;

namespace NovaLeave.Presentation.Tests.Authentication;

// Verifica que el claims factory personalizado emite el claim "EmployeeId" en el principal
// (SR-007/FR-012). Sin él, controllers, policies y dashboards no pueden resolver al Employee
// actual: GetEmployeeIdFromClaims() devuelve Guid.Empty y el balance no se carga.
// Usa usuarios/empleados propios con emails únicos para no depender del seed compartido
// de AuthTestFixture (la InMemory DB se comparte entre colecciones de tests).
[Collection("AuthTests")]
public class ClaimsPrincipalFactoryTests
{
    private readonly AuthTestFixture _fixture;

    public ClaimsPrincipalFactoryTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GenerateClaimsAsync_AddsEmployeeIdClaim()
    {
        using var scope = _fixture.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();

        var (user, employee) = await CreateUserWithEmployeeAsync(userManager, scope);

        var principal = await factory.CreateAsync(user);

        var employeeIdClaim = principal.FindFirst("EmployeeId");
        Assert.NotNull(employeeIdClaim);
        Assert.Equal(employee.Id.ToString(), employeeIdClaim.Value);
    }

    [Fact]
    public async Task EmployeeIdClaim_ResolvesToEmployeeWithBalance()
    {
        using var scope = _fixture.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var (user, employee) = await CreateUserWithEmployeeAsync(userManager, scope);

        var principal = await factory.CreateAsync(user);

        var claim = principal.FindFirst("EmployeeId");
        Assert.NotNull(claim);
        Assert.Equal(employee.Id.ToString(), claim.Value);

        var reloaded = await db.Employees.FindAsync(employee.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(employee.Balance, reloaded!.Balance);
        Assert.True(reloaded.Balance > 0, "El empleado de prueba debería tener balance > 0 para validar la carga.");
    }

    private static async Task<(ApplicationUser User, Employee Employee)> CreateUserWithEmployeeAsync(
        UserManager<ApplicationUser> userManager,
        IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = $"claims-{Guid.NewGuid():N}@novaleave.test";

        var employee = new Employee(
            id: Guid.NewGuid(),
            identityId: string.Empty,
            name: "Claims Factory Test",
            email: email,
            employmentStartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
            initialBalance: 20);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            EmployeeId = employee.Id
        };

        var createResult = await userManager.CreateAsync(user, "Test123!@#");
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        employee.UpdateIdentityId(user.Id);
        await db.SaveChangesAsync();

        return (user, employee);
    }
}
