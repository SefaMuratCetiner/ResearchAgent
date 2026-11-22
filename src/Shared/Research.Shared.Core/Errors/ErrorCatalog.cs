using Research.Shared.Kernel.Results;

namespace Research.Shared.Core.Errors;

/// <summary>
/// Every error code the service can return, from all registered <see cref="ErrorSet"/> instances. Built once at
/// composition time; an invalid catalog (duplicate code, foreign prefix) stops the service before it starts.
/// </summary>
public sealed class ErrorCatalog
{
    private readonly Dictionary<string, Error> _errors;

    public ErrorCatalog(IEnumerable<ErrorSet> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);
        _errors = new Dictionary<string, Error>(StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var set in sets)
        {
            if (set is null)
            {
                throw new ArgumentException("Error sets must not contain null.", nameof(sets));
            }
            foreach (var error in set.Errors)
            {
                if (!error.Code.StartsWith(set.Prefix, StringComparison.Ordinal))
                {
                    failures.Add($"{error.Code} does not start with {set.Prefix}");
                }

                if (!_errors.TryAdd(error.Code, error))
                {
                    failures.Add($"{error.Code} is declared twice");
                }
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException("Invalid error catalog: " + string.Join("; ", failures));
        }
    }

    public IReadOnlyCollection<Error> All => _errors.Values;

    public bool TryGet(string code, out Error? error) => _errors.TryGetValue(code, out error);
}
