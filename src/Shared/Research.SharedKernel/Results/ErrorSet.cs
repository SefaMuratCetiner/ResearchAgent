namespace Research.SharedKernel.Results;

/// <summary>
/// A prefixed set of error codes owned by a single service or library.
/// Every code in <see cref="Errors"/> must start with <see cref="Prefix"/> (including the trailing dot, e.g. <c>jobs.</c>).
/// </summary>
public sealed record ErrorSet(string Prefix, IReadOnlyList<Error> Errors);
