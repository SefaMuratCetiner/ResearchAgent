using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Research.Jobs.Domain;
using Research.Jobs.GetResearchJob;
using Research.Problems;

namespace Research.Jobs.Api.Endpoints;

/// <summary>
/// <c>GET /research/{id}/events</c>: Server-Sent Events. Sends the job now and on every change (event <c>job</c>,
/// id = version), and closes after the terminal state. Changes are read from the database every
/// <see cref="JobsOptions.EventsPollInterval"/>, so a stream works on any replica, whichever replica made the change.
/// </summary>
public static class ResearchJobEventsEndpoint
{
    public const string Pattern = "/{id:guid}/events";
    public const string Name = "StreamResearchJobEvents";
    public const string EventType = "job";

    public static async Task<IResult> HandleAsync(
        Guid id,
        GetResearchJobHandler handler,
        IOptionsSnapshot<JobsOptions> options,
        TimeProvider timeProvider,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);

        var first = await handler.HandleAsync(new GetResearchJobQuery(id), cancellationToken).ConfigureAwait(false);
        if (!first.IsSuccess)
        {
            return ProblemResults.From(first.Error, context);
        }

        return TypedResults.ServerSentEvents(
            StreamAsync(first.Value, handler, options.Value.EventsPollInterval, timeProvider, cancellationToken));
    }

    private static async IAsyncEnumerable<SseItem<ResearchJobResponse>> StreamAsync(
        ResearchJobView first,
        GetResearchJobHandler handler,
        TimeSpan pollInterval,
        TimeProvider timeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var current = first;
        yield return ToItem(current);

        while (!current.Job.Status.IsTerminal() && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(pollInterval, timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            var next = await handler.HandleAsync(new GetResearchJobQuery(current.Job.Id), cancellationToken).ConfigureAwait(false);
            if (!next.IsSuccess)
            {
                yield break;
            }

            if (next.Value.Job.Version != current.Job.Version || !SameProgress(next.Value, current))
            {
                current = next.Value;
                yield return ToItem(current);
            }
        }
    }

    private static bool SameProgress(ResearchJobView a, ResearchJobView b) => a.Steps.SequenceEqual(b.Steps);

    private static SseItem<ResearchJobResponse> ToItem(ResearchJobView view) =>
        new(ResearchJobResponse.From(view), EventType)
        {
            EventId = view.Job.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
}
