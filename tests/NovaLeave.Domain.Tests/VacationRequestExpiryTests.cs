using NovaLeave.Domain.Entities;

namespace NovaLeave.Domain.Tests;

// T610: Domain tests for VacationRequest.Expire() (Phase 8, FR-016).
// Validates Pending → Expired transition, non-Pending guard, timestamp behavior.
public class VacationRequestExpiryTests
{
    [Fact]
    public void Expire_PendingRequest_TransitionsToExpired()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new VacationRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            today.AddDays(10),
            today.AddDays(14),
            "Vacation",
            5,
            today.AddDays(-1)); // Already expired

        Assert.Equal(RequestStatus.Pending, request.Status);

        // Act
        request.Expire();

        // Assert
        Assert.Equal(RequestStatus.Expired, request.Status);
        Assert.NotNull(request.ResolvedAt);
        Assert.True(request.ResolvedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Expire_ApprovedRequest_Throws()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new VacationRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            today.AddDays(10),
            today.AddDays(14),
            "Vacation",
            5,
            today.AddDays(44));

        request.Approve(Guid.NewGuid());

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => request.Expire());
        Assert.Contains("solo es válida para solicitudes en estado Pending", ex.Message);
    }

    [Fact]
    public void Expire_RejectedRequest_Throws()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new VacationRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            today.AddDays(10),
            today.AddDays(14),
            "Vacation",
            5,
            today.AddDays(44));

        request.Reject(Guid.NewGuid(), "No available");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => request.Expire());
        Assert.Contains("solo es válida para solicitudes en estado Pending", ex.Message);
    }

    [Fact]
    public void Expire_CancelledRequest_Throws()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new VacationRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            today.AddDays(10),
            today.AddDays(14),
            "Vacation",
            5,
            today.AddDays(44));

        request.Cancel();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => request.Expire());
        Assert.Contains("solo es válida para solicitudes en estado Pending", ex.Message);
    }

    [Fact]
    public void Expire_VoidedRequest_Throws()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new VacationRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            today.AddDays(10),
            today.AddDays(14),
            "Vacation",
            5,
            today.AddDays(44));

        request.Approve(Guid.NewGuid());
        request.Void(Guid.NewGuid(), "Test void");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => request.Expire());
        Assert.Contains("solo es válida para solicitudes en estado Pending", ex.Message);
    }

    [Fact]
    public void Expire_SetsResolvedAtTimestamp()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new VacationRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            today.AddDays(10),
            today.AddDays(14),
            "Vacation",
            5,
            today.AddDays(-1));

        var beforeExpiry = DateTimeOffset.UtcNow;

        // Act
        request.Expire();

        // Assert
        Assert.NotNull(request.ResolvedAt);
        Assert.True(request.ResolvedAt >= beforeExpiry);
        Assert.True(request.ResolvedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Expire_MultipleCalls_Throws()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new VacationRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            today.AddDays(10),
            today.AddDays(14),
            "Vacation",
            5,
            today.AddDays(-1));

        request.Expire();

        // Act & Assert - trying to expire again
        var ex = Assert.Throws<InvalidOperationException>(() => request.Expire());
        Assert.Contains("solo es válida para solicitudes en estado Pending", ex.Message);
    }
}
