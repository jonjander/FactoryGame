using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Hosting;
using FactoryGame.Infrastructure.Options;
using FactoryGame.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryGame.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GameEconomyOptions>(configuration.GetSection(GameEconomyOptions.SectionName));
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));

        var dbResolution = DbConnectionResolver.Resolve(configuration.GetConnectionString("DefaultConnection"));
        services.AddSingleton(dbResolution);

        services.AddDbContext<AppDbContext>(options =>
        {
            if (dbResolution.IsSqlite)
                options.UseSqlite(dbResolution.ConnectionString);
            else
                options.UseNpgsql(dbResolution.ConnectionString);
        });

        services.AddSingleton<IDatabaseSchemaInitializer>(_ =>
            dbResolution.UseEnsureCreatedInsteadOfMigrate
                ? new SqliteEnsureCreatedDatabaseSchemaInitializer()
                : new NpgsqlMigrateDatabaseSchemaInitializer());

        services.AddScoped<GuestAuthService>();
        services.AddScoped<ExchangeService>();
        services.AddScoped<BoardService>();
        services.AddScoped<AdminService>();
        services.AddHostedService<BaseIncomeBackgroundService>();
        services.AddHostedService<SimulationTickHostedService>();

        return services;
    }
}
