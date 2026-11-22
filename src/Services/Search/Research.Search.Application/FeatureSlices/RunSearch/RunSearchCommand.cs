using Research.Shared.Core.Mediation;
using Research.Shared.Ports.Search;
using Research.SharedKernel.Results;

namespace Research.Search.Application.RunSearch;

/// <summary>Search for <see cref="Query"/>; at most <see cref="MaxResults"/> distinct canonical URLs.</summary>
public sealed record RunSearchCommand(string Query, int MaxResults) : ICommand<Result<SearchResponse>>;
