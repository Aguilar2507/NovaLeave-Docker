using NSubstitute;
using NovaLeave.Application.Features.VacationRequest.Cancel;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Tests;

// T301: Handler tests for CancelRequestHandler (US3, Phase 5).
public class CancelRequestHandlerTests
{
    private readonly IVacationRequestRepository _mockRepository;
    private readonly IStateTransitionAuditService _mockAuditService;
    private readonly CancelRequestHandler _handler;

    private static readonly Guid TestRequestId = Guid.NewGuid();
    private static readonly Guid TestEmployeeId = Guid.NewGuid();
    private static readonly DateOnly FutureStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
    private static readonly DateOnly FutureEnd = FutureStart.AddDays(4);

    public CancelRequestHandlerTests()
    {
        _mockRepository = Substitute.For<IVacationRequestRepository>();
        _mockAuditService = Substitute.For<IStateTransitionAuditService>();
        _handler = new CancelRequestHandler(_mockRepository, _mockAuditService);
    }

    private static Employee CreateTestEmployee(int balance) =>
        new(TestEmployeeId, "test-id", "Test Emp", "test@test.com",
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1), balance, null);

    [Fact]
    public async Task Handle_ValidRequest_CancelsAndRestoresReservation()
    {
        // Arrange
        var employee = CreateTestEmployee(20);
        var request = new VacationRequest(TestRequestId, TestEmployeeId, FutureStart, FutureEnd, "Vacation", 5, FutureEnd.AddDays(30));
        var command = new CancelRequestCommand(TestRequestId, "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        // Act
        await _handler.Handle(command, employee, CancellationToken.None);

        // Assert
        Assert.Equal(RequestStatus.Cancelled, request.Status);
        Assert.Equal(25, employee.Balance); // 20 + 5 restored
        await _mockRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RequestNotFound_Throws()
    {
        // Arrange
        var employee = CreateTestEmployee(20);
        var command = new CancelRequestCommand(TestRequestId, "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns((VacationRequest?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, employee, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NotOwner_Throws()
    {
        // Arrange
        var employee = CreateTestEmployee(20);
        var otherOwnerId = Guid.NewGuid();
        var request = new VacationRequest(TestRequestId, otherOwnerId, FutureStart, FutureEnd, "Vacation", 5, FutureEnd.AddDays(30));
        var command = new CancelRequestCommand(TestRequestId, "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, employee, CancellationToken.None));
        Assert.Contains("no coincide con el propietario", ex.Message);
    }

    [Fact]
    public async Task Handle_AlreadyApproved_Throws()
    {
        // Arrange
        var employee = CreateTestEmployee(20);
        var request = new VacationRequest(TestRequestId, TestEmployeeId, FutureStart, FutureEnd, "Vacation", 5, FutureEnd.AddDays(30));
        request.Approve(Guid.NewGuid());
        var command = new CancelRequestCommand(TestRequestId, "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, employee, CancellationToken.None));
        Assert.Contains("solo es válida para solicitudes en estado Pending", ex.Message);
    }

    [Fact]
    public async Task Handle_WritesAuditRecord()
    {
        // Arrange
        var employee = CreateTestEmployee(20);
        var request = new VacationRequest(TestRequestId, TestEmployeeId, FutureStart, FutureEnd, "Vacation", 5, FutureEnd.AddDays(30));
        var correlationId = "test-corr-123";
        var command = new CancelRequestCommand(TestRequestId, correlationId);

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        // Act
        await _handler.Handle(command, employee, CancellationToken.None);

        // Assert
        await _mockAuditService.Received(1).RecordTransitionAsync(
            TestRequestId,
            RequestStatus.Pending,
            RequestStatus.Cancelled,
            TestEmployeeId.ToString(),
            "Employee",
            correlationId,
            null,
            TestRequestId.ToString(),
            Arg.Any<CancellationToken>());
    }
}
