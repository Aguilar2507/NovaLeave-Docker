namespace NovaLeave.Infrastructure.Persistence;

// Seam over EF Core's migration API (spec_005 FR-005, FR-006).
// It exists so the Production guard in DatabaseInitializer can be unit-tested without a
// live database: applying migrations automatically is the one action that must never
// happen in a deployed environment by accident, so the guard needs a real test.
public interface IDatabaseMigrator
{
    // False for non-relational providers (the InMemory provider used by the test suite),
    // which build their schema from the model and reject migration APIs outright.
    bool SupportsMigrations { get; }

    // Applies every pending migration. Callers own the retry policy.
    Task MigrateAsync(CancellationToken cancellationToken = default);

    // Describes the migration target for log messages, with credentials removed.
    // Connection strings carry the password, so they are never logged verbatim
    // (Constitution §8: logs MUST NOT contain unnecessary sensitive data).
    string DescribeTarget();
}
