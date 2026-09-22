namespace NovaLeave.Infrastructure.Persistence;

// Bounded retry budget for startup migration (spec_005 FR-007).
// Configurable because the window that matters varies by host: SQL Server running under
// amd64 emulation on an arm64 machine takes appreciably longer to become query-ready.
public sealed class DatabaseInitializerOptions
{
    public const string SectionName = "DatabaseInitializer";

    // Total attempts, not additional retries. Defaults give ~45s of tolerance, which
    // covers a cold container start without letting a genuine misconfiguration hang.
    public int MaxAttempts { get; set; } = 10;

    public TimeSpan DelayBetweenAttempts { get; set; } = TimeSpan.FromSeconds(5);
}
