using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NovaLeave.Infrastructure.Identity;
using NovaLeave.Infrastructure.Persistence;

namespace NovaLeave.Application.Tests;

// spec_005 T106 / SC-005. These tests guard a deployment hazard, not a feature: automatic
// migration in Production would apply an unreviewed, irreversible schema change. The guard is
// therefore asserted directly rather than trusted to configuration.
public class DatabaseInitializerTests
{
    private readonly IDatabaseMigrator _migrator = Substitute.For<IDatabaseMigrator>();
    private readonly IDevelopmentDataSeeder _seeder = Substitute.For<IDevelopmentDataSeeder>();

    public DatabaseInitializerTests()
    {
        _migrator.SupportsMigrations.Returns(true);
        _migrator.DescribeTarget().Returns("server 'test', database 'test'");
    }

    private DatabaseInitializer CreateSut(
        string environmentName,
        DatabaseInitializerOptions? options = null)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName = environmentName;

        return new DatabaseInitializer(
            _migrator,
            _seeder,
            environment,
            TimeProvider.System,
            NullLogger<DatabaseInitializer>.Instance,
            Options.Create(options ?? new DatabaseInitializerOptions()));
    }

    [Fact]
    public async Task InitializeAsync_InProduction_DoesNotMigrate()
    {
        var sut = CreateSut(Environments.Production);

        await sut.InitializeAsync();

        await _migrator.DidNotReceive().MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_InProduction_DoesNotSeed()
    {
        var sut = CreateSut(Environments.Production);

        await sut.InitializeAsync();

        await _seeder.DidNotReceive().SeedAsync();
    }

    [Fact]
    public async Task InitializeAsync_InDevelopment_MigratesAndSeeds()
    {
        var sut = CreateSut(Environments.Development);

        await sut.InitializeAsync();

        await _migrator.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        await _seeder.Received(1).SeedAsync();
    }

    // Staging migrates because it is a non-Production environment, but must never receive the
    // seeded accounts: their passwords are published in the seeder source.
    [Fact]
    public async Task InitializeAsync_InStaging_MigratesButDoesNotSeed()
    {
        var sut = CreateSut(Environments.Staging);

        await sut.InitializeAsync();

        await _migrator.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        await _seeder.DidNotReceive().SeedAsync();
    }

    // The integration test suite runs on the InMemory provider, which rejects migration APIs.
    // Skipping keeps the container and the test host on one code path (FR-022).
    [Fact]
    public async Task InitializeAsync_WhenProviderDoesNotSupportMigrations_SkipsMigration()
    {
        _migrator.SupportsMigrations.Returns(false);
        var sut = CreateSut("Testing");

        await sut.InitializeAsync();

        await _migrator.DidNotReceive().MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_RetriesTransientFailures_WithinBudget()
    {
        var attempts = 0;
        _migrator.MigrateAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;
                return attempts < 3
                    ? Task.FromException(new TimeoutException("database not ready"))
                    : Task.CompletedTask;
            });

        var sut = CreateSut(
            Environments.Development,
            new DatabaseInitializerOptions
            {
                MaxAttempts = 5,
                DelayBetweenAttempts = TimeSpan.Zero
            });

        await sut.InitializeAsync();

        Assert.Equal(3, attempts);
        await _seeder.Received(1).SeedAsync();
    }

    // FR-007: exhausting the budget must fail loudly and name the target, never hang silently.
    [Fact]
    public async Task InitializeAsync_WhenRetryBudgetExhausted_ThrowsActionableError()
    {
        _migrator.MigrateAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new TimeoutException("database not ready")));

        var sut = CreateSut(
            Environments.Development,
            new DatabaseInitializerOptions
            {
                MaxAttempts = 3,
                DelayBetweenAttempts = TimeSpan.Zero
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.InitializeAsync());

        Assert.Contains("3 attempts", exception.Message);
        Assert.Contains("server 'test'", exception.Message);
        Assert.IsType<TimeoutException>(exception.InnerException);

        // A failed migration must not leave the app seeding into an unmigrated database.
        await _seeder.DidNotReceive().SeedAsync();
    }
}
