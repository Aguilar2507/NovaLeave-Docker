using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NovaLeave.Infrastructure.Identity;

namespace NovaLeave.Infrastructure.Persistence;

// Brings the database to a usable state at startup: applies pending EF Core migrations and,
// in Development only, seeds the test accounts (spec_005 FR-005 through FR-008).
//
// This exists because a container starts with an empty database. Before spec_005, migrations
// were only ever applied by hand against LocalDB, so a fresh SQL Server container had no
// schema and the seeder failed on first run.
public sealed class DatabaseInitializer
{
    private readonly IDatabaseMigrator _migrator;
    private readonly IDevelopmentDataSeeder _seeder;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DatabaseInitializer> _logger;
    private readonly DatabaseInitializerOptions _options;

    public DatabaseInitializer(
        IDatabaseMigrator migrator,
        IDevelopmentDataSeeder seeder,
        IHostEnvironment environment,
        TimeProvider timeProvider,
        ILogger<DatabaseInitializer> logger,
        IOptions<DatabaseInitializerOptions> options)
    {
        _migrator = migrator;
        _seeder = seeder;
        _environment = environment;
        _timeProvider = timeProvider;
        _logger = logger;
        _options = options.Value;
    }

    // Preconditions: none. Side effects: schema changes and, in Development, account creation.
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Hard guard (FR-006). Automatic migration couples schema change to deployment, runs
        // without review and has no rollback path, so Production is refused here in code.
        // A mistyped environment variable or a stray config file must not be able to enable it;
        // the production migration strategy is a separate, deliberate step (GAP-005-3).
        if (_environment.IsProduction())
        {
            _logger.LogInformation(
                "Automatic database initialization skipped: environment is Production. " +
                "Migrations must be applied through the reviewed deployment process.");
            return;
        }

        await MigrateAsync(cancellationToken).ConfigureAwait(false);

        // Seeded accounts use known, published passwords. They are valid only in Development
        // and must never be created anywhere else, including Staging or Testing.
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("Seeding development data.");
            await _seeder.SeedAsync().ConfigureAwait(false);
        }
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        // The InMemory provider used by the integration test suite builds its schema from the
        // model and rejects migration APIs. Skipping keeps container startup and the test host
        // on the same code path instead of forcing a test-only branch (FR-022).
        if (!_migrator.SupportsMigrations)
        {
            _logger.LogInformation(
                "Skipping migrations: provider '{Provider}' does not support them.",
                _migrator.DescribeTarget());
            return;
        }

        var maxAttempts = Math.Max(1, _options.MaxAttempts);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Applying pending migrations to {Target} (attempt {Attempt}/{MaxAttempts}).",
                    _migrator.DescribeTarget(), attempt, maxAttempts);

                await _migrator.MigrateAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Database migrations applied successfully.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                // A container database commonly refuses connections for the first few seconds,
                // so early failures are expected rather than exceptional. Logged at Warning so a
                // genuinely broken configuration is still visible while it retries.
                _logger.LogWarning(
                    ex,
                    "Database not ready on attempt {Attempt}/{MaxAttempts}; retrying in {Delay}.",
                    attempt, maxAttempts, _options.DelayBetweenAttempts);

                await Task.Delay(_options.DelayBetweenAttempts, _timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Budget exhausted. Fail loudly and name the target: a silent hang here is the
                // single most confusing failure mode of a containerized stack (FR-007).
                throw new InvalidOperationException(
                    $"Database initialization failed after {maxAttempts} attempts against " +
                    $"{_migrator.DescribeTarget()}. Verify the database container is running and " +
                    "healthy, and that the connection string and credentials are correct.",
                    ex);
            }
        }
    }
}
