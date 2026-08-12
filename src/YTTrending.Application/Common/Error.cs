
public enum ErrorType { Validation, NotFound, Conflict }

public record Error(string Code, ErrorType Type, string Message)
{
    public IReadOnlyDictionary<string, string[]>? Fields { get; init; }

    public static Error NotFound(string code, string msg) => new(code, ErrorType.NotFound, msg);
    public static Error Conflict(string code, string msg) => new(code, ErrorType.Conflict, msg);
    public static Error Validation(string code, string msg) => new(code, ErrorType.Validation, msg);

    public static Error Validation(IReadOnlyDictionary<string, string[]> fields) =>
        new("validation.failed", ErrorType.Validation, "Dữ liệu không hợp lệ") { Fields = fields };
}
