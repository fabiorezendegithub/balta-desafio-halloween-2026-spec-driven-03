namespace Encurtador.Application.Links;

public enum ErrorKind
{
    Validation,
    NotFound,
    Expired,
    Unauthorized,
    RateLimited,
}

public sealed record LinkError(ErrorKind Kind, string Message);

public sealed class LinkResult<T>
{
    private LinkResult(T? value, LinkError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public LinkError? Error { get; }

    public bool IsSuccess => Error is null;

    public static LinkResult<T> Success(T value) => new(value, null);

    public static LinkResult<T> Failure(LinkError error) => new(default, error);

    public static LinkResult<T> Failure(ErrorKind kind, string message) =>
        new(default, new LinkError(kind, message));
}
