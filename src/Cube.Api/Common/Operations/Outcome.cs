namespace Cube.Api.Common.Operations;

public enum FailureKind { Invalid, NotFound, Forbidden, Conflict }

public sealed record Failure(FailureKind Kind, string Code)
{
    public static Failure Invalid(string code) => new(FailureKind.Invalid, code);
    public static Failure NotFound(string code) => new(FailureKind.NotFound, code);
    public static Failure Forbidden(string code) => new(FailureKind.Forbidden, code);
    public static Failure Conflict(string code) => new(FailureKind.Conflict, code);
}

// Expected business failures carry no HTTP types or status codes.
public sealed class Outcome<T>
{
    private readonly T? value;
    private Outcome(T? value, Failure? error) { this.value = value; Error = error; }
    public Failure? Error { get; }
    public T Value => Error is null ? value! : throw new InvalidOperationException("A failed operation has no value.");
    public static Outcome<T> Success(T value) => new(value, null);
    public static Outcome<T> Reject(Failure error) => new(default, error);
}
