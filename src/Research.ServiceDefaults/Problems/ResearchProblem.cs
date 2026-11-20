using System.Text.Json.Serialization;

namespace Research.ServiceDefaults.Problems;

/// <summary>
/// The error body of every failed API response (RFC 9457), fields in a fixed order. <see cref="Code"/> is the stable
/// field clients branch on; <see cref="Errors"/> is present only for <c>research.validation_failed</c>.
/// </summary>
public sealed record ResearchProblem(
    [property: JsonPropertyOrder(0)] string Type,
    [property: JsonPropertyOrder(1)] string Title,
    [property: JsonPropertyOrder(2)] int Status,
    [property: JsonPropertyOrder(3)] string? Detail,
    [property: JsonPropertyOrder(4)] string? Instance,
    [property: JsonPropertyOrder(5)] string Code,
    [property: JsonPropertyOrder(6)] string? TraceId,
    [property: JsonPropertyOrder(7)] IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public const string ContentType = "application/problem+json";

    /// <summary>The documentation base every <see cref="Type"/> points into.</summary>
    public const string TypeBase = "https://docs.research.local/errors/";
}
