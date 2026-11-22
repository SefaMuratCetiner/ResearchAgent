using Research.Shared.Core.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Fetching.Application.FetchPage;

/// <summary>Fetch <see cref="Url"/> for a job; <see cref="Title"/> is the search result title, used when the page has none.</summary>
public sealed record FetchPageCommand(Guid JobId, string Url, string? Title) : ICommand<Result<FetchedPage>>, IJobScopedRequest;
