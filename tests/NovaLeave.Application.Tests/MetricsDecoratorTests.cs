using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.DependencyInjection;
using NovaLeave.Application.Features.Authentication.Contracts;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Observability;

namespace NovaLeave.Application.Tests;

// spec_007 T205. These tests protect two properties that matter more than the metrics
// themselves: the decorators must be transparent (FR-010), and they must never be able to break
// the operation they observe (FR-011). Instrumentation that can cause an incident is worse than
// no instrumentation.
public class MetricsDecoratorTests
{
    // A real IMeterFactory from DI: the framework's implementation is internal, and these tests
    // are worth little if they exercise a hand-rolled substitute instead of the real one.
    private static NovaLeaveMetrics CreateMetrics()
    {
        var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        return new NovaLeaveMetrics(provider.GetRequiredService<IMeterFactory>());
    }

    // ---- Transparency -------------------------------------------------------------------

    [Fact]
    public async Task StateTransitionDecorator_DelegatesWithUnchangedArguments()
    {
        var inner = Substitute.For<IStateTransitionAuditService>();
        var sut = new MetricsStateTransitionAuditService(inner, CreateMetrics());

        var requestId = Guid.NewGuid();
        var cancellationToken = new CancellationToken();

        await sut.RecordTransitionAsync(
            requestId,
            RequestStatus.Pending,
            RequestStatus.Approved,
            actorId: "actor-1",
            actorRole: "Approver",
            correlationId: "corr-1",
            reason: "because",
            requestId2: "req-1",
            cancellationToken: cancellationToken);

        await inner.Received(1).RecordTransitionAsync(
            requestId,
            RequestStatus.Pending,
            RequestStatus.Approved,
            "actor-1",
            "Approver",
            "corr-1",
            "because",
            "req-1",
            cancellationToken);
    }

    [Fact]
    public async Task AuthenticationDecorator_DelegatesEveryMember()
    {
        var inner = Substitute.For<IAuthenticationAuditService>();
        var sut = new MetricsAuthenticationAuditService(inner, CreateMetrics());

        await sut.LogLoginSuccessAsync("user-1", "user@novaleave.local", "User");
        await sut.LogLoginFailureAsync("user@novaleave.local", "user-1", "InactiveAccount");
        await sut.LogLogoutAsync("user-1", "user@novaleave.local");
        await sut.LogAccountLockedAsync("user-1", "user@novaleave.local");

        await inner.Received(1).LogLoginSuccessAsync("user-1", "user@novaleave.local", "User", Arg.Any<CancellationToken>());
        await inner.Received(1).LogLoginFailureAsync("user@novaleave.local", "user-1", "InactiveAccount", Arg.Any<CancellationToken>());
        await inner.Received(1).LogLogoutAsync("user-1", "user@novaleave.local", Arg.Any<CancellationToken>());
        await inner.Received(1).LogAccountLockedAsync("user-1", "user@novaleave.local", Arg.Any<CancellationToken>());
    }

    // ---- Failure isolation (FR-011) -----------------------------------------------------

    // The metrics object is disposed, so every instrument call throws. The audit must still
    // happen and the caller must see no error.
    [Fact]
    public async Task StateTransitionDecorator_WhenMetricsThrow_AuditStillSucceeds()
    {
        var inner = Substitute.For<IStateTransitionAuditService>();
        var metrics = CreateMetrics();
        metrics.Meter.Dispose();

        var sut = new MetricsStateTransitionAuditService(inner, metrics);

        var exception = await Record.ExceptionAsync(() => sut.RecordTransitionAsync(
            Guid.NewGuid(),
            RequestStatus.Pending,
            RequestStatus.Rejected,
            "actor-1",
            "Approver",
            "corr-1"));

        Assert.Null(exception);
        await inner.Received(1).RecordTransitionAsync(
            Arg.Any<Guid>(), Arg.Any<RequestStatus>(), Arg.Any<RequestStatus>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthenticationDecorator_WhenMetricsThrow_AuditStillSucceeds()
    {
        var inner = Substitute.For<IAuthenticationAuditService>();
        var metrics = CreateMetrics();
        metrics.Meter.Dispose();

        var sut = new MetricsAuthenticationAuditService(inner, metrics);

        var exception = await Record.ExceptionAsync(
            () => sut.LogLoginSuccessAsync("user-1", "user@novaleave.local", "User"));

        Assert.Null(exception);
        await inner.Received(1).LogLoginSuccessAsync(
            "user-1", "user@novaleave.local", "User", Arg.Any<CancellationToken>());
    }

    // A failing audit is a real failure and MUST surface -- the decorator must not swallow it.
    // This is the inverse of the test above and guards against over-broad exception handling.
    [Fact]
    public async Task StateTransitionDecorator_WhenAuditThrows_ExceptionPropagates()
    {
        var inner = Substitute.For<IStateTransitionAuditService>();
        inner.RecordTransitionAsync(
                Arg.Any<Guid>(), Arg.Any<RequestStatus>(), Arg.Any<RequestStatus>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("audit failed")));

        var sut = new MetricsStateTransitionAuditService(inner, CreateMetrics());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RecordTransitionAsync(
            Guid.NewGuid(),
            RequestStatus.Pending,
            RequestStatus.Approved,
            "actor-1",
            "Approver",
            "corr-1"));
    }

    // ---- Recorded values ----------------------------------------------------------------

    [Fact]
    public void RecordTransition_EmitsBoundedStatusLabelsOnly()
    {
        var metrics = CreateMetrics();
        using var collector = new MetricCollector<long>(
            metrics.Meter, NovaLeaveMetrics.TransitionsInstrument);

        metrics.RecordTransition(RequestStatus.Pending, RequestStatus.Approved);

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);

        var tags = measurement.Tags.ToDictionary(tag => tag.Key, tag => tag.Value?.ToString());
        Assert.Equal("Pending", tags["from_status"]);
        Assert.Equal("Approved", tags["to_status"]);

        // Exactly two labels. A new one must be a conscious decision, checked for cardinality
        // and PII (research.md R-005), not something that slips in.
        Assert.Equal(2, tags.Count);
    }

    // FR-009: the failure reason must never reach the metric. /metrics is unauthenticated, so a
    // reason label would undo spec_004's existence hiding.
    [Theory]
    [InlineData(true, "success")]
    [InlineData(false, "failure")]
    public void RecordAuthenticationAttempt_EmitsResultOnly(bool succeeded, string expected)
    {
        var metrics = CreateMetrics();
        using var collector = new MetricCollector<long>(
            metrics.Meter, NovaLeaveMetrics.AuthenticationInstrument);

        metrics.RecordAuthenticationAttempt(succeeded);

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        var tags = measurement.Tags.ToDictionary(tag => tag.Key, tag => tag.Value?.ToString());

        Assert.Equal(expected, tags["result"]);
        Assert.Single(tags);
    }
}
