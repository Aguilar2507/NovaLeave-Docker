using NSubstitute;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Application.Features.VacationRequest.Reject;
using NovaLeave.Domain.Entities;
using FluentValidation;
using FluentValidation.Results;

namespace NovaLeave.Application.Tests;

// T201: Handler tests for RejectRequestHandler (US2, Phase 4).
public class RejectRequestHandlerTests
{
    private readonly IVacationRequestRepository _mockRepository;
    private readonly IStateTransitionAuditService _mockAuditService;
    private readonly IValidator<RejectRequestCommand> _mockValidator;
    private readonly RejectRequestHandler _handler;

    private static readonly Guid TestRequestId = Guid.NewGuid();
    private static readonly Guid TestEmployeeId = Guid.NewGuid();
    private static readonly Guid TestApproverId = Guid.NewGuid();
    private static readonly DateOnly FutureStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
    private static readonly DateOnly FutureEnd = FutureStart.AddDays(4);

    public RejectRequestHandlerTests()
    {
        _mockRepository = Substitute.For<IVacationRequestRepository>();
        _mockAuditService = Substitute.For<IStateTransitionAuditService>();
        _mockValidator = Substitute.For<IValidator<RejectRequestCommand>>();
        _mockValidator.ValidateAsync(Arg.Any<RejectRequestCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _handler = new RejectRequestHandler(_mockRepository, _mockAuditService, _mockValidator);
    }

    private static Employee CreateTestEmployee(int balance) =>
        new(TestEmployeeId, "test-id", "Test Emp", "test@test.com",
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1), balance, TestApproverId);

    [Fact]
    public async Task Handle_ValidRequest_RejectsAndReleasesReservation()
    {
        var employee = CreateTestEmployee(20);
        employee.ReserveDays(5);
        var request = new VacationRequest(TestRequestId, TestEmployeeId, FutureStart, FutureEnd, "Vacation", 5, FutureEnd.AddDays(30));
        var command = new RejectRequestCommand(TestRequestId, TestApproverId, "Insufficient staffing", "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns(request);

        await _handler.Handle(command, employee, CancellationToken.None);

        Assert.Equal(RequestStatus.Rejected, request.Status);
        Assert.Equal(20, employee.Balance);
        await _mockRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RequestNotFound_Throws()
    {
        var employee = CreateTestEmployee(20);
        var command = new RejectRequestCommand(TestRequestId, TestApproverId, "Some reason", "test-corr-id");

        _mockRepository.GetByIdAsync(TestRequestId, Arg.Any<CancellationToken>()).Returns((VacationRequest?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, employee, CancellationToken.None));
    }
}
