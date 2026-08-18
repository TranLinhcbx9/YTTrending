using Microsoft.AspNetCore.Diagnostics;

namespace YTTrending.API.Common;

// cách .NET 8 (IExceptionHandler) thay middleware try/catch tự viết.
// Bắt exception chưa xử lý -> log -> 500 body gọn, KHÔNG lộ stack ra client.
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception on {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            code = "server.error",
            type = "ServerError",
            message = "Đã có lỗi xảy ra, vui lòng thử lại."
        }, cancellationToken);

        return true; // đã xử lý -> không ném tiếp
    }
}
