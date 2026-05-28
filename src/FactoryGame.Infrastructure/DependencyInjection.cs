using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Hosting;
using FactoryGame.Infrastructure.Options;
using FactoryGame.Infrastructure.Services;
using FactoryGame.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryGame.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GameEconomyOptions>(configuration.GetSection(GameEconomyOptions.SectionName));
        services.Configure<MarketLiquidityOptions>(configuration.GetSection(MarketLiquidityOptions.SectionName));
        services.Configure<SponsorCompanyOptions>(configuration.GetSection(SponsorCompanyOptions.SectionName));
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));

        var dbResolution = DbConnectionResolver.Resolve(configuration.GetConnectionString("DefaultConnection"));
        services.AddSingleton(dbResolution);

        if (dbResolution.PinSharedMemoryDatabase)
        {
            var pin = new SqliteSharedMemoryDatabasePin(dbResolution.ConnectionString);
            services.AddSingleton(pin);
        }

        services.AddDbContext<AppDbContext>(options =>
            DbContextOptionsConfigurator.Configure(options, dbResolution));

        services.AddSingleton<IDatabaseSchemaInitializer>(dbResolution.Provider switch
        {
            DatabaseProvider.SqlServer => new SqlServerDatabaseSchemaInitializer(),
            DatabaseProvider.Sqlite => new SqliteDatabaseSchemaInitializer(),
            _ => throw new InvalidOperationException($"Unsupported database provider: {dbResolution.Provider}.")
        });

        services.AddScoped<PlayerPoolBootstrapService>();
        services.AddScoped<GuestAuthService>();
        services.AddScoped<ExchangeService>();
        services.AddScoped<MarketLiquidityService>();
        services.AddScoped<MarketQueryService>();
        services.AddHostedService<MarketLiquidityHostedService>();
        services.AddScoped<BoardService>();
        services.AddScoped<BoardSimulationRunner>();
        services.AddScoped<MachineInventoryService>();
        services.AddScoped<AdminService>();
        services.AddScoped<SponsorCompanyService>();
        services.AddScoped<SponsorCompanyTradingService>();
        services.AddHostedService<SponsorCompanyTradingHostedService>();
        services.AddHostedService<BaseIncomeBackgroundService>();
        services.AddHostedService<SimulationTickHostedService>();

        return services;
    }
}
