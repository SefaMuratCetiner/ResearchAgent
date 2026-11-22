using System.Diagnostics.CodeAnalysis;

namespace Research.SharedKernel.Results;

/// <summary>Non-generic view of a business outcome so cross-cutting pipeline behaviors can inspect failures without reflection.</summary>
public interface IOperationResult
{
    bool IsSuccess { get; }

    Error? Error { get; }
}

/// <summary>Allows pipeline behaviors (such as validation) to construct a failed result of type <typeparamref name="TSelf"/> without reflection.</summary>
public interface IResultFactory<out TSelf>
    where TSelf : IOperationResult
{
    static abstract TSelf CreateFailure(Error error);
}

/// <summary>The outcome of an operation the business expects to fail sometimes: success, or one of the service's errors.</summary>
public sealed class Result : IOperationResult, IResultFactory<Result>
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

    static Result IResultFactory<Result>.CreateFailure(Error error) => Failure(error);
}

/// <summary>The outcome of an operation the business expects to fail sometimes: a value, or one of the service's errors.</summary>
public sealed class Result<T> : IOperationResult, IResultFactory<Result<T>>
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

    static Result<T> IResultFactory<Result<T>>.CreateFailure(Error error) => Failure(error);
}
