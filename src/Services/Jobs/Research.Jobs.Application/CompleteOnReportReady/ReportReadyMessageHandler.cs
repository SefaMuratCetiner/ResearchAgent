using Microsoft.Extensions.Options;
using Research.Contracts.Synthesis;
using Research.Jobs.Domain;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Lifecycle;
using Research.Shared.Core.Actors;
using Research.Shared.Core.Messaging;
using Research.Shared.Core.Security;

namespace Research.Jobs.CompleteOnReportReady;

/// <summary>
/// The report arrived: store it and move the job to <see cref="JobStatus.Completed"/> or
/// <see cref="JobStatus.CompletedWithWarnings"/>. A job cancelled or expired meanwhile discards it unstored (the gate
/// sees a terminal job).
/// </summary>
public sealed class ReportReadyMessageHandler(
    JobReplyGate gate,
    JobLifecycle lifecycle,
    IResearchReportRepository reports,
    TimeProvider timeProvider,
    ICurrentActor actor,
    IOptionsSnapshot<JobsOptions> options) : IMessageHandler<ReportReady>
{
    public Task HandleAsync(ReportReady message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return gate.ProcessAsync(
            message.JobId,
            message.StepKey,
            StepStatus.Succeeded,
            stepErrorCode: null,
            context,
            async (job, transaction) =>
            {
                if (job.Status != JobStatus.Synthesizing)
                {
                    return;
                }

                var current = await lifecycle.AddTokensAsync(job, message.InputTokens + message.OutputTokens, transaction, cancellationToken)
                    .ConfigureAwait(false);
                if (current is null)
                {
                    return;
                }

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
                    CreatedAt = timeProvider.GetUtcNow(),
                    CreatedBy = actor.Id,
                };
                await reports.InsertAsync(report, transaction, cancellationToken).ConfigureAwait(false);
                await lifecycle.CompleteAsync(current, report.Truncated, transaction, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }
}
