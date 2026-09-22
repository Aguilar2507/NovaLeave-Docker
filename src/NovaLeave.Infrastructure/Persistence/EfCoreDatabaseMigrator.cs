using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NovaLeave.Infrastructure.Identity;

namespace NovaLeave.Infrastructure.Persistence;

// EF Core implementation of IDatabaseMigrator. The only production implementation;
// the interface exists for the testability of the Production guard, not for variation.
public sealed class EfCoreDatabaseMigrator : IDatabaseMigrator
{
    private readonly ApplicationDbContext _context;

    public EfCoreDatabaseMigrator(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool SupportsMigrations => _context.Database.IsRelational();

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        _context.Database.MigrateAsync(cancellationToken);

    public string DescribeTarget()
    {
        // Non-relational providers have no connection string at all; degrade to the
        // provider name rather than throwing while composing an error message.
        if (!SupportsMigrations)
        {
            return _context.Database.ProviderName ?? "unknown provider";
        }

        var connectionString = _context.Database.GetConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "unknown target";
        }

        try
        {
            // Project only server and database. Echoing the raw connection string would
            // leak the SA password into logs the moment initialization fails.
            var builder = new SqlConnectionStringBuilder(connectionString);
            return $"server '{builder.DataSource}', database '{builder.InitialCatalog}'";
        }
        catch (ArgumentException)
        {
            // A malformed connection string must not be echoed back either: it may still
            // contain a readable password even though it failed to parse.
            return "unparseable connection string";
        }
    }
}
