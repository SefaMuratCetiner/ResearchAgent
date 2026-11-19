using System.Diagnostics.CodeAnalysis;

namespace Research.SharedKernel.Results;

/// <summary>The outcome of an operation the business expects to fail sometimes: success, or one of the service's errors.</summary>
public sealed class Result
{
    private static readonly Result SuccessInstance = new(null);

    private Result(Error? error)
    {
        Error = error;
    }

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => SuccessInstance;

    public static Result Failure(Error error) => new(error);
}

/// <summary>The outcome of an operation the business expects to fail sometimes: a value, or one of the service's errors.</summary>
public sealed class Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) => new(default, error);
}
