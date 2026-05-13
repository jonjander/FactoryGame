using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Infrastructure.Data;

public interface IDatabaseSchemaInitializer
{
    Task EnsureSchemaAsync(AppDbContext db, CancellationToken cancellationToken = default);
}

internal sealed class NpgsqlMigrateDatabaseSchemaInitializer : IDatabaseSchemaInitializer
{
    public Task EnsureSchemaAsync(AppDbContext db, CancellationToken cancellationToken = default) =>
        db.Database.MigrateAsync(cancellationToken);
}

internal sealed class SqliteEnsureCreatedDatabaseSchemaInitializer : IDatabaseSchemaInitializer
{
    public Task EnsureSchemaAsync(AppDbContext db, CancellationToken cancellationToken = default) =>
        db.Database.EnsureCreatedAsync(cancellationToken);
}
