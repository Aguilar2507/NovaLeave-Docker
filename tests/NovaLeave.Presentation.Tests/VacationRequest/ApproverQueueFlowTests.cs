using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Application.Features.VacationRequest.List;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Identity;
using NovaLeave.Presentation.Tests.Fixtures;
using Xunit;

namespace NovaLeave.Presentation.Tests.VacationRequest;

// Verifica la cadena del flujo del aprobador (T214, US2):
// 1) El claims factory emite el claim "EmployeeId" del approver.
// 2) Con ese EmployeeId, la cola de solicitudes Pending asignadas se resuelve.
// Sin el fix, GetEmployeeIdFromClaims() usaba ClaimTypes.NameIdentifier (userId de
// Identity) y la cola quedaba vacía porque AssignedApproverId almacena EmployeeId.
[Collection("VacationTests")]
public class ApproverQueueFlowTests
{
    private readonly VacationTestFixture _fixture;

    public ApproverQueueFlowTests(VacationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ApproverPrincipal_EmployeeIdClaim_MatchesSeededApprover()
    {
        using var scope = _fixture.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();

        var approver = await userManager.FindByEmailAsync(VacationTestFixture.TestApproverEmail);
        Assert.NotNull(approver);

        var principal = await factory.CreateAsync(approver!);

        var claim = principal.FindFirst("EmployeeId");
        Assert.NotNull(claim);
        Assert.Equal(VacationTestFixture.TestApproverEmployeeId.ToString(), claim.Value);

        // El claim debe diferir del NameIdentifier (userId de Identity): ese era el bug.
        var nameId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Assert.NotEqual(nameId, claim.Value);
    }

    [Fact]
    public async Task ListPendingForApprover_WithApproverEmployeeId_ReturnsAssignedPendingRequest()
    {
        using var scope = _fixture.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IVacationRequestRepository>();
        var handler = scope.ServiceProvider.GetRequiredService<ListPendingForApproverHandler>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Datos propios (emails/ids únicos) para no depender del seed compartido,
        // que puede omitirse en suite por la InMemory DB compartida entre colecciones.
        var owner = new Employee(
            id: Guid.NewGuid(),
            identityId: string.Empty,
            name: $"Queue Owner {Guid.NewGuid():N}",
            email: $"queue-{Guid.NewGuid():N}@novaleave.test",
            employmentStartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
            initialBalance: 10);
        owner.SetAssignedApprover(VacationTestFixture.TestApproverEmployeeId);

        var requestId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new Domain.Entities.VacationRequest(
            id: requestId,
            ownerId: owner.Id,
            startDate: today.AddDays(20),
            endDate: today.AddDays(24),
            reason: "Solicitud del flujo aprobador",
            requestedDays: 4,
            expiryDate: today.AddDays(54));

        db.Employees.Add(owner);
        db.VacationRequests.Add(request);
        await db.SaveChangesAsync();

        // Precondición: la solicitud Pending está asignada a este approver.
        var seeded = await db.VacationRequests
            .Include(r => r.Owner)
            .FirstOrDefaultAsync(r => r.Id == requestId);
        Assert.NotNull(seeded);
        Assert.Equal(VacationTestFixture.TestApproverEmployeeId, seeded!.Owner.AssignedApproverId);

        var query = new ListPendingForApproverQuery(
            VacationTestFixture.TestApproverEmployeeId,
            PageNumber: 1,
            PageSize: 20);

        var requests = await handler.Handle(query, CancellationToken.None);

        Assert.Contains(requests, r => r.Id == requestId);

        // Con un EmployeeId distinto (el que devolvía el bug: userId de Identity)
        // la cola debe quedar vacía.
        var wrongApproverId = Guid.NewGuid();
        var emptyQuery = new ListPendingForApproverQuery(wrongApproverId, 1, 20);
        var empty = await handler.Handle(emptyQuery, CancellationToken.None);
        Assert.Empty(empty);
    }

    [Fact]
    public async Task ApproverQueue_Unauthenticated_RedirectsToLogin()
    {
        var client = _fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/approver/requests");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString);
    }
}
