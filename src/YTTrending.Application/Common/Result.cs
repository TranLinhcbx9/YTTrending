
public interface IResult
{
    bool IsSuccess { get; }
    Error? Error { get; }
}

public sealed class Result : IResult
{
    public bool IsSuccess { get; }
    public Error? Error { get; }

    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);
}

public sealed class Result<T> : IResult
{
    private readonly T _value;

    public bool IsSuccess { get; }
    public Error? Error { get; }
    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException($"Result failed with error '{Error?.Code}' — cannot read Value.");

    private Result(bool isSuccess, T value, Error? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default!, error);
}
