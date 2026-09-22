using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Identity;
using Xunit;

namespace NovaLeave.Presentation.Tests.Fixtures;

// Shared collection so all test classes that need AuthTestFixture
// reuse one WebApplicationFactory instance and avoid duplicate seeding
// on the same InMemory database.
[CollectionDefinition("AuthTests")]
public class AuthTestCollection : ICollectionFixture<AuthTestFixture>;

// WebApplicationFactory fixture for integration tests with authentication
// Provides in-memory database with test users and employees
public class AuthTestFixture : WebApplicationFactory<Program>
{
    // Test user credentials (deterministic for test repeatability)
    public const string TestUserEmail = "test@novaleaveconso.com";
    public const string TestUserPassword = "Test123!@#";
    public const string TestAdminEmail = "admin@novaleaveconso.com";
    public const string TestAdminPassword = "Admin123!@#";
    public const string TestApproverEmail = "approver@novaleaveconso.com";
    public const string TestApproverPassword = "Test123!@#";

    // Known GUIDs for test data
    public static readonly Guid TestEmployeeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid TestAdminEmployeeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid TestApproverEmployeeId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Testing so Program.cs uses InMemory database
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Build service provider and seed test data
            var sp = services.BuildServiceProvider();
            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<ApplicationDbContext>();
                var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();

                // Ensure database is created
                db.Database.EnsureCreated();

                // Seed test data synchronously (blocking is acceptable in test setup)
                SeedTestDataAsync(db, userManager, roleManager).GetAwaiter().GetResult();
            }
        });
    }

    // Seed test users and employees
    private static async Task SeedTestDataAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // Skip if already seeded
        if (await db.Employees.AnyAsync())
        {
            return;
        }

        // Create roles
        if (!await roleManager.RoleExistsAsync("Employee"))
        {
            await roleManager.CreateAsync(new IdentityRole("Employee"));
        }
        if (!await roleManager.RoleExistsAsync("Approver"))
        {
            await roleManager.CreateAsync(new IdentityRole("Approver"));
        }
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        // Create test Employee (regular user)
        var testEmployee = new Employee(
            id: TestEmployeeId,
            identityId: "", // Will be set after Identity user creation
            name: "Test User",
            email: TestUserEmail,
            employmentStartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
            initialBalance: 20,
            assignedApproverId: null);

        // Create test Admin Employee
        var testAdminEmployee = new Employee(
            id: TestAdminEmployeeId,
            identityId: "", // Will be set after Identity user creation
            name: "Admin User",
            email: TestAdminEmail,
            employmentStartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-5)),
            initialBalance: 25,
            assignedApproverId: null);

        // Create test Approver-only Employee
        var testApproverEmployee = new Employee(
            id: TestApproverEmployeeId,
            identityId: "", // Will be set after Identity user creation
            name: "Approver User",
            email: TestApproverEmail,
            employmentStartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-3)),
            initialBalance: 22,
            assignedApproverId: null);

        // Create Identity users
        var testUser = new ApplicationUser
        {
            UserName = TestUserEmail,
            Email = TestUserEmail,
            EmailConfirmed = true,
            EmployeeId = TestEmployeeId
        };

        var testAdmin = new ApplicationUser
        {
            UserName = TestAdminEmail,
            Email = TestAdminEmail,
            EmailConfirmed = true,
            EmployeeId = TestAdminEmployeeId
        };

        var testApprover = new ApplicationUser
        {
            UserName = TestApproverEmail,
            Email = TestApproverEmail,
            EmailConfirmed = true,
            EmployeeId = TestApproverEmployeeId
        };

        // Create users with passwords
        var testUserResult = await userManager.CreateAsync(testUser, TestUserPassword);
        if (!testUserResult.Succeeded)
        {
            throw new InvalidOperationException("Failed to create test user");
        }

        var testAdminResult = await userManager.CreateAsync(testAdmin, TestAdminPassword);
        if (!testAdminResult.Succeeded)
        {
            throw new InvalidOperationException("Failed to create test admin");
        }

        var testApproverResult = await userManager.CreateAsync(testApprover, TestApproverPassword);
        if (!testApproverResult.Succeeded)
        {
            throw new InvalidOperationException("Failed to create test approver");
        }

        // Assign roles
        await userManager.AddToRoleAsync(testUser, "Employee");
        await userManager.AddToRoleAsync(testAdmin, "Admin");
        await userManager.AddToRoleAsync(testApprover, "Approver"); // Only Approver role, no Employee

        // Update Employee IdentityId references
        testEmployee = new Employee(
            id: testEmployee.Id,
            identityId: testUser.Id,
            name: testEmployee.Name,
            email: testEmployee.Email,
            employmentStartDate: testEmployee.EmploymentStartDate,
            initialBalance: testEmployee.Balance,
            assignedApproverId: testEmployee.AssignedApproverId);

        testAdminEmployee = new Employee(
            id: testAdminEmployee.Id,
            identityId: testAdmin.Id,
            name: testAdminEmployee.Name,
            email: testAdminEmployee.Email,
            employmentStartDate: testAdminEmployee.EmploymentStartDate,
            initialBalance: testAdminEmployee.Balance,
            assignedApproverId: testAdminEmployee.AssignedApproverId);

        testApproverEmployee = new Employee(
            id: testApproverEmployee.Id,
            identityId: testApprover.Id,
            name: testApproverEmployee.Name,
            email: testApproverEmployee.Email,
            employmentStartDate: testApproverEmployee.EmploymentStartDate,
            initialBalance: testApproverEmployee.Balance,
            assignedApproverId: testApproverEmployee.AssignedApproverId);

        // Save Employees to database
        db.Employees.Add(testEmployee);
        db.Employees.Add(testAdminEmployee);
        db.Employees.Add(testApproverEmployee);
        await db.SaveChangesAsync();
    }
}
