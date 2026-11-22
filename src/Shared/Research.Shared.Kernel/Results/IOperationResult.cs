namespace Research.Shared.Kernel.Results;

/// <summary>Non-generic view of a business outcome so cross-cutting pipeline behaviors can inspect failures without reflection.</summary>
public interface IOperationResult
{
    bool IsSuccess { get; }

    Error? Error { get; }
}
