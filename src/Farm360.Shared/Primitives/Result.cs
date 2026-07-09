namespace Farm360.Shared.Primitives;

/// <summary>
/// Represents a discriminated union of success or failure.
/// Constitution §3: Use Result pattern instead of throwing exceptions for expected failures.
/// Never throw for business logic failures — only for programming errors.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A success result cannot have an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failure result must have an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    /// <summary>Creates a success Result if value is non-null; otherwise a failure.</summary>
    public static Result<TValue> Create<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure<TValue>(Error.NullValue);
}

/// <summary>
/// Generic Result with a strongly-typed value.
/// </summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the value. Throws <see cref="InvalidOperationException"/> on failure.
    /// Pattern: always check IsSuccess before accessing Value.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failure result.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
