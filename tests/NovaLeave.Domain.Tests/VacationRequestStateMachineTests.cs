using NovaLeave.Domain.Entities;

namespace NovaLeave.Domain.Tests;

public class VacationRequestStateMachineTests
{
    // Todas las solicitudes creadas deben usar fechas estrictamente futuras
    // porque la invariante del constructor exige StartDate > hoy.
    private static VacationRequest CreatePending(Guid? ownerId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(10);
        var end = start.AddDays(4);

        return new VacationRequest(
            id: Guid.NewGuid(),
            ownerId: ownerId ?? Guid.NewGuid(),
            startDate: start,
            endDate: end,
            reason: "Motivo",
            requestedDays: 3,
            expiryDate: end.AddDays(30));
    }

    [Fact]
    public void NewRequest_isPending()
    {
        var request = CreatePending();

        Assert.Equal(RequestStatus.Pending, request.Status);
        Assert.Null(request.ResolvedAt);
        Assert.Null(request.ResolvedBy);
        Assert.False(request.IsFinalState());
    }

    [Fact]
    public void Approve_fromPending_transitionsToApproved()
    {
        var request = CreatePending();
        var approverId = Guid.NewGuid();

        request.Approve(approverId);

        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Equal(approverId, request.ResolvedBy);
        Assert.NotNull(request.ResolvedAt);
        Assert.True(request.IsFinalState());
    }

    [Fact]
    public void Approve_fromNonPending_throws()
    {
        var request = CreatePending();
        request.Approve(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => request.Approve(Guid.NewGuid()));
    }

    [Fact]
    public void Reject_requiresReason()
    {
        var request = CreatePending();

        Assert.Throws<ArgumentException>(() => request.Reject(Guid.NewGuid(), string.Empty));
        Assert.Throws<ArgumentException>(() => request.Reject(Guid.NewGuid(), "   "));
    }

    [Fact]
    public void Reject_setsRejectionReasonAndStatus()
    {
        var request = CreatePending();

        request.Reject(Guid.NewGuid(), "Fechas conflictivas");

        Assert.Equal(RequestStatus.Rejected, request.Status);
        Assert.Equal("Fechas conflictivas", request.RejectionReason);
    }

    [Fact]
    public void Cancel_onlyFromPending()
    {
        var request = CreatePending();
        request.Cancel();
        Assert.Equal(RequestStatus.Cancelled, request.Status);

        var approved = CreatePending();
        approved.Approve(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => approved.Cancel());
    }

    [Fact]
    public void Void_onlyFromApproved_requiresReason()
    {
        var request = CreatePending();
        request.Approve(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => request.Void(Guid.NewGuid(), " "));

        request.Void(Guid.NewGuid(), "Error administrativo");

        Assert.Equal(RequestStatus.Voided, request.Status);
    }

    [Fact]
    public void Void_fromPending_throws()
    {
        var request = CreatePending();

        Assert.Throws<InvalidOperationException>(() => request.Void(Guid.NewGuid(), "no válido"));
    }

    [Fact]
    public void Expire_fromPending_transitionsToExpired()
    {
        var request = CreatePending();

        request.Expire();

        Assert.Equal(RequestStatus.Expired, request.Status);
    }

    [Fact]
    public void Edit_fromPending_updatesAndRevalidates()
    {
        var request = CreatePending();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var newStart = today.AddDays(20);
        var newEnd = newStart.AddDays(2);

        request.Edit(newStart, newEnd, "Nuevo motivo", 2, newEnd.AddDays(30));

        Assert.Equal(newStart, request.StartDate);
        Assert.Equal(newEnd, request.EndDate);
        Assert.Equal("Nuevo motivo", request.Reason);
        Assert.Equal(2, request.RequestedDays);
    }

    [Fact]
    public void Edit_afterFinalState_throws()
    {
        var request = CreatePending();
        request.Approve(Guid.NewGuid());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            request.Edit(today.AddDays(20), today.AddDays(22), "x", 1, today.AddDays(50)));
    }

    [Fact]
    public void Constructor_pastStartDate_throws()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            new VacationRequest(
                id: Guid.NewGuid(),
                ownerId: Guid.NewGuid(),
                startDate: today.AddDays(-1),
                endDate: today.AddDays(3),
                reason: "x",
                requestedDays: 1,
                expiryDate: today.AddDays(30)));
    }

    [Fact]
    public void Constructor_invertedRange_throws()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            new VacationRequest(
                id: Guid.NewGuid(),
                ownerId: Guid.NewGuid(),
                startDate: today.AddDays(10),
                endDate: today.AddDays(5),
                reason: "x",
                requestedDays: 1,
                expiryDate: today.AddDays(30)));
    }
}
