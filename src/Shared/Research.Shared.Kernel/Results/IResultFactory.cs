namespace Research.Shared.Kernel.Results;

/// <summary>Allows pipeline behaviors (such as validation) to construct a failed result of type <typeparamref name="TSelf"/> without reflection.</summary>
public interface IResultFactory<out TSelf>
    where TSelf : IOperationResult
{
    static abstract TSelf CreateFailure(Error error);
}
