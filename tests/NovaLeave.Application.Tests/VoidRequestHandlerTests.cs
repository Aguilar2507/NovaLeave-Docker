using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Application.Features.VacationRequest.Void;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Tests;

// T401: Handler tests for VoidRequestHandler (US4, Phase 6).
// Validates StartDate > today, transitions Approved → Voided, restores days.
public class VoidRequestHandlerTests
{
    private readonly IVacationRequestRepository _mockRepository;
    private readonly IStateTransitionAuditService _mockAuditService;

    private static readonly Guid TestRequestId = Guid.NewGuid();
    private static readonly Guid TestEmployeeId = Guid.NewGuid();

    public VoidRequestHandlerTests()
    {
        _mockRepository = Substitute.For<IVacationRequestRepository>();
        _mockAuditService = Substitute.For<IStateTransitionAuditService>();
    }

    private static Employee CreateTestEmployee(int balance) =>
        new(TestEmployeeId, "test-id", "Test Emp", "test@test.com",
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1), balance, null);

    [Fact]
    public async Task Handle_FutureStartApprovedRequest_VoidsAndRestoresDays()
    {
        // Arrange - request with future start (created now, will be valid for voiding)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureStart = today.AddDays(10);
        var futureEnd = futureStart.AddDays(4);

        var fakeTime = new FakeTimeProvider();
        var handler = new VoidRequestHandler(_mockRepository, _mockAuditService, fakeTime);

        var employee = CreateTestEmployee(10); // Already deducted 5

        var request = new VacationRequest(TestRequestId, TestEmployeeId, futureStart, futureEnd, "Vacation", 5, futureEnd.AddDays(30));
        request.Approve(Guid.NewGuid());

        var command = new VoidRequestCommand(TestRequestId, "Change of plans", "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        // Act
        await handler.Handle(command, employee, CancellationToken.None);

        // Assert
        Assert.Equal(RequestStatus.Voided, request.Status);
        Assert.Equal(15, employee.Balance); // 10 + 5 restored
        await _mockRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PastStartDate_Throws()
    {
        // Arrange - simulate a request where start date is in the past relative to handler's TimeProvider
        // Request created with start date 5 days from real "now"
        var realToday = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureStart = realToday.AddDays(5);
        var futureEnd = futureStart.AddDays(4);

        var request = new VacationRequest(TestRequestId, TestEmployeeId, futureStart, futureEnd, "Vacation", 5, futureEnd.AddDays(30));
        request.Approve(Guid.NewGuid());

        // Handler uses a fake time that is advanced beyond the start date
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(10)); // "Today" is 10 days from now
        var handler = new VoidRequestHandler(_mockRepository, _mockAuditService, fakeTime);

        var employee = CreateTestEmployee(10);
        var command = new VoidRequestCommand(TestRequestId, "Too late", "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, employee, CancellationToken.None));
        Assert.Contains("no se puede anular porque ya ha comenzado", ex.Message);
    }

    [Fact]
    public async Task Handle_RequestNotFound_Throws()
    {
        // Arrange
        var handler = new VoidRequestHandler(_mockRepository, _mockAuditService, TimeProvider.System);
        var employee = CreateTestEmployee(10);
        var command = new VoidRequestCommand(TestRequestId, "Reason", "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns((VacationRequest?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, employee, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NotOwner_Throws()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureStart = today.AddDays(10);
        var futureEnd = futureStart.AddDays(4);

        var handler = new VoidRequestHandler(_mockRepository, _mockAuditService, TimeProvider.System);

        var employee = CreateTestEmployee(10);
        var otherOwnerId = Guid.NewGuid();

        var request = new VacationRequest(TestRequestId, otherOwnerId, futureStart, futureEnd, "Vacation", 5, futureEnd.AddDays(30));
        request.Approve(Guid.NewGuid());

        var command = new VoidRequestCommand(TestRequestId, "Reason", "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, employee, CancellationToken.None));
        Assert.Contains("no coincide con el propietario", ex.Message);
    }

    [Fact]
    public async Task Handle_PendingRequest_Throws()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureStart = today.AddDays(10);
        var futureEnd = futureStart.AddDays(4);

        var handler = new VoidRequestHandler(_mockRepository, _mockAuditService, TimeProvider.System);

        var employee = CreateTestEmployee(10);

        var request = new VacationRequest(TestRequestId, TestEmployeeId, futureStart, futureEnd, "Vacation", 5, futureEnd.AddDays(30));
        // Not approved

        var command = new VoidRequestCommand(TestRequestId, "Reason", "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, employee, CancellationToken.None));
        Assert.Contains("Solo las solicitudes aprobadas pueden ser anuladas", ex.Message);
    }

    [Fact]
    public async Task Handle_WritesAuditRecord()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureStart = today.AddDays(10);
        var futureEnd = futureStart.AddDays(4);

        var handler = new VoidRequestHandler(_mockRepository, _mockAuditService, TimeProvider.System);

        var employee = CreateTestEmployee(10);

        var request = new VacationRequest(TestRequestId, TestEmployeeId, futureStart, futureEnd, "Vacation", 5, futureEnd.AddDays(30));
        request.Approve(Guid.NewGuid());

        var correlationId = "test-corr-123";
        var reason = "Family emergency";
        var command = new VoidRequestCommand(TestRequestId, reason, correlationId);

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        // Act
        await handler.Handle(command, employee, CancellationToken.None);

        // Assert
        await _mockAuditService.Received(1).RecordTransitionAsync(
            TestRequestId,
            RequestStatus.Approved,
            RequestStatus.Voided,
            TestEmployeeId.ToString(),
            "Employee",
            correlationId,
            reason,
            TestRequestId.ToString(),
            Arg.Any<CancellationToken>());
    }
}
