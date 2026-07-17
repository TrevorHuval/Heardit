using Heardit.Areas.Identity.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Heardit.Tests.Infrastructure;

/// <summary>
/// A throwaway database backed by SQLite held entirely in memory, scoped to a single test.
///
/// We deliberately use the real SQLite provider rather than EF's InMemory provider:
/// <see cref="Heardit.Services.ProfileService.UnfollowAsync"/> calls <c>ExecuteDeleteAsync</c>,
/// which the InMemory provider cannot translate but SQLite runs for real.
///
/// An in-memory SQLite database only exists while at least one connection to it is open, so the
/// connection is opened once in the constructor and held open for the lifetime of this object.
/// Every <see cref="HearditDbContext"/> handed out shares that one connection, so they all observe
/// the same data. EF does not own an externally-opened connection, so disposing a context leaves it open.
///
/// NOTE: SQLite's SQL dialect is not PostgreSQL's — these are service-logic tests, not provider tests.
/// </summary>
public sealed class SqliteInMemoryDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<HearditDbContext> _options;

    public SqliteInMemoryDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<HearditDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>A fresh context over the shared connection. Use a new one per arrange/act/assert step so
    /// assertions read committed data rather than the change tracker's view.</summary>
    public HearditDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
