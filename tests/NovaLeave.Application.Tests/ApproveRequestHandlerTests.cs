using NSubstitute;
using NovaLeave.Application.Features.VacationRequest.Approve;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Tests;

// T201: Handler tests for ApproveRequestHandler (US2, Phase 4).
public class ApproveRequestHandlerTests
{
    private readonly IVacationRequestRepository _mockRepository;
    private readonly IStateTransitionAuditService _mockAuditService;
    private readonly ApproveRequestHandler _handler;

    private static readonly Guid TestRequestId = Guid.NewGuid();
    private static readonly Guid TestEmployeeId = Guid.NewGuid();
    private static readonly Guid TestApproverId = Guid.NewGuid();
    private static readonly DateOnly FutureStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
    private static readonly DateOnly FutureEnd = FutureStart.AddDays(4);

    public ApproveRequestHandlerTests()
    {
        _mockRepository = Substitute.For<IVacationRequestRepository>();
        _mockAuditService = Substitute.For<IStateTransitionAuditService>();
        _handler = new ApproveRequestHandler(_mockRepository, _mockAuditService);
    }

    private static Employee CreateTestEmployee(int balance) =>
        new(TestEmployeeId, "test-id", "Test Emp", "test@test.com",
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1), balance, TestApproverId);

    [Fact]
    public async Task Handle_ValidRequest_ApprovesAndDeductsBalance()
    {
        var employee = CreateTestEmployee(20);
        var request = new VacationRequest(TestRequestId, TestEmployeeId, FutureStart, FutureEnd, "Vacation", 5, FutureEnd.AddDays(30));
        var command = new ApproveRequestCommand(TestRequestId, TestApproverId, "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        await _handler.Handle(command, employee, CancellationToken.None);

        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Equal(15, employee.Balance);
        await _mockRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InsufficientBalance_Throws()
    {
        var employee = CreateTestEmployee(3);
        var request = new VacationRequest(TestRequestId, TestEmployeeId, FutureStart, FutureEnd, "Vacation", 5, FutureEnd.AddDays(30));
        var command = new ApproveRequestCommand(TestRequestId, TestApproverId, "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, employee, CancellationToken.None));

        Assert.Contains("saldo insuficiente", ex.Message.ToLower());
    }

    [Fact]
    public async Task Handle_RequestNotFound_Throws()
    {
        var employee = CreateTestEmployee(20);
        var command = new ApproveRequestCommand(TestRequestId, TestApproverId, "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns((VacationRequest?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, employee, CancellationToken.None));
    }
}
