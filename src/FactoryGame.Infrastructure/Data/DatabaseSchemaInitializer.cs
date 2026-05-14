using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Infrastructure.Data;

public interface IDatabaseSchemaInitializer
{
    Task EnsureSchemaAsync(AppDbContext db, CancellationToken cancellationToken = default);
}

internal sealed class SqliteDatabaseSchemaInitializer : IDatabaseSchemaInitializer
{
    public Task EnsureSchemaAsync(AppDbContext db, CancellationToken cancellationToken = default) =>
        db.Database.EnsureCreatedAsync(cancellationToken);
}
