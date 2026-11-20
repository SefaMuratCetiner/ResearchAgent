namespace Research.Shared.Core.Llm;

/// <summary>
/// One text completion from a large language model. Modules depend on this port only; the Anthropic and OpenAI
/// providers implement it, and the Host picks one with <see cref="LlmOptions.Provider"/>. Transport failures throw; a
/// refusal or truncation comes back in <see cref="LlmCompletion.StopReason"/>.
/// </summary>
public interface ILlmClient
{
    Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken);
}
