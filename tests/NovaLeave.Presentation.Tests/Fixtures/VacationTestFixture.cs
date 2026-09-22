using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Identity;
using Xunit;
using VacationRequestEntity = NovaLeave.Domain.Entities.VacationRequest;

namespace NovaLeave.Presentation.Tests.Fixtures;

// Shared collection for VacationTestFixture (T026, spec_001 Phase 2).
// Reutiliza la infraestructura de AuthTestFixture y añade datos semilla propios de vacaciones.
[CollectionDefinition("VacationTests")]
public class VacationTestCollection : ICollectionFixture<VacationTestFixture>;

// WebApplicationFactory que extiende AuthTestFixture con datos semilla adicionales:
// - Asigna testApproverEmployee como AssignedApprover de testEmployee.
// - Crea varias VacationRequests en distintos estados para escenarios de US1-US5.
public class VacationTestFixture : AuthTestFixture
{
    public static readonly Guid PendingRequestId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid ApprovedRequestId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid RejectedRequestId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Deja que la fixture base seed los empleados y usuarios de Identity.
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            SeedVacationDataAsync(db).GetAwaiter().GetResult();
        });
    }

    private static async Task SeedVacationDataAsync(ApplicationDbContext db)
    {
        // Skip si ya hay solicitudes semilladas
        if (await db.VacationRequests.AnyAsync())
        {
            return;
        }

        // Vincula testEmployee -> testApproverEmployee como AssignedApprover
        var owner = await db.Employees.FindAsync(TestEmployeeId);
        if (owner is not null && owner.AssignedApproverId is null)
        {
            owner.SetAssignedApprover(TestApproverEmployeeId);
        }

        // Fechas semilla usando UTC (las pruebas suelen sustituir TimeProvider por FakeTimeProvider,
        // pero los datos semilla no dependen del reloj de dominio).
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var future1Start = today.AddDays(10);
        var future1End = future1Start.AddDays(4);
        var future2Start = today.AddDays(30);
        var future2End = future2Start.AddDays(6);
        var future3Start = today.AddDays(60);
        var future3End = future3Start.AddDays(3);

        // Pending
        var pending = new VacationRequestEntity(
            id: PendingRequestId,
            ownerId: TestEmployeeId,
            startDate: future1Start,
            endDate: future1End,
            reason: "Vacaciones familiares",
            requestedDays: 3,
            expiryDate: future1End.AddDays(30));

        // Approved (aprobada por el approver)
        var approved = new VacationRequestEntity(
            id: ApprovedRequestId,
            ownerId: TestEmployeeId,
            startDate: future2Start,
            endDate: future2End,
            reason: "Viaje planificado",
            requestedDays: 5,
            expiryDate: future2End.AddDays(30));
        approved.Approve(TestApproverEmployeeId);

        // Rejected (con motivo)
        var rejected = new VacationRequestEntity(
            id: RejectedRequestId,
            ownerId: TestEmployeeId,
            startDate: future3Start,
            endDate: future3End,
            reason: "Descanso corto",
            requestedDays: 2,
            expiryDate: future3End.AddDays(30));
        rejected.Reject(TestApproverEmployeeId, "Fechas conflictivas con proyecto");

        db.VacationRequests.AddRange(pending, approved, rejected);
        await db.SaveChangesAsync();
    }
}
