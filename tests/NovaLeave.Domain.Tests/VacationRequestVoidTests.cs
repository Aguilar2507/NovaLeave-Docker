using NovaLeave.Domain.Entities;

namespace NovaLeave.Domain.Tests;

// T400: Domain tests for VacationRequest void (US4, Phase 6).
// Owner voids Approved request when StartDate > today → Voided (final), days returned to balance.
public class VacationRequestVoidTests
{
    private static DateOnly FutureDate(int daysFromNow = 10) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysFromNow);

    private static DateOnly PastDate(int daysAgo = 5) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-daysAgo);

    [Fact]
    public void Void_ApprovedFutureRequest_TransitionsToVoided()
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
        var voidedBy = Guid.NewGuid();

        // Act
        request.Void(voidedBy, "Cambio de planes");

        // Assert
        Assert.Equal(RequestStatus.Voided, request.Status);
        Assert.NotNull(request.ResolvedAt);
        Assert.Equal(voidedBy, request.ResolvedBy);
        Assert.Equal("Cambio de planes", request.RejectionReason); // Reuses field
    }

    [Fact]
    public void Void_PendingRequest_Throws()
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

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => 
            request.Void(Guid.NewGuid(), "Test"));
        Assert.Contains("Solo las solicitudes aprobadas pueden ser anuladas", ex.Message);
    }

    [Fact]
    public void Void_RejectedRequest_Throws()
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
        var ex = Assert.Throws<InvalidOperationException>(() => 
            request.Void(Guid.NewGuid(), "Test"));
        Assert.Contains("Solo las solicitudes aprobadas pueden ser anuladas", ex.Message);
    }

    [Fact]
    public void Void_CancelledRequest_Throws()
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
        var ex = Assert.Throws<InvalidOperationException>(() => 
            request.Void(Guid.NewGuid(), "Test"));
        Assert.Contains("Solo las solicitudes aprobadas pueden ser anuladas", ex.Message);
    }

    [Fact]
    public void Void_AlreadyVoided_Throws()
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
        request.Void(Guid.NewGuid(), "Primera anulación");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => 
            request.Void(Guid.NewGuid(), "Segunda anulación"));
        Assert.Contains("Solo las solicitudes aprobadas pueden ser anuladas", ex.Message);
    }

    [Fact]
    public void Void_RequiresReason()
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

        // Act & Assert - empty reason
        var ex1 = Assert.Throws<ArgumentException>(() => 
            request.Void(Guid.NewGuid(), ""));
        Assert.Contains("reason", ex1.Message, StringComparison.OrdinalIgnoreCase);

        // Act & Assert - null reason
        var ex2 = Assert.Throws<ArgumentException>(() => 
            request.Void(Guid.NewGuid(), null!));
        Assert.Contains("reason", ex2.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Void_SetsResolvedAtTimestamp()
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
        var beforeVoid = DateTimeOffset.UtcNow;

        // Act
        request.Void(Guid.NewGuid(), "Cambio de planes");

        // Assert
        Assert.NotNull(request.ResolvedAt);
        Assert.True(request.ResolvedAt >= beforeVoid);
        Assert.True(request.ResolvedAt <= DateTimeOffset.UtcNow.AddSeconds(1));
    }
}
