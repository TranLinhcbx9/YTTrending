using YTTrending.Infrastructure.Persistence;

namespace YTTrending.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // YTTrendingDbContext nhận TimeProvider ở tham số thứ 2 -> phải có trong DI
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<YTTrendingDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("Default"))
            .UseSnakeCaseNamingConvention());

        // Thiếu dòng này thì build vẫn sạch nhưng handler inject IYTTrendingDbContext sẽ vỡ DI lúc chạy
        services.AddScoped<IYTTrendingDbContext>(sp => sp.GetRequiredService<YTTrendingDbContext>());

        return services;
    }
}
