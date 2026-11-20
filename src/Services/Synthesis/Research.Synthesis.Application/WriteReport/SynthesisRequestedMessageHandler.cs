using Research.Contracts.Steps;
using Research.Contracts.Synthesis;
using Research.Shared.Core.Messaging;
using Research.Shared.Core.Messaging;

namespace Research.Synthesis.WriteReport;

/// <summary>Answers <c>SynthesisRequested</c> with <c>ReportReady</c>, or <c>StepFailed</c> for a business failure.</summary>
public sealed class SynthesisRequestedMessageHandler(WriteReportHandler handler, IMessageSender sender) : IMessageHandler<SynthesisRequested>
{
    public const string ServiceName = "synthesis";

    public async Task HandleAsync(SynthesisRequested message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var result = await handler.HandleAsync(
            new WriteReportCommand(message.JobId, message.Question, message.SubQuestions ?? [], message.TopK),
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            await sender.SendAsync(new StepFailed(message.JobId, message.StepKey, result.Error.Code, ServiceName), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var report = result.Value;
        await sender.SendAsync(
            new ReportReady(
                message.JobId,
                message.StepKey,
                report.Content,
                report.Citations.Select(static c => new ReportCitation(c.Number, c.Url, c.Title)).ToList(),
                report.Truncated,
                report.InputTokens,
                report.OutputTokens,
                report.Model),
            cancellationToken).ConfigureAwait(false);
    }
}
