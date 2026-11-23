using Microsoft.Extensions.Options;
using Research.Shared.Contracts.Synthesis;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Shared.Bus.Messaging;
using Research.Shared.Core.Security;

namespace Research.Jobs.Application.CompleteOnReportReady;

/// <summary>
/// The report arrived: store it and move the job to <see cref="JobStatus.Completed"/> or
/// <see cref="JobStatus.CompletedWithWarnings"/>. A job cancelled or expired meanwhile discards it unstored
/// (<see cref="StepReplyProcessor"/> ignores terminal jobs).
/// </summary>
public sealed class ReportReadyMessageHandler(
    StepReplyProcessor replyProcessor,
    IJobStepRepository steps,
    IResearchReportRepository reports,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider) : IMessageHandler<ReportReady>
{
    public Task HandleAsync(ReportReady message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return replyProcessor.ProcessAsync(
            message.JobId,
            message.StepKey,
            StepStatus.Succeeded,
            stepErrorCode: null,
            context,
            async job =>
            {
                if (job.Status != JobStatus.Synthesizing)
                {
                    return;
                }

                job.RecordTokens(message.InputTokens + message.OutputTokens);

                // Another service's message is input like any other (OWASP ASI07, LLM05): bounded text, citations
                // with web URLs only.
                var settings = options.Value;
                var content = message.Content ?? string.Empty;
                var cut = content.Length > settings.MaxReportChars;
                var report = new ResearchReport
                {
                    Id = job.Id,
                    Content = cut ? content[..settings.MaxReportChars] : content,
                    Citations = (message.Citations ?? [])
                        .Where(static c => c is not null && c.Number > 0 && UntrustedText.IsWebUrl(c.Url))
                        .Take(settings.MaxCitations)
                        .Select(static c => new ReportSource(c.Number, c.Url, UntrustedText.SingleLine(c.Title)))
                        .ToList(),
                    Model = UntrustedText.SingleLine(message.Model, 100),
                    Truncated = message.Truncated || cut,
                };
                await reports.InsertAsync(report, cancellationToken).ConfigureAwait(false);

                var counts = await steps.CountAsync(job.Id, cancellationToken).ConfigureAwait(false);
                job.Complete(counts.GatheringFailed, report.Truncated, timeProvider.GetUtcNow());
            },
            cancellationToken);
    }
}
