namespace PRN232.Api.Contracts;

public sealed record ApiErrorResponse(
    bool Success,
    string Code,
    string Message,
    IReadOnlyList<ApiFieldError>? Errors,
    DateTime Timestamp,
    string Path)
{
    public static ApiErrorResponse Create(
        string code,
        string message,
        string path,
        IReadOnlyList<ApiFieldError>? errors = null) =>
        new(false, code, message, errors, DateTime.Now, path);
}

public sealed record ApiFieldError(string Field, string? Message);
