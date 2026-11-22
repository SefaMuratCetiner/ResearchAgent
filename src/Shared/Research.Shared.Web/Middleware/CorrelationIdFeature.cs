namespace Research.Shared.Web.Middleware;

/// <summary>The accepted correlation id of the current request.</summary>
public sealed record CorrelationIdFeature(string Value);
