namespace Research.Jobs.Application.Common.Models;

/// <summary>How many steps of one kind are in each state.</summary>
public sealed record StepProgressDto(string Kind, int Running, int Succeeded, int Failed);
