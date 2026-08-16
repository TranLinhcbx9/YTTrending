using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace YTTrending.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var start = Stopwatch.GetTimestamp();

        var response = await next();                        // exception xuyên qua — xem QĐ #2

        var ms = (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        if (response is IResult { IsSuccess: false } failed)
            logger.LogWarning("{Request} FAIL {Code} ({Elapsed}ms)", name, failed.Error!.Code, ms);
        else
            logger.LogInformation("{Request} OK ({Elapsed}ms)", name, ms);

        return response;
    }
}
