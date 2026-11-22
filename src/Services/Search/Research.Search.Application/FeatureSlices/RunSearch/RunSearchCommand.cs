using Research.Shared.Bus.Mediation;
using Research.Shared.Core.Search;
using Research.Shared.Kernel.Results;

namespace Research.Search.Application.RunSearch;

/// <summary>Search for <see cref="Query"/>; at most <see cref="MaxResults"/> distinct canonical URLs.</summary>
public sealed record RunSearchCommand(string Query, int MaxResults) : ICommand<Result<SearchResponse>>;
