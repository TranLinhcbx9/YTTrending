using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YTTrending.Application.Common.Behaviors;
using YTTrending.Application.Common.Options;

namespace YTTrending.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);

            // Thứ tự = thứ tự bọc: Logging đăng ký trước -> nằm ngoài -> log được cả nhánh
            // validation FAIL. Đảo 2 dòng này là mất dòng log "FAIL validation.failed".
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // ValidationBehavior resolve IEnumerable<IValidator<TRequest>> từ đây. Chưa có validator
        // nào (Features ở mục 6) -> list rỗng, behavior đã có nhánh count == 0 đi thẳng.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // ValidateOnStart: sai config -> app chết lúc khởi động, không chết lúc job chạy 3h sáng.
        services.AddOptions<TrackingOptions>()
            .Bind(configuration.GetSection(TrackingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<TrendingOptions>()
            .Bind(configuration.GetSection(TrendingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JobOptions>()
            .Bind(configuration.GetSection(JobOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<YouTubeOptions>()
            .Bind(configuration.GetSection(YouTubeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
