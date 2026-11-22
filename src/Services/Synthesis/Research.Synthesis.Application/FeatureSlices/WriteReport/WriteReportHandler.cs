using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Shared.Ports.Llm;
using Research.Shared.Core.Logging;
using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;
using Research.SharedKernel.Security;
using Research.Synthesis.Application.Common.Interfaces;
using Research.Synthesis.Domain.Policies;

namespace Research.Synthesis.Application.WriteReport;

/// <summary>
/// Retrieves the top passages for the question and every sub-question, writes the report with one LLM call, and
/// keeps only the sources it cites. A report cut at the output limit is still returned, marked truncated. The report
/// is logged in full under <see cref="ContentLogCategories.Reports"/>.
/// <para>
/// The prompt fences every untrusted input (<see cref="PromptFence"/>), and the answer goes through
/// <see cref="ReportSanitizer"/> before anything else sees it. A report that cites no source fails with
/// <c>synthesis.uncited_report</c> rather than presenting unsupported text as research (OWASP LLM09, ASI09).
/// </para>
/// </summary>
public sealed partial class WriteReportHandler : ICommandHandler<WriteReportCommand, Result<WrittenReport>>
{
    private readonly IKnowledgeRetriever _retriever;
    private readonly ILlmClient _llm;
    private readonly IOptionsSnapshot<SynthesisOptions> _options;
    private readonly IOptionsMonitor<ContentLoggingOptions> _contentOptions;
    private readonly ILogger _logger;
    private readonly ILogger _reportLogger;

    public WriteReportHandler(
        IKnowledgeRetriever retriever,
        ILlmClient llm,
        IOptionsSnapshot<SynthesisOptions> options,
        IOptionsMonitor<ContentLoggingOptions> contentOptions,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _retriever = retriever;
        _llm = llm;
        _options = options;
        _contentOptions = contentOptions;
        _logger = loggerFactory.CreateLogger<WriteReportHandler>();
        _reportLogger = loggerFactory.CreateLogger(ContentLogCategories.Reports);
    }

    public async Task<Result<WrittenReport>> HandleAsync(WriteReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var settings = _options.Value;

        var queries = new List<string> { command.Question };
        queries.AddRange(command.SubQuestions);
        var passages = await _retriever.RetrieveAsync(command.JobId, queries, command.TopK, cancellationToken).ConfigureAwait(false);
        if (passages.Count == 0)
        {
            return Result<WrittenReport>.Failure(SynthesisErrors.NoPassages);
        }

        var fence = new PromptFence();
        var citations = new CitationBuilder();
        var context = citations.BuildContext(passages, settings.MaxContextChars, fence);
        if (context.Length == 0)
        {
            return Result<WrittenReport>.Failure(SynthesisErrors.NoPassages);
        }

        var completion = await _llm.CompleteAsync(
            new LlmRequest(
                "synthesize",
                settings.Model,
                settings.Effort,
                SynthesisPrompts.System,
                SynthesisPrompts.User(command.Question, command.SubQuestions, context, fence),
                settings.MaxTokens),
            cancellationToken).ConfigureAwait(false);

        if (completion.StopReason == LlmStopReason.Refused)
        {
            return Result<WrittenReport>.Failure(SynthesisErrors.LlmRefused);
        }

        var sanitized = ReportSanitizer.Sanitize(completion.Text, citations.Sources.Select(static s => s.Number).ToHashSet());
        if (sanitized.Removals.Total > 0)
        {
            LogOutputSanitized(_logger, command.JobId, sanitized.Removals.ToString());
        }

        var content = sanitized.Content;
        if (content.Length == 0)
        {
            return Result<WrittenReport>.Failure(SynthesisErrors.EmptyReport);
        }

        var truncated = completion.StopReason == LlmStopReason.MaxTokens;
        var cited = citations.CitedIn(content);
        if (cited.Count == 0)
        {
            LogUncited(_logger, command.JobId);
            LogReport(_reportLogger, command.JobId, ContentText.Truncate(content, _contentOptions.CurrentValue.MaxChars), 0);
            return Result<WrittenReport>.Failure(SynthesisErrors.UncitedReport);
        }

        LogReportWritten(_logger, command.JobId, passages.Count, cited.Count, truncated);
        LogReport(_reportLogger, command.JobId, ContentText.Truncate(content, _contentOptions.CurrentValue.MaxChars), cited.Count);

        return Result<WrittenReport>.Success(
            new WrittenReport(content, cited, truncated, completion.InputTokens, completion.OutputTokens, completion.Model));
    }

    [LoggerMessage(EventId = 7000, Level = LogLevel.Information, Message = "Report for job {JobId} written from {PassageCount} passages, citing {CitationCount} sources (truncated: {Truncated})")]
    private static partial void LogReportWritten(ILogger logger, Guid jobId, int passageCount, int citationCount, bool truncated);

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "Report for job {JobId} ({CitationCount} citations): {Report}")]
    private static partial void LogReport(ILogger logger, Guid jobId, string report, int citationCount);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Warning, Message = "Report for job {JobId} broke its output rules and was cleaned ({Removals}); a source may have tried to steer the model")]
    private static partial void LogOutputSanitized(ILogger logger, Guid jobId, string removals);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Warning, Message = "Report for job {JobId} cites no source and is rejected")]
    private static partial void LogUncited(ILogger logger, Guid jobId);
}
