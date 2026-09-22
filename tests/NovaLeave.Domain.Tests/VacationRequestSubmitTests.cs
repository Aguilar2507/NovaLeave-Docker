using NovaLeave.Domain.Entities;
using NovaLeave.Domain.Services;

namespace NovaLeave.Domain.Tests;

// T100: Domain tests for VacationRequest submission behavior, overlap detection, working-day calculation.
public class VacationRequestSubmitTests
{
    private static DateOnly FutureDate(int daysFromNow = 10) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysFromNow);

    [Fact]
    public void NewRequest_startsInPendingState()
    {
        var request = new VacationRequest(
            id: Guid.NewGuid(),
            ownerId: Guid.NewGuid(),
            startDate: FutureDate(10),
            endDate: FutureDate(14),
            reason: "Vacaciones",
            requestedDays: 3,
            expiryDate: FutureDate(44));

        Assert.Equal(RequestStatus.Pending, request.Status);
    }

    [Fact]
    public void ConstructorGuard_startBeforeToday_throws()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            new VacationRequest(
                id: Guid.NewGuid(),
                ownerId: Guid.NewGuid(),
                startDate: today, // hoy no es válido (spec exige > hoy)
                endDate: today.AddDays(3),
                reason: "X",
                requestedDays: 1,
                expiryDate: today.AddDays(30)));
    }

    [Fact]
    public void ConstructorGuard_invertedRange_throws()
    {
        var start = FutureDate(10);

        Assert.Throws<InvalidOperationException>(() =>
            new VacationRequest(
                id: Guid.NewGuid(),
                ownerId: Guid.NewGuid(),
                startDate: start,
                endDate: start.AddDays(-2),
                reason: "X",
                requestedDays: 1,
                expiryDate: start.AddDays(30)));
    }

    [Fact]
    public void OverlapChecker_noOverlap_returnsFalse()
    {
        var checker = new OverlapChecker();
        var existingRequests = new List<VacationRequest>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), FutureDate(10), FutureDate(12), "A", 2, FutureDate(40))
        };

        var hasOverlap = checker.HasOverlap(FutureDate(20), FutureDate(25), existingRequests);

        Assert.False(hasOverlap);
    }

    [Fact]
    public void OverlapChecker_touchingAtEnd_isOverlap()
    {
        var checker = new OverlapChecker();
        var existingRequests = new List<VacationRequest>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), FutureDate(10), FutureDate(15), "A", 4, FutureDate(40))
        };

        var hasOverlap = checker.HasOverlap(FutureDate(15), FutureDate(20), existingRequests);

        Assert.True(hasOverlap); // spec: inclusive touch counts as overlap
    }

    [Fact]
    public void OverlapChecker_fullyContained_isOverlap()
    {
        var checker = new OverlapChecker();
        var existingRequests = new List<VacationRequest>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), FutureDate(10), FutureDate(20), "A", 7, FutureDate(50))
        };

        var hasOverlap = checker.HasOverlap(FutureDate(12), FutureDate(15), existingRequests);

        Assert.True(hasOverlap);
    }

    [Fact]
    public void OverlapChecker_onlyChecksPendingAndApproved()
    {
        var checker = new OverlapChecker();
        var rejected = new VacationRequest(
            Guid.NewGuid(), Guid.NewGuid(),
            FutureDate(10), FutureDate(15), "Rejected", 4, FutureDate(40));
        rejected.Reject(Guid.NewGuid(), "No disponible");

        var hasOverlap = checker.HasOverlap(FutureDate(12), FutureDate(14), new[] { rejected });

        Assert.False(hasOverlap); // rejected no cuenta
    }

    [Theory]
    [InlineData(20, 5)]   // balance suficiente
    [InlineData(3, 3)]    // justo
    public void EmployeeReserveDays_sufficientBalance_succeeds(int initialBalance, int toReserve)
    {
        var employee = new Employee(
            id: Guid.NewGuid(),
            identityId: "id-1",
            name: "Test",
            email: "test@example.com",
            employmentStartDate: new DateOnly(2024, 1, 1),
            initialBalance: initialBalance);

        employee.ReserveDays(toReserve);

        Assert.Equal(initialBalance - toReserve, employee.Balance);
    }

    [Fact]
    public void EmployeeReserveDays_insufficientBalance_throws()
    {
        var employee = new Employee(
            id: Guid.NewGuid(),
            identityId: "id-1",
            name: "Test",
            email: "test@example.com",
            employmentStartDate: new DateOnly(2024, 1, 1),
            initialBalance: 2);

        Assert.Throws<InvalidOperationException>(() => employee.ReserveDays(5));
    }
}
