namespace Research.Jobs.Api.GetResearchReport;

/// <summary>One numbered source; <c>[n]</c> in the report text refers to <see cref="Number"/>.</summary>
public sealed record CitationResponse(int Number, string Url, string Title);
