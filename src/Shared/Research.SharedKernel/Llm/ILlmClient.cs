namespace Research.SharedKernel.Llm;

/// <summary>
/// One text completion from a large language model. Modules depend on this port only; the Anthropic provider
/// implements it. Transport failures throw; a refusal or truncation comes back in <see cref="LlmCompletion.StopReason"/>.
/// </summary>
public interface ILlmClient
{
    Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken);
}
