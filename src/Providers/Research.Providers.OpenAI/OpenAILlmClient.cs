using System.ClientModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Research.SharedKernel.Llm;
using Research.SharedKernel.Logging;

namespace Research.Providers.OpenAI;

/// <summary>
/// <see cref="ILlmClient"/> on the OpenAI Chat Completions API. Depth is set with <see cref="LlmRequest.Effort"/>, sent
/// as <c>reasoning_effort</c> (see <see cref="OpenAIOptions.UseReasoningEffort"/>); a JSON schema becomes a strict
/// structured output. OpenAI caches a repeated prompt prefix by itself, so nothing is marked for caching. A refusal is
/// retried once on <see cref="OpenAIOptions.RefusalFallbackModel"/>. Prompts and responses are logged in full under
/// <see cref="ContentLogCategories.Llm"/>; the API key never is.
/// </summary>
public sealed partial class OpenAILlmClient : ILlmClient
{
    private const int TooManyRequests = 429;

    private readonly OpenAIClientSource _clients;
    private readonly IOptionsMonitor<OpenAIOptions> _options;
    private readonly IOptionsMonitor<ContentLoggingOptions> _contentOptions;
    private readonly LlmMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly ILogger _contentLogger;

    public OpenAILlmClient(
        OpenAIClientSource clients,
        IOptionsMonitor<OpenAIOptions> options,
        IOptionsMonitor<ContentLoggingOptions> contentOptions,
        LlmMetrics metrics,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _clients = clients;
        _options = options;
        _contentOptions = contentOptions;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<OpenAILlmClient>();
        _contentLogger = loggerFactory.CreateLogger(ContentLogCategories.Llm);
    }

    public async Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var completion = await SendAsync(request, request.Model, cancellationToken).ConfigureAwait(false);
        var fallbackModel = _options.CurrentValue.RefusalFallbackModel;
        if (completion.StopReason == LlmStopReason.Refused
            && !string.IsNullOrWhiteSpace(fallbackModel)
            && !string.Equals(fallbackModel, request.Model, StringComparison.Ordinal))
        {
            LogRefusalFallback(_logger, request.Operation, request.Model, fallbackModel);
            var fallback = await SendAsync(request, fallbackModel, cancellationToken).ConfigureAwait(false);
            return fallback with
            {
                InputTokens = fallback.InputTokens + completion.InputTokens,
                OutputTokens = fallback.OutputTokens + completion.OutputTokens,
            };
        }

        return completion;
    }

    private async Task<LlmCompletion> SendAsync(LlmRequest request, string model, CancellationToken cancellationToken)
    {
        using var activity = LlmTracing.Source.StartActivity("llm " + request.Operation, ActivityKind.Client);
        activity?.SetTag("gen_ai.system", "openai");
        activity?.SetTag("gen_ai.request.model", model);
        activity?.SetTag("gen_ai.operation.name", request.Operation);

        var maxChars = _contentOptions.CurrentValue.MaxChars;
        LogPrompt(
            _contentLogger,
            request.Operation,
            model,
            request.Effort,
            ContentText.Truncate(request.SystemPrompt, maxChars),
            ContentText.Truncate(request.UserPrompt, maxChars));

        ChatMessage[] messages = [new SystemChatMessage(request.SystemPrompt), new UserChatMessage(request.UserPrompt)];
        var options = CreateOptions(request, _options.CurrentValue.UseReasoningEffort);

        var started = _timeProvider.GetTimestamp();
        ChatCompletion response;
        try
        {
            response = await _clients.Current.GetChatClient(model)
                .CompleteChatAsync(messages, options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ClientResultException exception) when (exception.Status == TooManyRequests)
        {
            _metrics.Throttled(model);
            activity?.SetStatus(ActivityStatusCode.Error, "rate limited");
            throw;
        }

        var text = string.Concat(response.Content
            .Where(static part => part.Kind == ChatMessageContentPartKind.Text)
            .Select(static part => part.Text));
        var stopReason = MapStopReason(response.FinishReason, response.Refusal);
        long inputTokens = response.Usage?.InputTokenCount ?? 0;
        long outputTokens = response.Usage?.OutputTokenCount ?? 0;

        _metrics.Completed(model, request.Operation, _timeProvider.GetElapsedTime(started), inputTokens, outputTokens);
        if (stopReason == LlmStopReason.Refused)
        {
            _metrics.Refused(model);
        }

        activity?.SetTag("gen_ai.usage.input_tokens", inputTokens);
        activity?.SetTag("gen_ai.usage.output_tokens", outputTokens);
        activity?.SetTag("gen_ai.response.finish_reason", stopReason.ToString());

        LogResponse(
            _contentLogger,
            request.Operation,
            model,
            stopReason,
            inputTokens,
            outputTokens,
            ContentText.Truncate(string.IsNullOrEmpty(text) ? response.Refusal ?? string.Empty : text, maxChars));

        return new LlmCompletion(text, stopReason, model, inputTokens, outputTokens);
    }

    internal static ChatCompletionOptions CreateOptions(LlmRequest request, bool useReasoningEffort)
    {
        var options = new ChatCompletionOptions { MaxOutputTokenCount = request.MaxTokens };
        if (useReasoningEffort)
        {
            options.ReasoningEffortLevel = MapEffort(request.Effort);
        }

        if (request.JsonSchema is not null)
        {
            options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                request.Operation,
                BinaryData.FromString(request.JsonSchema),
                jsonSchemaIsStrict: true);
        }

        return options;
    }

    /// <summary><see cref="LlmEffort.Max"/> maps to <c>high</c>: the levels above it are not offered by every model.</summary>
    internal static ChatReasoningEffortLevel MapEffort(LlmEffort effort) => effort switch
    {
        LlmEffort.Low => ChatReasoningEffortLevel.Low,
        LlmEffort.High or LlmEffort.Max => ChatReasoningEffortLevel.High,
        _ => ChatReasoningEffortLevel.Medium,
    };

    /// <summary>A refusal comes back in its own field (structured outputs) or as a content-filter stop.</summary>
    internal static LlmStopReason MapStopReason(ChatFinishReason finishReason, string? refusal)
    {
        if (!string.IsNullOrEmpty(refusal) || finishReason == ChatFinishReason.ContentFilter)
        {
            return LlmStopReason.Refused;
        }

        return finishReason switch
        {
            ChatFinishReason.Stop => LlmStopReason.Completed,
            ChatFinishReason.Length => LlmStopReason.MaxTokens,
            _ => LlmStopReason.Other,
        };
    }

    [LoggerMessage(EventId = 8300, Level = LogLevel.Information, Message = "LLM {Operation} prompt to {Model} at effort {Effort}: system {SystemPrompt} user {UserPrompt}")]
    private static partial void LogPrompt(ILogger logger, string operation, string model, LlmEffort effort, string systemPrompt, string userPrompt);

    [LoggerMessage(EventId = 8301, Level = LogLevel.Information, Message = "LLM {Operation} response from {Model} ({StopReason}, {InputTokens} in / {OutputTokens} out): {ResponseText}")]
    private static partial void LogResponse(ILogger logger, string operation, string model, LlmStopReason stopReason, long inputTokens, long outputTokens, string responseText);

    [LoggerMessage(EventId = 8302, Level = LogLevel.Warning, Message = "LLM {Operation} was refused by {Model}; retrying once on {FallbackModel}")]
    private static partial void LogRefusalFallback(ILogger logger, string operation, string model, string fallbackModel);
}
