using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Contracts;
using Research.Contracts.Steps;
using Research.Shared.Adapters.Messaging.Tracing;
using Research.Shared.Core.Actors;
using Research.Shared.Core.Mediation;
using Research.Shared.Core.Messaging;

namespace Research.Shared.Adapters.Messaging.Consuming;

/// <summary>
/// The consumer pipeline for one contract, independent of the broker: deserialize, start the hop's span, open a DI
/// scope running as <c>system:&lt;service&gt;</c>, run the module's handler under a timeout, and classify the
/// outcome (§13.2). Registered as a singleton per consumed contract; the transport listener calls
/// <see cref="DispatchAsync"/> once per delivery and acts on the returned <see cref="DispatchResult"/>.
/// </summary>
public sealed partial class MessageDispatcher<TMessage>(
    IServiceScopeFactory scopeFactory,
    IMessagePublisher publisher,
    EnvelopeFactory envelopes,
    IOptionsMonitor<MessagingOptions> options,
    MessagingMetrics metrics,
    TimeProvider timeProvider,
    ILogger<MessageDispatcher<TMessage>> logger)
    where TMessage : class, IIntegrationMessage
{
    // Read on every use, so reloaded timeouts, attempts and delays apply to the next delivery.
    private MessagingOptions Settings => options.CurrentValue;

    public async Task<DispatchResult> DispatchAsync(IncomingDelivery delivery, string consumer, CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var started = timeProvider.GetTimestamp();
        var producer = TraceContextHeaders.Extract(delivery.Headers);
        using var activity = MessagingTracing.StartConsume(consumer, TMessage.MessageType, producer);
        activity?.SetTag("messaging.message.id", delivery.MessageId?.ToString("D"));
        activity?.SetTag("research.attempt", delivery.Attempt);

        var message = Deserialize(delivery);
        if (message is null || delivery.MessageId is not { } messageId)
        {
            var result = await HandlePoisonAsync(delivery, consumer, stoppingToken).ConfigureAwait(false);
            Record(consumer, "poison", started, activity, result);
            return result;
        }

        activity?.SetTag(MessagingTracing.JobIdTag, message.JobId.ToString("D"));
        activity?.SetTag(MessagingTracing.StepKeyTag, message.StepKey);

        using var logScope = logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["JobId"] = message.JobId,
            ["StepKey"] = message.StepKey,
            ["MessageId"] = messageId,
            ["MessageType"] = TMessage.MessageType,
            ["Attempt"] = delivery.Attempt,
        });

        var context = new MessageContext(
            messageId,
            TMessage.MessageType,
            delivery.Attempt,
            ReadOccurredAt(delivery.Headers),
            consumer);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Settings.MessageTimeoutSeconds));

        try
        {
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                scope.ServiceProvider.GetRequiredService<ScopedActor>().Id = ScopedActor.System(Settings.ServiceName);
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.SendAsync(new HandleMessage<TMessage>(message, context), timeout.Token).ConfigureAwait(false);
            }

            Record(consumer, "success", started, activity, DispatchResult.Ack);
            return DispatchResult.Ack;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogInterruptedByShutdown(logger, consumer, messageId);
            Record(consumer, "requeued", started, activity, DispatchResult.Requeue);
            return DispatchResult.Requeue;
        }
#pragma warning disable CA1031 // Any handler exception is a delivery failure the pipeline classifies; nothing may escape to the transport.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error);

            if (delivery.Attempt < Settings.MaxAttempts)
            {
                var delayIndex = Math.Min(delivery.Attempt - 1, Settings.RetryDelaysSeconds.Length - 1);
                LogRetrying(logger, exception, consumer, messageId, delivery.Attempt, Settings.RetryDelaysSeconds[delayIndex]);
                var retry = DispatchResult.Retry(delayIndex);
                Record(consumer, "retry", started, activity, retry);
                return retry;
            }

            LogExhausted(logger, exception, consumer, messageId, delivery.Attempt);
            var result = await ReportFailureAsync(message.JobId, message.StepKey, MessagingErrors.StepExhausted.Code, stoppingToken)
                .ConfigureAwait(false);
            Record(consumer, "exhausted", started, activity, result);
            return result;
        }
    }

    private TMessage? Deserialize(IncomingDelivery delivery)
    {
        try
        {
            return JsonSerializer.Deserialize<TMessage>(delivery.Body.Span, MessageJson.Options);
        }
        catch (JsonException exception)
        {
            LogUnreadable(logger, exception, TMessage.MessageType, delivery.MessageId);
            return null;
        }
        catch (NotSupportedException exception)
        {
            LogUnreadable(logger, exception, TMessage.MessageType, delivery.MessageId);
            return null;
        }
    }

    private async Task<DispatchResult> HandlePoisonAsync(IncomingDelivery delivery, string consumer, CancellationToken cancellationToken)
    {
        LogPoison(logger, consumer, delivery.MessageId);

        // A poison message still owes Jobs an answer when the headers say which step it belongs to; otherwise the step
        // waits for StuckJobRecoveryWorker.
        if (delivery.Headers.TryGetValue(MessageHeaders.JobId, out var jobIdText)
            && Guid.TryParse(jobIdText, out var jobId)
            && delivery.Headers.TryGetValue(MessageHeaders.StepKey, out var stepKey)
            && !string.IsNullOrWhiteSpace(stepKey))
        {
            return await ReportFailureAsync(jobId, stepKey, MessagingErrors.PoisonMessage.Code, cancellationToken).ConfigureAwait(false);
        }

        return DispatchResult.DeadLetter;
    }

    /// <summary>Publishes <see cref="StepFailed"/>; if even that fails, keeps the delivery for a later retry.</summary>
    private async Task<DispatchResult> ReportFailureAsync(Guid jobId, string stepKey, string errorCode, CancellationToken cancellationToken)
    {
        try
        {
            var failed = new StepFailed(jobId, stepKey, errorCode, Settings.ServiceName);
            await publisher.PublishAsync(envelopes.Create(failed), cancellationToken).ConfigureAwait(false);
            return DispatchResult.DeadLetter;
        }
#pragma warning disable CA1031 // The broker may be down; the delivery is then retried rather than dead-lettered unreported.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            LogStepFailedNotPublished(logger, exception, jobId, stepKey, errorCode);
            return DispatchResult.Retry(Settings.RetryDelaysSeconds.Length - 1);
        }
    }

    private void Record(string consumer, string outcome, long started, Activity? activity, DispatchResult result)
    {
        activity?.SetTag("research.dispatch.outcome", outcome);
        metrics.RecordHandled(consumer, outcome, timeProvider.GetElapsedTime(started));
        if (result.Outcome == DispatchOutcome.DeadLetter)
        {
            metrics.DeadLettered(consumer);
        }
    }

    private static DateTimeOffset ReadOccurredAt(IReadOnlyDictionary<string, string> headers) =>
        headers.TryGetValue(MessageHeaders.OccurredAt, out var text)
        && DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var value)
            ? value
            : DateTimeOffset.MinValue;

    [LoggerMessage(EventId = 1000, Level = LogLevel.Warning, Message = "Message {MessageType} {MessageId} could not be deserialized")]
    private static partial void LogUnreadable(ILogger logger, Exception exception, string messageType, Guid? messageId);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "Poison message {MessageId} on {Queue} is dead-lettered")]
    private static partial void LogPoison(ILogger logger, string queue, Guid? messageId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Handling {MessageId} on {Queue} failed on attempt {Attempt}; retrying in {DelaySeconds} s")]
    private static partial void LogRetrying(ILogger logger, Exception exception, string queue, Guid messageId, int attempt, int delaySeconds);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "Handling {MessageId} on {Queue} failed on all {Attempt} attempts; dead-lettering")]
    private static partial void LogExhausted(ILogger logger, Exception exception, string queue, Guid messageId, int attempt);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Handling {MessageId} on {Queue} was interrupted by shutdown; requeued")]
    private static partial void LogInterruptedByShutdown(ILogger logger, string queue, Guid messageId);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Error, Message = "StepFailed {ErrorCode} for job {JobId} step {StepKey} could not be published")]
    private static partial void LogStepFailedNotPublished(ILogger logger, Exception exception, Guid jobId, string stepKey, string errorCode);
}
