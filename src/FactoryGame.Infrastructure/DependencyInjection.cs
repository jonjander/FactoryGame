using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Hosting;
using FactoryGame.Infrastructure.Services;
using FactoryGame.Infrastructure.Options;
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

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<GuestAuthService>();
        services.AddScoped<ExchangeService>();
        services.AddScoped<BoardService>();
        services.AddScoped<AdminService>();
        services.AddHostedService<BaseIncomeBackgroundService>();
        services.AddHostedService<SimulationTickHostedService>();

        return services;
    }
}
