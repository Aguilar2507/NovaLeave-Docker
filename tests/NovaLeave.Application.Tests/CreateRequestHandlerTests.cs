using FluentValidation;
using FluentValidation.Results;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Application.Features.VacationRequest.Create;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Tests;

// T101: Handler tests for CreateRequestHandler — validates orchestration logic:
// identity check, balance check, overlap check, working-day calculation, audit invocation.
public class CreateRequestHandlerTests
{
    [Fact]
    public async Task Handle_validRequest_createsRequestAndReservesDays()
    {
        // Arrange
        var employee = CreateEmployee(balance: 10);
        var repository = Substitute.For<IVacationRequestRepository>();
        var workingDayCalc = Substitute.For<IWorkingDayCalculator>();
        var auditService = Substitute.For<IStateTransitionAuditService>();
        var validator = Substitute.For<IValidator<CreateRequestCommand>>();
        var timeProvider = TimeProvider.System;

        repository.ExistsOverlappingAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        workingDayCalc.Count(Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(5);
        validator.ValidateAsync(Arg.Any<CreateRequestCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var handler = new CreateRequestHandler(
            repository,
            workingDayCalc,
            auditService,
            validator,
            timeProvider);

        var command = new CreateRequestCommand(
            OwnerId: employee.Id,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14),
            Reason: "Vacaciones",
            CorrelationId: "test-corr-id");

        // Act
        var result = await handler.Handle(command, employee, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RequestStatus.Pending, result.Status);
        Assert.Equal(5, result.RequestedDays);
        await repository.Received(1).AddAsync(Arg.Any<Domain.Entities.VacationRequest>(), Arg.Any<CancellationToken>());
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_overlappingRequest_throws()
    {
        // Arrange
        var employee = CreateEmployee(balance: 10);
        var repository = Substitute.For<IVacationRequestRepository>();
        var workingDayCalc = Substitute.For<IWorkingDayCalculator>();
        var auditService = Substitute.For<IStateTransitionAuditService>();
        var validator = Substitute.For<IValidator<CreateRequestCommand>>();

        repository.ExistsOverlappingAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true); // overlap detectado
        validator.ValidateAsync(Arg.Any<CreateRequestCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var handler = new CreateRequestHandler(
            repository,
            workingDayCalc,
            auditService,
            validator,
            TimeProvider.System);

        var command = new CreateRequestCommand(
            OwnerId: employee.Id,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14),
            Reason: "Vacaciones",
            CorrelationId: "test-corr-id");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(command, employee, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_insufficientBalance_throws()
    {
        // Arrange
        var employee = CreateEmployee(balance: 2); // menos días que los solicitados
        var repository = Substitute.For<IVacationRequestRepository>();
        var workingDayCalc = Substitute.For<IWorkingDayCalculator>();
        var auditService = Substitute.For<IStateTransitionAuditService>();
        var validator = Substitute.For<IValidator<CreateRequestCommand>>();

        repository.ExistsOverlappingAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        workingDayCalc.Count(Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(5);
        validator.ValidateAsync(Arg.Any<CreateRequestCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var handler = new CreateRequestHandler(
            repository,
            workingDayCalc,
            auditService,
            validator,
            TimeProvider.System);

        var command = new CreateRequestCommand(
            OwnerId: employee.Id,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14),
            Reason: "Vacaciones",
            CorrelationId: "test-corr-id");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(command, employee, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_validationFails_throws()
    {
        // Arrange
        var employee = CreateEmployee(balance: 10);
        var repository = Substitute.For<IVacationRequestRepository>();
        var workingDayCalc = Substitute.For<IWorkingDayCalculator>();
        var auditService = Substitute.For<IStateTransitionAuditService>();
        var validator = Substitute.For<IValidator<CreateRequestCommand>>();

        validator.ValidateAsync(Arg.Any<CreateRequestCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("StartDate", "Debe ser futura") }));

        var handler = new CreateRequestHandler(
            repository,
            workingDayCalc,
            auditService,
            validator,
            TimeProvider.System);

        var command = new CreateRequestCommand(
            OwnerId: employee.Id,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), // pasado
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3),
            Reason: "Vacaciones",
            CorrelationId: "test-corr-id");

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(command, employee, CancellationToken.None));
    }

    private static Employee CreateEmployee(int balance = 20) =>
        new(
            id: Guid.NewGuid(),
            identityId: "identity-1",
            name: "Test User",
            email: "test@example.com",
            employmentStartDate: new DateOnly(2024, 1, 1),
            initialBalance: balance);
}
