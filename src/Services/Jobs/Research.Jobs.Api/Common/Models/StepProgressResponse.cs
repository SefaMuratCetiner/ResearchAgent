namespace Research.Jobs.Api.Common.Models;

/// <summary>Step progress of one kind.</summary>
public sealed record StepProgressResponse(string Kind, int Running, int Succeeded, int Failed);
