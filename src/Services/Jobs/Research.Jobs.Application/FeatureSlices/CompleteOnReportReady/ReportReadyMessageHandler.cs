using Research.Jobs.Application.Common.Services;
using Research.Shared.Bus.Messaging;
using Research.Shared.Contracts.Synthesis;

namespace Research.Jobs.Application.CompleteOnReportReady;

/// <summary>The report arrived: it is stored and the job completes.</summary>
public sealed class ReportReadyMessageHandler(StepReplyProcessor replies, JobProgressService progress) : IMessageHandler<ReportReady>
{
    public Task HandleAsync(ReportReady message, MessageContext context, CancellationToken cancellationToken) =>
        replies.ProcessAsync(message, context, job => progress.ApplyReportAsync(job, message, cancellationToken), cancellationToken);
}
