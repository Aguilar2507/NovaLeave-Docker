using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Identity;
using NovaLeave.Presentation.Tests.Fixtures;
using Xunit;

namespace NovaLeave.Presentation.Tests.Authentication;

// Tests de integración para expiración de sesión y revalidación
// Verifica idle timeout, absolute lifetime, y invalidación mid-flight por cambios de estado/rol
public class SessionExpirationTests
{
    // T046: Idle timeout (20min) redirige a Login con ReturnUrl
    [Fact]
    public async Task IdleTimeout_ExceedsExpiration_RedirectsToLoginWithReturnUrl()
    {
        // Arrange - Cliente con TimeProvider fake
        var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Reemplazar TimeProvider con el fake
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TimeProvider));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<TimeProvider>(fakeTimeProvider);
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // No seguir redirects automáticamente
        });

        // Crear usuario y empleado de prueba
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Limpiar datos de prueba anteriores para este test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var testUser = new ApplicationUser
        {
            UserName = "test_idle@novalabs.com",
            Email = "test_idle@novalabs.com",
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(testUser, "Test@123");
        Assert.True(createResult.Succeeded);

        var employee = new Employee(
            Guid.NewGuid(),
            testUser.Id,
            "Test User Idle",
            "test_idle@novalabs.com",
            DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
            initialBalance: 20
        );
        testUser.EmployeeId = employee.Id;
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();
        await userManager.UpdateAsync(testUser);

        // Act - Autenticar usuario a través de la página de login
        var loginGetResponse = await client.GetAsync("/Account/Login");
        var loginContent = await loginGetResponse.Content.ReadAsStringAsync();

        // Extraer token antiforgery
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            loginContent, 
            @"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(tokenMatch.Success, "Antiforgery token not found");
        var token = tokenMatch.Groups[1].Value;

        var loginResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", "test_idle@novalabs.com"),
            new KeyValuePair<string, string>("Password", "Test@123"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        }));

        // Verificar login exitoso (redirect a dashboard u otra página)
        Assert.True(
            loginResponse.StatusCode == System.Net.HttpStatusCode.Redirect || 
            loginResponse.IsSuccessStatusCode,
            $"Login failed with status {loginResponse.StatusCode}");

        // Avanzar tiempo 21 minutos (excede idle timeout de 20 min)
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(21));

        // Intentar acceder a página protegida
        var protectedResponse = await client.GetAsync("/Employee/Dashboard");

        // Assert - Debe redirigir a Login con ReturnUrl
        Assert.Equal(System.Net.HttpStatusCode.Redirect, protectedResponse.StatusCode);
        var location = protectedResponse.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("/Account/Login", location);
        Assert.Contains("returnUrl", location, StringComparison.OrdinalIgnoreCase);

        factory.Dispose();
    }

    // T047: Absolute lifetime excedido redirige a Login
    [Fact]
    public async Task AbsoluteLifetime_Exceeded_RedirectsToLogin()
    {
        // Arrange - Cliente con TimeProvider fake
        var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Reemplazar TimeProvider con el fake
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TimeProvider));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<TimeProvider>(fakeTimeProvider);
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Crear usuario y empleado de prueba
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var testUser = new ApplicationUser
        {
            UserName = "test_absolute@novalabs.com",
            Email = "test_absolute@novalabs.com",
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(testUser, "Test@123");
        Assert.True(createResult.Succeeded);

        var employee = new Employee(
            Guid.NewGuid(),
            testUser.Id,
            "Test User Absolute",
            "test_absolute@novalabs.com",
            DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
            initialBalance: 20
        );
        testUser.EmployeeId = employee.Id;
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();
        await userManager.UpdateAsync(testUser);

        // Act - Autenticar usuario
        var loginGetResponse = await client.GetAsync("/Account/Login");
        var loginContent = await loginGetResponse.Content.ReadAsStringAsync();
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            loginContent, 
            @"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(tokenMatch.Success);
        var token = tokenMatch.Groups[1].Value;

        var loginResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", "test_absolute@novalabs.com"),
            new KeyValuePair<string, string>("Password", "Test@123"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        }));

        Assert.True(
            loginResponse.StatusCode == System.Net.HttpStatusCode.Redirect || 
            loginResponse.IsSuccessStatusCode);

        // Avanzar tiempo 12h + 1min (excede absolute lifetime de 12h)
        fakeTimeProvider.Advance(TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(1)));

        // Intentar acceder a página protegida
        var protectedResponse = await client.GetAsync("/Employee/Dashboard");

        // Assert - Debe redirigir a Login
        Assert.Equal(System.Net.HttpStatusCode.Redirect, protectedResponse.StatusCode);
        var location = protectedResponse.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("/Account/Login", location);

        factory.Dispose();
    }

    // T048: Account status cambia a Inactive durante sesión → operación denegada (FR-011)
    [Fact]
    public async Task AccountInactive_DuringSession_DeniesStateChangingOperation()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Crear usuario y empleado de prueba
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var testUser = new ApplicationUser
        {
            UserName = "test_inactive@novalabs.com",
            Email = "test_inactive@novalabs.com",
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(testUser, "Test@123");
        Assert.True(createResult.Succeeded);

        var employee = new Employee(
            Guid.NewGuid(),
            testUser.Id,
            "Test User Inactive",
            "test_inactive@novalabs.com",
            DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
            initialBalance: 20
        );
        testUser.EmployeeId = employee.Id;
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();
        await userManager.UpdateAsync(testUser);

        // Act - Autenticar usuario
        var loginGetResponse = await client.GetAsync("/Account/Login");
        var loginContent = await loginGetResponse.Content.ReadAsStringAsync();
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            loginContent, 
            @"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(tokenMatch.Success);
        var token = tokenMatch.Groups[1].Value;

        var loginResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", "test_inactive@novalabs.com"),
            new KeyValuePair<string, string>("Password", "Test@123"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        }));

        Assert.True(
            loginResponse.StatusCode == System.Net.HttpStatusCode.Redirect || 
            loginResponse.IsSuccessStatusCode);

        // Cambiar Employee.Status a Inactive durante la sesión activa
        var employeeToUpdate = await dbContext.Employees.FindAsync(employee.Id);
        Assert.NotNull(employeeToUpdate);
        employeeToUpdate.Deactivate(); // Método que cambia Status a Inactive
        await dbContext.SaveChangesAsync();

        // Intentar acceder a página protegida (GET) o hacer operación POST
        // El filtro ValidateAccountStatusFilter debe interceptar y redirigir/denegar
        var protectedResponse = await client.GetAsync("/Employee/Dashboard");

        // Assert - Debe redirigir a Login o retornar Forbidden/Redirect
        // Por ahora, verificamos que NO retorna Success (200) para cuenta inactiva
        Assert.True(
            protectedResponse.StatusCode == System.Net.HttpStatusCode.Redirect ||
            protectedResponse.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            protectedResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized,
            $"Expected redirect/forbidden, got {protectedResponse.StatusCode}");

        factory.Dispose();
    }

    // T049: Rol removido durante sesión → security stamp invalida cookie (FR-010)
    [Fact]
    public async Task RoleChanged_DuringSession_InvalidatesCookie()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Reemplazar TimeProvider con el fake
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TimeProvider));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<TimeProvider>(fakeTimeProvider);
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Crear usuario y empleado de prueba con rol
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Crear rol si no existe
        if (!await roleManager.RoleExistsAsync("Employee"))
        {
            await roleManager.CreateAsync(new IdentityRole("Employee"));
        }

        var testUser = new ApplicationUser
        {
            UserName = "test_role@novalabs.com",
            Email = "test_role@novalabs.com",
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(testUser, "Test@123");
        Assert.True(createResult.Succeeded);

        await userManager.AddToRoleAsync(testUser, "Employee");

        var employee = new Employee(
            Guid.NewGuid(),
            testUser.Id,
            "Test User Role",
            "test_role@novalabs.com",
            DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
            initialBalance: 20
        );
        testUser.EmployeeId = employee.Id;
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();
        await userManager.UpdateAsync(testUser);

        // Act - Autenticar usuario
        var loginGetResponse = await client.GetAsync("/Account/Login");
        var loginContent = await loginGetResponse.Content.ReadAsStringAsync();
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            loginContent, 
            @"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(tokenMatch.Success);
        var token = tokenMatch.Groups[1].Value;

        var loginResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", "test_role@novalabs.com"),
            new KeyValuePair<string, string>("Password", "Test@123"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        }));

        Assert.True(
            loginResponse.StatusCode == System.Net.HttpStatusCode.Redirect || 
            loginResponse.IsSuccessStatusCode);

        // Remover rol del usuario y actualizar security stamp
        var userToUpdate = await userManager.FindByEmailAsync("test_role@novalabs.com");
        Assert.NotNull(userToUpdate);
        await userManager.RemoveFromRoleAsync(userToUpdate, "Employee");
        await userManager.UpdateSecurityStampAsync(userToUpdate);

        // Avanzar tiempo 6 minutos (excede intervalo de revalidación de 5 min)
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(6));

        // Intentar acceder a página protegida
        var protectedResponse = await client.GetAsync("/Employee/Dashboard");

        // Assert - Debe redirigir a Login (cookie invalidada por cambio de security stamp)
        Assert.Equal(System.Net.HttpStatusCode.Redirect, protectedResponse.StatusCode);
        var location = protectedResponse.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("/Account/Login", location);

        factory.Dispose();
    }
}
