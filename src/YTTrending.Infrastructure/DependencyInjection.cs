using System;
using YTTrending.Infrastructure.Persistence;

namespace YTTrending.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // AppDbContext nhận TimeProvider ở tham số thứ 2 -> phải có trong DI
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<YTTrendingDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("Default"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IYTTrendingDbContext>(sp => sp.GetRequiredService<YTTrendingDbContext>());

        return services;
    }
}
