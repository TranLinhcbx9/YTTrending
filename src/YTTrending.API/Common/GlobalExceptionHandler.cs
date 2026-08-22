using Microsoft.AspNetCore.Diagnostics;

namespace YTTrending.API.Common;

// cách .NET 8 (IExceptionHandler) thay middleware try/catch tự viết.
// Bắt exception chưa xử lý -> log -> 500 ProblemDetails, KHÔNG lộ stack ra client.
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception on {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Server Error",
            Detail = "Đã có lỗi xảy ra, vui lòng thử lại.",
        };
        problem.Extensions["code"] = "server.error";

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }
}
