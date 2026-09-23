using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Identity;

namespace NovaLeave.Infrastructure.Observability;

// Exports queue-state gauges for vacation requests (spec_007 T203, FR-007, FR-008).
//
// WHY A BACKGROUND SERVICE RATHER THAN A QUERY PER SCRAPE
// research.md R-003 originally chose to query the database inside the ObservableGauge callback.
// That is not implementable here: ObservableGauge callbacks are synchronous, so it would require
// either sync-over-async or EF Core's blocking APIs, and Constitution §2.VIII prohibits both
// ("all I/O uses async/await end-to-end; .Result, .Wait(), sync-over-async prohibited").
//
// Refreshing asynchronously on a timer and letting the callbacks read a cached snapshot
// satisfies §2.VIII and is strictly safer: the scrape path performs no I/O at all, so a slow or
// unavailable database can never stall a scrape. The cost is bounded staleness -- at most one
// refresh interval -- which is immaterial for a queue-depth signal.
//
// Schedule: refreshes every RefreshInterval. Failure mode: the snapshot is cleared and the
// gauges report no measurement, so Prometheus records a visible gap rather than a fabricated
// zero (FR-012).
public sealed class VacationRequestMetricsCollector : BackgroundService
{
    // Matches the default Prometheus scrape interval. Refreshing faster would burn queries that
    // nobody reads; slower would make the gauges visibly lag the dashboard.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);

    // A refresh that outlives this is treated as a failure. Keeps a degraded database from
    // holding the loop open indefinitely.
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<VacationRequestMetricsCollector> _logger;

    // Immutable snapshot swapped atomically. `volatile` gives the gauge callbacks -- which run
    // on the scrape thread -- a consistent view without locking.
    private volatile QueueSnapshot? _snapshot;

    public VacationRequestMetricsCollector(
        NovaLeaveMetrics metrics,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<VacationRequestMetricsCollector> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ArgumentNullException.ThrowIfNull(metrics);

        metrics.Meter.CreateObservableGauge(
            NovaLeaveMetrics.PendingRequestsInstrument,
            ObservePendingCount,
            unit: "{request}",
            description: "Vacation requests currently awaiting approver action.");

        metrics.Meter.CreateObservableGauge(
            NovaLeaveMetrics.OldestPendingAgeInstrument,
            ObserveOldestPendingAge,
            unit: "s",
            description: "Age of the oldest vacation request still awaiting approver action.");
    }

    // No labels on either gauge: these describe the queue as a whole. Adding an employee
    // dimension would create one series per employee and expose who has requests outstanding
    // (Constitution §7.3, research.md R-005).
    private IEnumerable<Measurement<long>> ObservePendingCount()
    {
        var snapshot = _snapshot;

        // No measurement rather than zero. Zero would assert "the queue is empty", which is a
        // different claim from "the queue is currently unknown".
        if (snapshot is null)
        {
            yield break;
        }

        yield return new Measurement<long>(snapshot.PendingCount);
    }

    private IEnumerable<Measurement<double>> ObserveOldestPendingAge()
    {
        var snapshot = _snapshot;

        // Absent when the queue is empty (FR-008). Reporting 0 would be indistinguishable from
        // "a request was created one second ago" -- a meaningfully different situation.
        if (snapshot?.OldestPendingAgeSeconds is not { } age)
        {
            yield break;
        }

        yield return new Measurement<double>(age);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval, _timeProvider);

        // Refresh once immediately so the gauges have data before the first scrape rather than
        // reporting nothing for the first interval.
        await RefreshAsync(stoppingToken).ConfigureAwait(false);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await RefreshAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task RefreshAsync(CancellationToken stoppingToken)
    {
        try
        {
            // TimeProvider-driven timeout (Constitution §2.VI), linked to shutdown so the refresh
            // stops for either reason.
            using var timeoutSource = new CancellationTokenSource(QueryTimeout, _timeProvider);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken, timeoutSource.Token);

            // ApplicationDbContext is scoped; this service is a singleton. Resolving it from the
            // root provider would throw, so a scope is created per refresh.
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var pending = dbContext.VacationRequests
                .Where(request => request.Status == RequestStatus.Pending);

            var pendingCount = await pending
                .CountAsync(linkedSource.Token)
                .ConfigureAwait(false);

            double? oldestAgeSeconds = null;

            if (pendingCount > 0)
            {
                var oldestCreatedAt = await pending
                    .MinAsync(request => request.CreatedAt, linkedSource.Token)
                    .ConfigureAwait(false);

                // TimeProvider rather than DateTimeOffset.UtcNow, per Constitution §2.VI.
                var age = _timeProvider.GetUtcNow() - oldestCreatedAt;

                // Clamp: a clock adjustment must not produce a negative age.
                oldestAgeSeconds = Math.Max(0, age.TotalSeconds);
            }

            _snapshot = new QueueSnapshot(pendingCount, oldestAgeSeconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Clearing the snapshot is deliberate: stale queue depth presented as current is
            // worse than an visible gap, because someone would act on it.
            _snapshot = null;

            _logger.LogWarning(
                ex,
                "Could not refresh vacation request queue metrics; gauges will report no value until the next successful refresh.");
        }
    }

    private sealed record QueueSnapshot(long PendingCount, double? OldestPendingAgeSeconds);
}
