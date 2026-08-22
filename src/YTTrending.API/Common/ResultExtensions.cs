using YTTrending.Application.Common.Models;

namespace YTTrending.API.Common;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result) =>
        result.IsSuccess ? new NoContentResult() : ToObjectResult(result.Error!.ToProblemDetails());

    public static IActionResult ToActionResult<T>(this Result<T> result) =>
        result.IsSuccess ? new OkObjectResult(result.Value) : ToObjectResult(result.Error!.ToProblemDetails());

    public static ProblemDetails ToProblemDetails(this Error error)
    {
        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Validation failed"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not Found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Server Error"),
        };

        ProblemDetails problem = error.Fields is not null
            ? new ValidationProblemDetails(error.Fields.ToDictionary(kv => kv.Key, kv => kv.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = title;
        problem.Detail = error.Message;
        problem.Extensions["code"] = error.Code;
        return problem;
    }

    private static ObjectResult ToObjectResult(ProblemDetails problem) =>
        new(problem) { StatusCode = problem.Status };
}
