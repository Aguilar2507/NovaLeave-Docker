using NovaLeave.Domain.Entities;

namespace NovaLeave.Domain.Tests;

// T300: Domain tests for VacationRequest cancellation (US3, Phase 5).
// Owner can cancel Pending request → Cancelled (final), reservation released.
public class VacationRequestCancelTests
{
    private static DateOnly FutureDate(int daysFromNow = 10) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysFromNow);

    [Fact]
    public void Cancel_PendingRequest_TransitionsToCancelled()
    {
        // Arrange
        var request = new VacationRequest(
            id: Guid.NewGuid(),
            ownerId: Guid.NewGuid(),
            startDate: FutureDate(10),
            endDate: FutureDate(14),
            reason: "Vacaciones",
            requestedDays: 5,
            expiryDate: FutureDate(44));

        // Act
        request.Cancel();

        // Assert
        Assert.Equal(RequestStatus.Cancelled, request.Status);
        Assert.NotNull(request.ResolvedAt);
        Assert.Null(request.ResolvedBy); // Cancelled by owner, not by approver
    }

    [Fact]
    public void Cancel_AlreadyApproved_Throws()
    {
        // Arrange
        var request = new VacationRequest(
            id: Guid.NewGuid(),
            ownerId: Guid.NewGuid(),
            startDate: FutureDate(10),
            endDate: FutureDate(14),
            reason: "Vacaciones",
            requestedDays: 5,
            expiryDate: FutureDate(44));

        request.Approve(Guid.NewGuid());

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => request.Cancel());
        Assert.Contains("solo es válida para solicitudes en estado Pending", ex.Message);
    }

    [Fact]
    public void Cancel_AlreadyRejected_Throws()
    {
        // Arrange
        var request = new VacationRequest(
            id: Guid.NewGuid(),
            ownerId: Guid.NewGuid(),
            startDate: FutureDate(10),
            endDate: FutureDate(14),
            reason: "Vacaciones",
            requestedDays: 5,
            expiryDate: FutureDate(44));

        request.Reject(Guid.NewGuid(), "No aprobado");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => request.Cancel());
        Assert.Contains("solo es válida para solicitudes en estado Pending", ex.Message);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_Throws()
    {
        // Arrange
        var request = new VacationRequest(
            id: Guid.NewGuid(),
            ownerId: Guid.NewGuid(),
            startDate: FutureDate(10),
            endDate: FutureDate(14),
            reason: "Vacaciones",
            requestedDays: 5,
            expiryDate: FutureDate(44));

        request.Cancel();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => request.Cancel());
        Assert.Contains("solo es válida para solicitudes en estado Pending", ex.Message);
    }

    [Fact]
    public void Cancel_SetsResolvedAtTimestamp()
    {
        // Arrange
        var request = new VacationRequest(
            id: Guid.NewGuid(),
            ownerId: Guid.NewGuid(),
            startDate: FutureDate(10),
            endDate: FutureDate(14),
            reason: "Vacaciones",
            requestedDays: 5,
            expiryDate: FutureDate(44));

        var beforeCancel = DateTimeOffset.UtcNow;

        // Act
        request.Cancel();

        // Assert
        Assert.NotNull(request.ResolvedAt);
        Assert.True(request.ResolvedAt >= beforeCancel);
        Assert.True(request.ResolvedAt <= DateTimeOffset.UtcNow.AddSeconds(1));
    }
}
