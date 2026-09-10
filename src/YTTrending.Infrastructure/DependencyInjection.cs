using YTTrending.Application.Common.Interfaces.Concurrency;
using YTTrending.Application.Common.Interfaces.Integrations;
using YTTrending.Application.Common.Interfaces.Jobs;
using YTTrending.Application.Common.Interfaces.Persistence;
using YTTrending.Infrastructure.Concurrency;
using YTTrending.Infrastructure.Jobs.Sync;
using YTTrending.Infrastructure.Persistence;
using YTTrending.Infrastructure.Persistence.Repositories;
using YTTrending.Infrastructure.YouTube;

namespace YTTrending.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<YTTrendingDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("Default"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<ISyncRunRepository, SyncRunRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IChannelSyncLock, ChannelSyncLock>();
        services.AddSingleton<ISyncRunQueue, SyncRunQueue>();
        services.AddSingleton<ISyncRunCreationLock, SyncRunCreationLock>();

        services.AddHostedService<SyncRunWorker>();

        if (configuration.GetValue("YouTube:UseFake", true))
        {
            services.AddSingleton<IYouTubeClient, FakeYouTubeClient>();
        }
        else
        {
            services.AddHttpClient<IYouTubeClient, YouTubeClient>(client =>
            {
                client.BaseAddress = new Uri(
                    "https://www.googleapis.com/youtube/v3/");
            });
        }

        return services;
    }
}
