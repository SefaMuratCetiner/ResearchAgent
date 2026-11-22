using System.Diagnostics;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Shared.Adapters.Llm;
using Research.Shared.Ports.Llm;
using Research.Shared.Core.Logging;
using Research.Shared.Adapters.Llm.Observability;

namespace Research.Providers.Anthropic;

/// <summary>
/// <see cref="ILlmClient"/> on the Anthropic Messages API. Thinking is left adaptive (omitted); depth is set with
/// <see cref="LlmRequest.Effort"/>. The system prompt is marked for prompt caching. A refusal is retried once on
/// <see cref="AnthropicOptions.RefusalFallbackModel"/>. Prompts and responses are logged in full under
/// <see cref="ContentLogCategories.Llm"/>; the API key never is.
/// </summary>
public sealed partial class AnthropicLlmClient : ILlmClient
{
    private readonly AnthropicClientSource _clients;
    private readonly IOptionsMonitor<AnthropicOptions> _options;
    private readonly IOptionsMonitor<ContentLoggingOptions> _contentOptions;
    private readonly LlmMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly ILogger _contentLogger;

    public AnthropicLlmClient(
        AnthropicClientSource clients,
        IOptionsMonitor<AnthropicOptions> options,
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
        _logger = loggerFactory.CreateLogger<AnthropicLlmClient>();
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
        activity?.SetTag("gen_ai.system", "anthropic");
        activity?.SetTag("gen_ai.request.model", model);
        activity?.SetTag("gen_ai.operation.name", request.Operation);

        var maxChars = _contentOptions.CurrentValue.MaxChars;
        if (_contentLogger.IsEnabled(LogLevel.Information))
        {
            var systemPrompt = LogContentTruncator.Truncate(request.SystemPrompt, maxChars);
            var userPrompt = LogContentTruncator.Truncate(request.UserPrompt, maxChars);
            LogPrompt(_contentLogger, request.Operation, model, request.Effort, systemPrompt, userPrompt);
        }

        var parameters = CreateParameters(request, model);

        var started = _timeProvider.GetTimestamp();
        Message response;
        try
        {
            response = await _clients.Current.Messages.Create(parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (AnthropicRateLimitException)
        {
            _metrics.Throttled(model);
            activity?.SetStatus(ActivityStatusCode.Error, "rate limited");
            throw;
        }

        var text = string.Concat(response.Content.Select(static block => block.Value).OfType<TextBlock>().Select(static block => block.Text));
        var stopReason = MapStopReason(response);
        var inputTokens = response.Usage.InputTokens;
        var outputTokens = response.Usage.OutputTokens;

        RecordCompletion(activity, request, model, started, stopReason, inputTokens, outputTokens, text, maxChars);
        return new LlmCompletion(text, stopReason, model, inputTokens, outputTokens);
    }

    private void RecordCompletion(
        Activity? activity,
        LlmRequest request,
        string model,
        long started,
        LlmStopReason stopReason,
        long inputTokens,
        long outputTokens,
        string text,
        int maxChars)
    {
        _metrics.Completed(model, request.Operation, _timeProvider.GetElapsedTime(started), inputTokens, outputTokens);
        if (stopReason == LlmStopReason.Refused)
        {
            _metrics.Refused(model);
        }

        activity?.SetTag("gen_ai.usage.input_tokens", inputTokens);
        activity?.SetTag("gen_ai.usage.output_tokens", outputTokens);
        activity?.SetTag("gen_ai.response.finish_reason", stopReason.ToString());

        if (_contentLogger.IsEnabled(LogLevel.Information))
        {
            var responseText = LogContentTruncator.Truncate(text, maxChars);
            LogResponse(_contentLogger, request.Operation, model, stopReason, inputTokens, outputTokens, responseText);
        }
    }

    private static MessageCreateParams CreateParameters(LlmRequest request, string model) => new()
    {
        Model = model,
        MaxTokens = request.MaxTokens,
        System = new List<TextBlockParam>
        {
            new() { Text = request.SystemPrompt, CacheControl = new CacheControlEphemeral() },
        },
        Messages = [new UserMessageParam { Content = request.UserPrompt }],
        OutputConfig = CreateOutputConfig(request),
    };

    private static OutputConfig CreateOutputConfig(LlmRequest request) =>
        request.JsonSchema is null
            ? new OutputConfig { Effort = MapEffort(request.Effort) }
            : new OutputConfig
            {
                Effort = MapEffort(request.Effort),
                Format = new JsonOutputFormat { Schema = ParseSchema(request.JsonSchema) },
            };

    private static Dictionary<string, JsonElement> ParseSchema(string schema)
    {
        using var document = JsonDocument.Parse(schema);
        return document.RootElement.EnumerateObject()
            .ToDictionary(static property => property.Name, static property => property.Value.Clone(), StringComparer.Ordinal);
    }

    private static Effort MapEffort(LlmEffort effort) => effort switch
    {
        LlmEffort.Low => Effort.Low,
        LlmEffort.High => Effort.High,
        LlmEffort.Max => Effort.Max,
        _ => Effort.Medium,
    };

    private static LlmStopReason MapStopReason(Message response)
    {
        if (response.StopReason == "refusal")
        {
            return LlmStopReason.Refused;
        }

        if (response.StopReason == "max_tokens")
        {
            return LlmStopReason.MaxTokens;
        }

        return response.StopReason == "end_turn" ? LlmStopReason.Completed : LlmStopReason.Other;
    }

    [LoggerMessage(EventId = 8000, Level = LogLevel.Information, Message = "LLM {Operation} prompt to {Model} at effort {Effort}: system {SystemPrompt} user {UserPrompt}")]
    private static partial void LogPrompt(ILogger logger, string operation, string model, LlmEffort effort, string systemPrompt, string userPrompt);

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "LLM {Operation} response from {Model} ({StopReason}, {InputTokens} in / {OutputTokens} out): {ResponseText}")]
    private static partial void LogResponse(ILogger logger, string operation, string model, LlmStopReason stopReason, long inputTokens, long outputTokens, string responseText);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Warning, Message = "LLM {Operation} was refused by {Model}; retrying once on {FallbackModel}")]
    private static partial void LogRefusalFallback(ILogger logger, string operation, string model, string fallbackModel);
}
