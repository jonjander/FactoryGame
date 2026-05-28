using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Infrastructure.Data;

internal static class DbContextOptionsConfigurator
{
    public static void Configure(DbContextOptionsBuilder options, DbConnectionResolution resolution)
    {
        switch (resolution.Provider)
        {
            case DatabaseProvider.SqlServer:
                options.UseSqlServer(resolution.ConnectionString, sql =>
                    sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null));
                break;
            case DatabaseProvider.Sqlite:
                options.UseSqlite(resolution.ConnectionString);
                break;
            default:
                throw new InvalidOperationException($"Unsupported database provider: {resolution.Provider}.");
        }
    }
}
