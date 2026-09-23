using System.Diagnostics.Metrics;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Infrastructure.Observability;

// Central definition of every NovaLeave-specific metric (spec_007 FR-005).
//
// Naming: instrument names are lowercase snake_case with a "novaleave_" prefix and use base
// units. Counters are deliberately NOT suffixed "_total" here -- the OpenTelemetry Prometheus
// exporter appends that suffix itself when it writes the exposition format, so naming them
// "..._total" would produce "..._total_total" in the scrape output.
//
// Cardinality: every label below comes from a closed set (research.md R-005). Employee ids,
// emails, names, request ids, reasons and raw URL paths are PROHIBITED as label values -- both
// because Constitution §7.3 classifies them as sensitive, and because an unbounded label grows
// Prometheus memory without limit. Do not add a label without checking it is bounded.
public sealed class NovaLeaveMetrics
{
    // Subscribed to by name in Program.cs. Changing this silently stops every custom metric.
    public const string MeterName = "NovaLeave";

    // Instrument names, held as constants so the gauge collector and the documentation
    // cannot drift from what is actually exported.
    public const string TransitionsInstrument = "novaleave_vacation_request_transitions";
    public const string AuthenticationInstrument = "novaleave_authentication_attempts";
    public const string PendingRequestsInstrument = "novaleave_vacation_requests_pending";
    public const string OldestPendingAgeInstrument = "novaleave_vacation_request_oldest_pending_age_seconds";

    private readonly Counter<long> _transitions;
    private readonly Counter<long> _authenticationAttempts;

    // Exposed so VacationRequestMetricsCollector registers its observable gauges on the SAME
    // meter. Creating a second meter with this name would work, but would split one logical
    // instrument set across two scopes for no benefit.
    // Public rather than internal so tests can attach a MetricCollector to it.
    public Meter Meter { get; }

    public NovaLeaveMetrics(IMeterFactory meterFactory)
    {
        // IMeterFactory rather than `new Meter(...)`: it ties the meter's lifetime to DI and is
        // what makes the meter observable from tests.
        var meter = meterFactory.Create(MeterName);
        Meter = meter;

        _transitions = meter.CreateCounter<long>(
            TransitionsInstrument,
            unit: "{transition}",
            description: "Vacation request state transitions, by originating and resulting status.");

        _authenticationAttempts = meter.CreateCounter<long>(
            AuthenticationInstrument,
            unit: "{attempt}",
            description: "Authentication attempts by outcome.");
    }

    // Records one vacation request state transition.
    // Both labels are RequestStatus enum values, so the label set is bounded at 6 x 6.
    public void RecordTransition(RequestStatus fromStatus, RequestStatus toStatus)
    {
        _transitions.Add(
            1,
            new KeyValuePair<string, object?>("from_status", fromStatus.ToString()),
            new KeyValuePair<string, object?>("to_status", toStatus.ToString()));
    }

    // Records one authentication attempt.
    //
    // `succeeded` is a bool by design, not a reason string. spec_004 requires that failures do
    // not reveal whether an account exists, is inactive or is locked out; a "reason" label would
    // republish exactly that distinction on an endpoint with no authentication in front of it.
    public void RecordAuthenticationAttempt(bool succeeded)
    {
        _authenticationAttempts.Add(
            1,
            new KeyValuePair<string, object?>("result", succeeded ? "success" : "failure"));
    }
}
