using System.Reflection;

namespace YTTrending.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var list = validators as IReadOnlyList<IValidator<TRequest>> ?? validators.ToList();
        if (list.Count == 0) return await next();           // query không có validator — đi thẳng

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in list)                     // tuần tự — mỗi command 1 validator
        {
            var result = await validator.ValidateAsync(context, ct);
            if (!result.IsValid) failures.AddRange(result.Errors);
        }

        if (failures.Count == 0) return await next();

        var fields = failures
            .GroupBy(f => ToCamelCase(f.PropertyName))       // QĐ #4
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

        return CreateFailure(Error.Validation(fields));
    }

    /// <summary>PropertyName của FluentValidation là PascalCase; response còn lại đều camelCase (A19).</summary>
    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0])
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>
    /// Result và Result&lt;T&gt; đều sealed + private ctor + chỉ chung nhau IResult, nên không có
    /// đường generic nào dựng được TResponse lúc compile. Reflection chỉ chạy ở nhánh validate FAIL.
    /// </summary>
    private static TResponse CreateFailure(Error error)
    {
        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(error);

        var factory = typeof(TResponse).GetMethod(
            nameof(Result.Failure), BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"{typeof(TResponse)} không có static Failure(Error) — ValidationBehavior không dựng được kết quả lỗi.");

        return (TResponse)factory.Invoke(null, [error])!;
    }
}
