using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FactoryGame.Infrastructure.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var resolution = DbConnectionResolver.Resolve(string.IsNullOrWhiteSpace(conn) ? null : conn);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(resolution.ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
