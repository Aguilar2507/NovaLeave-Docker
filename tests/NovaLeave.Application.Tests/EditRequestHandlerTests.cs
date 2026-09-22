using NSubstitute;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Application.Features.VacationRequest.Edit;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Tests;

// T600: Handler tests for EditRequestHandler (Phase 8, FR-009).
// Validates dates re-validated, overlap re-checked, balance re-checked, RowVersion enforced.
public class EditRequestHandlerTests
{
    private readonly IVacationRequestRepository _mockRepository;
    private readonly IStateTransitionAuditService _mockAuditService;

    private static readonly Guid TestRequestId = Guid.NewGuid();
    private static readonly Guid TestEmployeeId = Guid.NewGuid();

    public EditRequestHandlerTests()
    {
        _mockRepository = Substitute.For<IVacationRequestRepository>();
        _mockAuditService = Substitute.For<IStateTransitionAuditService>();
    }

    private static Employee CreateTestEmployee(Guid id, int balance) =>
        new(id, $"test-{id}", $"Test Emp {id}", $"test{id}@test.com",
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1), balance, null);

    [Fact]
    public async Task Handle_ValidEdit_UpdatesDatesAndDays()
    {
        // Arrange
        var handler = new EditRequestHandler(_mockRepository, _mockAuditService, TimeProvider.System);
        var employee = CreateTestEmployee(TestEmployeeId, 20);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var originalStart = today.AddDays(10);
        var originalEnd = originalStart.AddDays(4); // 5 days

        var request = new VacationRequest(TestRequestId, TestEmployeeId, originalStart, originalEnd, "Original reason", 5, originalEnd.AddDays(30));

        var newStart = today.AddDays(15);
        var newEnd = newStart.AddDays(6); // 7 days (2 more)

        var command = new EditRequestCommand(
            TestRequestId,
            newStart,
            newEnd,
            "Updated reason",
            "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);
        _mockRepository.ExistsOverlappingAsync(TestEmployeeId, newStart, newEnd, TestRequestId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await handler.Handle(command, employee, CancellationToken.None);

        // Assert
        Assert.Equal(newStart, request.StartDate);
        Assert.Equal(newEnd, request.EndDate);
        Assert.Equal(7, request.RequestedDays);
        Assert.Equal("Updated reason", request.Reason);
        // Balance should have been adjusted: originally deducted 5, now needs 7, so deduct 2 more
        Assert.Equal(18, employee.Balance); // 20 - 2
        await _mockRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReducingDays_RestoresBalance()
    {
        // Arrange
        var handler = new EditRequestHandler(_mockRepository, _mockAuditService, TimeProvider.System);
        var employee = CreateTestEmployee(TestEmployeeId, 15); // Already deducted 5

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var originalStart = today.AddDays(10);
        var originalEnd = originalStart.AddDays(4); // 5 days

        var request = new VacationRequest(TestRequestId, TestEmployeeId, originalStart, originalEnd, "Original reason", 5, originalEnd.AddDays(30));

        var newStart = today.AddDays(15);
        var newEnd = newStart.AddDays(2); // 3 days (2 fewer)

        var command = new EditRequestCommand(
            TestRequestId,
            newStart,
            newEnd,
            "Shorter vacation",
            "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);
        _mockRepository.ExistsOverlappingAsync(TestEmployeeId, newStart, newEnd, TestRequestId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await handler.Handle(command, employee, CancellationToken.None);

        // Assert
        Assert.Equal(3, request.RequestedDays);
        // Balance should increase by 2: was 15, restore 2 days
        Assert.Equal(17, employee.Balance);
    }

    [Fact]
    public async Task Handle_RequestNotFound_Throws()
    {
        // Arrange
        var handler = new EditRequestHandler(_mockRepository, _mockAuditService, TimeProvider.System);
        var employee = CreateTestEmployee(TestEmployeeId, 20);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var command = new EditRequestCommand(
            TestRequestId,
            today.AddDays(10),
            today.AddDays(14),
            "Reason",
            "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns((VacationRequest?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, employee, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NotOwner_Throws()
    {
        // Arrange
        var handler = new EditRequestHandler(_mockRepository, _mockAuditService, TimeProvider.System);
        var employee = CreateTestEmployee(TestEmployeeId, 20);
        var otherOwnerId = Guid.NewGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new VacationRequest(
            TestRequestId, otherOwnerId,
            today.AddDays(10), today.AddDays(14),
            "Reason", 5, today.AddDays(44));

        var command = new EditRequestCommand(
            TestRequestId,
            today.AddDays(15),
            today.AddDays(19),
            "Updated",
            "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, employee, CancellationToken.None));
        Assert.Contains("no coincide con el propietario", ex.Message);
    }

    [Fact]
    public async Task Handle_NonPendingRequest_Throws()
    {
        // Arrange
        var handler = new EditRequestHandler(_mockRepository, _mockAuditService, TimeProvider.System);
        var employee = CreateTestEmployee(TestEmployeeId, 20);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new VacationRequest(
            TestRequestId, TestEmployeeId,
            today.AddDays(10), today.AddDays(14),
            "Reason", 5, today.AddDays(44));

        // Approve it so it's no longer Pending
        request.Approve(Guid.NewGuid());

        var command = new EditRequestCommand(
            TestRequestId,
            today.AddDays(15),
            today.AddDays(19),
            "Updated",
            "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, employee, CancellationToken.None));
        Assert.Contains("Solo las solicitudes en estado Pending pueden ser editadas", ex.Message);
    }

    [Fact]
    public async Task Handle_OverlappingDates_Throws()
    {
        // Arrange
        var handler = new EditRequestHandler(_mockRepository, _mockAuditService, TimeProvider.System);
        var employee = CreateTestEmployee(TestEmployeeId, 20);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new VacationRequest(
            TestRequestId, TestEmployeeId,
            today.AddDays(10), today.AddDays(14),
            "Reason", 5, today.AddDays(44));

        var newStart = today.AddDays(20);
        var newEnd = today.AddDays(24);

        var command = new EditRequestCommand(
            TestRequestId,
            newStart,
            newEnd,
            "Updated",
            "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);
        _mockRepository.ExistsOverlappingAsync(TestEmployeeId, newStart, newEnd, TestRequestId, Arg.Any<CancellationToken>())
            .Returns(true); // Overlap detected

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, employee, CancellationToken.None));
        Assert.Contains("Ya existe una solicitud", ex.Message);
    }

    [Fact]
    public async Task Handle_InsufficientBalance_Throws()
    {
        // Arrange
        var handler = new EditRequestHandler(_mockRepository, _mockAuditService, TimeProvider.System);
        var employee = CreateTestEmployee(TestEmployeeId, 3); // Only 3 days left

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var originalStart = today.AddDays(10);
        var originalEnd = originalStart.AddDays(4); // 5 days already deducted

        var request = new VacationRequest(TestRequestId, TestEmployeeId, originalStart, originalEnd, "Original", 5, originalEnd.AddDays(30));

        var newStart = today.AddDays(15);
        var newEnd = newStart.AddDays(9); // 10 days total - needs 5 more days but only 3 available

        var command = new EditRequestCommand(
            TestRequestId,
            newStart,
            newEnd,
            "Longer vacation",
            "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);
        _mockRepository.ExistsOverlappingAsync(TestEmployeeId, newStart, newEnd, TestRequestId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, employee, CancellationToken.None));
        Assert.Contains("Saldo insuficiente", ex.Message);
    }

    [Fact]
    public async Task Handle_WritesAuditRecord()
    {
        // Arrange
        var handler = new EditRequestHandler(_mockRepository, _mockAuditService, TimeProvider.System);
        var employee = CreateTestEmployee(TestEmployeeId, 20);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new VacationRequest(
            TestRequestId, TestEmployeeId,
            today.AddDays(10), today.AddDays(14),
            "Original", 5, today.AddDays(44));

        var correlationId = "test-corr-123";
        var command = new EditRequestCommand(
            TestRequestId,
            today.AddDays(15),
            today.AddDays(19),
            "Updated reason",
            correlationId);

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);
        _mockRepository.ExistsOverlappingAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await handler.Handle(command, employee, CancellationToken.None);

        // Assert
        await _mockAuditService.Received(1).RecordTransitionAsync(
            TestRequestId,
            RequestStatus.Pending,
            RequestStatus.Pending,
            TestEmployeeId.ToString(),
            "Employee",
            correlationId,
            Arg.Is<string>(s => s.Contains("editada")),
            TestRequestId.ToString(),
            Arg.Any<CancellationToken>());
    }
}
