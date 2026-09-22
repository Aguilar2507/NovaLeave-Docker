using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Infrastructure.Identity;

// Development data seeder - only runs in Development environment
// Creates test users, roles, and employees for local testing
public class DevelopmentDataSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public DevelopmentDataSeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context,
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task SeedAsync()
    {
        // Create roles
        await EnsureRoleAsync("User");
        await EnsureRoleAsync("Approver");

        // Create test users with known passwords
        // Password for all test accounts: Test123!@#

        // 1. User-only account (Active)
        await CreateUserWithEmployeeAsync(
            email: "user@novaleave.local",
            password: "Test123!@#",
            name: "Juan Pérez",
            roles: new[] { "User" },
            balance: 15,
            status: AccountStatus.Active,
            assignedApproverId: null); // Will be set after approver is created

        // 2. Approver-only account (Active)
        var approverId = await CreateUserWithEmployeeAsync(
            email: "approver@novaleave.local",
            password: "Test123!@#",
            name: "María García",
            roles: new[] { "User", "Approver" },
            balance: 10, // Approvers typically don't have vacation balance
            status: AccountStatus.Active,
            assignedApproverId: null);

        // 3. Dual-role account (User + Approver, Active)
        await CreateUserWithEmployeeAsync(
            email: "manager@novaleave.local",
            password: "Test123!@#",
            name: "Carlos Rodríguez",
            roles: new[] { "User", "Approver" },
            balance: 20,
            status: AccountStatus.Active,
            assignedApproverId: approverId);

        // 4. Inactive account
        await CreateUserWithEmployeeAsync(
            email: "inactive@novaleave.local",
            password: "Test123!@#",
            name: "Ana Martínez",
            roles: new[] { "User" },
            balance: 10,
            status: AccountStatus.Inactive,
            assignedApproverId: approverId);

        // 5. Regular employee assigned to approver
        await CreateUserWithEmployeeAsync(
            email: "employee@novaleave.local",
            password: "Test123!@#",
            name: "Pedro López",
            roles: new[] { "User" },
            balance: 12,
            status: AccountStatus.Active,
            assignedApproverId: approverId);

        // Update first user to have the approver assigned
        var firstUser = await _context.Employees
            .FirstOrDefaultAsync(e => e.Email == "user@novaleave.local");
        if (firstUser != null && approverId.HasValue)
        {
            firstUser.SetAssignedApprover(approverId);
            await _context.SaveChangesAsync();
        }

        Console.WriteLine("✅ Development data seeded successfully");
        Console.WriteLine("📋 Test Accounts (Password: Test123!@#):");
        Console.WriteLine("   - user@novaleave.local (User, Active, 15 days)");
        Console.WriteLine("   - approver@novaleave.local (Approver, Active)");
        Console.WriteLine("   - manager@novaleave.local (User + Approver, Active, 20 days)");
        Console.WriteLine("   - inactive@novaleave.local (User, Inactive, 10 days)");
        Console.WriteLine("   - employee@novaleave.local (User, Active, 12 days)");
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    private async Task<Guid?> CreateUserWithEmployeeAsync(
        string email,
        string password,
        string name,
        string[] roles,
        int balance,
        AccountStatus status,
        Guid? assignedApproverId)
    {
        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            // User already seeded, return existing employee ID
            return existingUser.EmployeeId;
        }

        // Create Employee first
        var employee = new Employee(
            id: Guid.NewGuid(),
            identityId: string.Empty, // Will be updated after user creation
            name: name,
            email: email,
            employmentStartDate: DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime.AddYears(-2)),
            initialBalance: balance,
            assignedApproverId: assignedApproverId);

        // Save employee
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        // Apply inactive status if needed (constructor always sets Active)
        if (status == AccountStatus.Inactive)
        {
            employee.Deactivate();
            await _context.SaveChangesAsync();
        }

        // Create ApplicationUser
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            EmployeeId = employee.Id
        };

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        // Update Employee with IdentityId
        employee.UpdateIdentityId(user.Id);
        await _context.SaveChangesAsync();

        // Assign roles
        foreach (var role in roles)
        {
            await _userManager.AddToRoleAsync(user, role);
        }

        return employee.Id;
    }
}
