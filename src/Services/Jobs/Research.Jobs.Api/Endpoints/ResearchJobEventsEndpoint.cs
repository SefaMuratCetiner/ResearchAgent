using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Research.Jobs.Application;
using Research.Jobs.Application.GetResearchJob;
using Research.Shared.Core.Mediation;
using Research.Shared.Web.Endpoints;
using Research.Shared.Web.Problems;

namespace Research.Jobs.Api.Endpoints;

/// <summary>
/// <c>GET /research/{id}/events</c>: Server-Sent Events. Sends the job now and on every change (event <c>job</c>,
/// id = version), and closes after the terminal state. Changes are read from the database every
/// <see cref="JobsOptions.EventsPollInterval"/>, so a stream works on any replica, whichever replica made the change.
/// </summary>
public sealed class ResearchJobEventsEndpoint : IEndpoint
{
    public const string Pattern = "/{id:guid}/events";
    public const string Name = "StreamResearchJobEvents";
    public const string EventType = "job";

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(Pattern, HandleAsync)
            .WithName(Name)
            .WithSummary("Stream status changes of a research job (Server-Sent Events)")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status404NotFound);

    public static async Task<IResult> HandleAsync(
        Guid id,
        ISender sender,
        IOptionsSnapshot<JobsOptions> options,
        TimeProvider timeProvider,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(options);

        var first = await sender.SendAsync(new GetResearchJobQuery(id), cancellationToken).ConfigureAwait(false);
        return first.ToHttpResult(context, value => TypedResults.ServerSentEvents(
            StreamAsync(value, sender, options.Value.EventsPollInterval, timeProvider, cancellationToken)));
    }

    private static async IAsyncEnumerable<SseItem<ResearchJobResponse>> StreamAsync(
        ResearchJobView first,
        ISender sender,
        TimeSpan pollInterval,
        TimeProvider timeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var current = first;
        yield return ToItem(current);

        while (!current.Job.IsTerminal && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(pollInterval, timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            var next = await sender.SendAsync(new GetResearchJobQuery(current.Job.Id), cancellationToken).ConfigureAwait(false);
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
