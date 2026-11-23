using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Shared.Contracts.Planning;
using Research.Shared.Core.Caching;
using Research.Shared.Core.Llm;
using Research.Shared.Core.Security;
using Research.Shared.Kernel.Results;

namespace Research.Planning.Application.CreatePlan;

/// <summary>
/// Cache-aside planning: a cached plan for the same question is reused; otherwise one structured-output LLM call
/// produces it. A refusal, a truncated answer or an empty plan is a business failure; transport errors throw so the
/// pipeline retries.
/// </summary>
public sealed partial class ResearchPlanningService(
    ILlmClient llm,
    ICacheStore cache,
    IOptionsSnapshot<PlanningOptions> options,
    ILogger<ResearchPlanningService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<ResearchPlan>> PlanAsync(PlanRequested message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;
        var cacheKey = CacheKey(message);

        if (settings.CacheTimeToLive > TimeSpan.Zero
            && await cache.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false) is { } cached
            && TryParse(cached, message.MaxSubQuestions) is { Count: > 0 } cachedPlan)
        {
            LogCacheHit(logger, cachedPlan.Count);
            return Result<ResearchPlan>.Success(new ResearchPlan(cachedPlan, 0, 0, FromCache: true));
        }

        var completion = await llm.CompleteAsync(
            new LlmRequest(
                "plan",
                settings.Model,
                settings.Effort,
                PlanningPrompts.System,
                PlanningPrompts.User(message.Question, message.MaxSubQuestions, new PromptFence()),
                settings.MaxTokens,
                PlanningPrompts.Schema),
            cancellationToken).ConfigureAwait(false);

        if (completion.StopReason == LlmStopReason.Refused)
        {
            return Result<ResearchPlan>.Failure(PlanningErrors.LlmRefused);
        }

        var subQuestions = completion.StopReason == LlmStopReason.Completed
            ? TryParse(completion.Text, message.MaxSubQuestions)
            : null;
        if (subQuestions is not { Count: > 0 })
        {
            LogInvalidPlan(logger, completion.StopReason);
            return Result<ResearchPlan>.Failure(PlanningErrors.InvalidPlan);
        }

        if (settings.CacheTimeToLive > TimeSpan.Zero)
        {
            await cache.SetAsync(cacheKey, completion.Text, settings.CacheTimeToLive, cancellationToken).ConfigureAwait(false);
        }

        return Result<ResearchPlan>.Success(new ResearchPlan(subQuestions, completion.InputTokens, completion.OutputTokens, FromCache: false));
    }

    /// <summary>
    /// Model output is untrusted too (OWASP LLM05): each sub-question is cleaned to one line, cut at
    /// <see cref="PlanningPrompts.MaxSubQuestionChars"/>, and dropped when it carries a URL.
    /// </summary>
    internal static List<string>? TryParse(string json, int max)
    {
        try
        {
            var answer = JsonSerializer.Deserialize<PlanAnswer>(json, Json);
            return answer?.SubQuestions?
                .Select(static q => UntrustedText.SingleLine(q, PlanningPrompts.MaxSubQuestionChars))
                .Where(static q => q.Length > 0 && !q.Contains("://", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string CacheKey(PlanRequested message)
    {
        var normalized = message.Question.Trim().ToUpperInvariant() + "|" + message.MaxSubQuestions.ToString(CultureInfo.InvariantCulture);
        return "planning:v1:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private sealed record PlanAnswer(List<string?>? SubQuestions);

    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Plan served from cache ({Count} sub-questions)")]
    private static partial void LogCacheHit(ILogger logger, int count);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning, Message = "The model returned no usable plan (stop reason {StopReason})")]
    private static partial void LogInvalidPlan(ILogger logger, LlmStopReason stopReason);
}
