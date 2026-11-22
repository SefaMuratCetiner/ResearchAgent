namespace Research.Knowledge.Api.SearchPassages;

/// <summary>The matching passages, best first.</summary>
public sealed record SearchPassagesResponse(IReadOnlyList<PassageResponse> Passages);
