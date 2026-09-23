using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Infrastructure.Identity;

// Development data seeder - only runs in Development environment
// Creates test users, roles, and employees for local testing
public class DevelopmentDataSeeder : IDevelopmentDataSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IWorkingDayCalculator _workingDayCalculator;

    public DevelopmentDataSeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context,
        TimeProvider timeProvider,
        IWorkingDayCalculator workingDayCalculator)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _timeProvider = timeProvider;
        _workingDayCalculator = workingDayCalculator;
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

        var seededRequests = await SeedVacationRequestsAsync();

        Console.WriteLine("✅ Development data seeded successfully");
        Console.WriteLine("📋 Test Accounts (Password: Test123!@#):");
        Console.WriteLine("   - user@novaleave.local (User, Active, 15 days)");
        Console.WriteLine("   - approver@novaleave.local (Approver, Active)");
        Console.WriteLine("   - manager@novaleave.local (User + Approver, Active, 20 days)");
        Console.WriteLine("   - inactive@novaleave.local (User, Inactive, 10 days)");
        Console.WriteLine("   - employee@novaleave.local (User, Active, 12 days)");
        Console.WriteLine($"🗓  Vacation requests seeded: {seededRequests}");
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

    // ---- Vacation request seeding (spec_008) -------------------------------------------
    //
    // Seeds 13 requests across all six statuses so the application has a realistic starting
    // state: approver queues are non-empty, history views have content, and the Grafana
    // business panels have something to show. Before this, a fresh database had 0 requests and
    // "Requests awaiting approval" / "Oldest request still waiting" rendered blank.
    //
    // Returns the number of requests created (0 when already seeded).
    private async Task<int> SeedVacationRequestsAsync()
    {
        // Idempotent: never top up a database that already has requests. Re-running would
        // otherwise double the fixtures and silently drain every balance.
        if (await _context.VacationRequests.AnyAsync())
        {
            return 0;
        }

        var employees = await _context.Employees.ToListAsync();
        var juan = employees.FirstOrDefault(e => e.Email == "user@novaleave.local");
        var pedro = employees.FirstOrDefault(e => e.Email == "employee@novaleave.local");
        var carlos = employees.FirstOrDefault(e => e.Email == "manager@novaleave.local");
        var maria = employees.FirstOrDefault(e => e.Email == "approver@novaleave.local");

        if (juan is null || pedro is null || carlos is null || maria is null)
        {
            return 0;
        }

        // Ana (inactive@) deliberately gets no requests: ReserveDays calls EnsureActiveAccount,
        // which throws for an Inactive employee. She exists to test rejected sign-in, and
        // giving her requests would mean bypassing the very invariant she is there to prove.
        var approver = maria.Id;
        var secondApprover = carlos.Id;
        var created = 0;

        // Juan — 15 days. Two pending (one deliberately old), one approved, one rejected, one cancelled.
        Pending(juan, workingDays: 3, startsInDays: 21, "Vacaciones familiares de verano", createdDaysAgo: 12);
        Pending(juan, workingDays: 5, startsInDays: 45, "Viaje planificado con antelación", createdDaysAgo: 5);
        Approved(juan, 4, 60, "Semana de descanso aprobada", approver, createdDaysAgo: 30, resolvedDaysAgo: 28);
        Rejected(juan, 2, 14, "Días sueltos", approver, "Coincide con el cierre trimestral", createdDaysAgo: 25, resolvedDaysAgo: 24);
        Cancelled(juan, 3, 35, "Escapada que finalmente no se hizo", createdDaysAgo: 20, resolvedDaysAgo: 18);

        // Pedro — 12 days. One pending, one approved, one expired, one voided.
        Pending(pedro, 2, 18, "Asuntos personales", createdDaysAgo: 2);
        Approved(pedro, 5, 70, "Vacaciones anuales", approver, createdDaysAgo: 40, resolvedDaysAgo: 37);
        Expired(pedro, 3, 28, "Solicitud sin respuesta del aprobador", createdDaysAgo: 50, resolvedDaysAgo: 20);
        Voided(pedro, 4, 90, "Aprobada y luego anulada por error administrativo", approver, "Aprobada por error: fechas incorrectas", createdDaysAgo: 45, resolvedDaysAgo: 15);

        // Carlos — 20 days. Approver who also requests time off, approved by María.
        Pending(carlos, 5, 25, "Vacaciones de fin de trimestre", createdDaysAgo: 1);
        Approved(carlos, 3, 50, "Puente aprobado", approver, createdDaysAgo: 22, resolvedDaysAgo: 21);
        Rejected(carlos, 4, 16, "Semana completa", approver, "Cobertura insuficiente del equipo en esas fechas", createdDaysAgo: 18, resolvedDaysAgo: 17);
        Cancelled(carlos, 2, 40, "Cancelada por cambio de planes", createdDaysAgo: 10, resolvedDaysAgo: 9);

        await _context.SaveChangesAsync();
        return created;

        // ---- local builders ----
        //
        // BALANCE SEMANTICS -- read before changing.
        // Each request costs its owner exactly ONE net charge while it holds days, and returns
        // them when it reaches a state that releases them. This deliberately does NOT replicate
        // what the running application does: CreateRequestHandler calls ReserveDays and
        // ApproveRequestHandler then calls DeductDays, and both methods are identical
        // (Balance -= days), so approving charges the employee twice. That is a defect in the
        // application, recorded in docs/Keep-in-mind.md -- not something fixture data should
        // bake in and make look intentional.

        void Track(VacationRequest request, int createdDaysAgo, int? resolvedDaysAgo)
        {
            _context.VacationRequests.Add(request);

            // CreatedAt is set to DateTimeOffset.UtcNow inside the constructor and has a private
            // setter, so backdating has to go through the EF entry. Without this every seeded
            // request is zero seconds old and the oldest-pending-age gauge reads ~0, which would
            // make a stalled queue look healthy.
            var entry = _context.Entry(request);
            var now = _timeProvider.GetUtcNow();
            entry.Property(r => r.CreatedAt).CurrentValue = now.AddDays(-createdDaysAgo);

            if (resolvedDaysAgo.HasValue)
            {
                entry.Property(r => r.ResolvedAt).CurrentValue = now.AddDays(-resolvedDaysAgo.Value);
            }

            created++;
        }

        VacationRequest Build(Employee owner, int workingDays, int startsInDays, string reason)
        {
            var start = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime).AddDays(startsInDays);
            var end = FindEndDate(start, workingDays);

            // Reserve first: the constructor validates invariants, and a request that cannot be
            // afforded should fail before it exists.
            owner.ReserveDays(workingDays);

            return new VacationRequest(
                id: Guid.NewGuid(),
                ownerId: owner.Id,
                startDate: start,
                endDate: end,
                reason: reason,
                requestedDays: workingDays,
                expiryDate: start.AddDays(30)); // Mirrors CreateRequestHandler's 30-day policy.
        }

        void Pending(Employee owner, int workingDays, int startsInDays, string reason, int createdDaysAgo)
            => Track(Build(owner, workingDays, startsInDays, reason), createdDaysAgo, null);

        void Approved(Employee owner, int workingDays, int startsInDays, string reason, Guid approverId, int createdDaysAgo, int resolvedDaysAgo)
        {
            var request = Build(owner, workingDays, startsInDays, reason);
            request.Approve(approverId);
            // No DeductDays here -- see BALANCE SEMANTICS above. The reservation IS the charge.
            Track(request, createdDaysAgo, resolvedDaysAgo);
        }

        void Rejected(Employee owner, int workingDays, int startsInDays, string reason, Guid approverId, string rejectionReason, int createdDaysAgo, int resolvedDaysAgo)
        {
            var request = Build(owner, workingDays, startsInDays, reason);
            request.Reject(approverId, rejectionReason);
            owner.RestoreDays(workingDays); // Rejected days go back, as RejectRequestHandler does.
            Track(request, createdDaysAgo, resolvedDaysAgo);
        }

        void Cancelled(Employee owner, int workingDays, int startsInDays, string reason, int createdDaysAgo, int resolvedDaysAgo)
        {
            var request = Build(owner, workingDays, startsInDays, reason);
            request.Cancel();
            owner.RestoreDays(workingDays);
            Track(request, createdDaysAgo, resolvedDaysAgo);
        }

        void Expired(Employee owner, int workingDays, int startsInDays, string reason, int createdDaysAgo, int resolvedDaysAgo)
        {
            var request = Build(owner, workingDays, startsInDays, reason);
            request.Expire();
            owner.RestoreDays(workingDays);
            Track(request, createdDaysAgo, resolvedDaysAgo);
        }

        void Voided(Employee owner, int workingDays, int startsInDays, string reason, Guid approverId, string voidReason, int createdDaysAgo, int resolvedDaysAgo)
        {
            var request = Build(owner, workingDays, startsInDays, reason);
            request.Approve(approverId);
            request.Void(approverId, voidReason);
            owner.RestoreDays(workingDays); // Matches VoidRequestHandler.
            Track(request, createdDaysAgo, resolvedDaysAgo);
        }
    }

    // Finds the end date that yields exactly `workingDays` working days from `start`, using the
    // application's own calculator so seeded RequestedDays always agrees with what the app would
    // compute for the same range (weekends and configured holidays excluded).
    private DateOnly FindEndDate(DateOnly start, int workingDays)
    {
        for (var offset = 1; offset <= 90; offset++)
        {
            var candidate = start.AddDays(offset);
            if (_workingDayCalculator.Count(start, candidate) == workingDays)
            {
                return candidate;
            }
        }

        // Unreachable for the small ranges seeded here; failing loudly beats emitting a request
        // whose RequestedDays contradicts its own date range.
        throw new InvalidOperationException(
            $"Could not find an end date giving {workingDays} working days from {start:O}.");
    }
}
