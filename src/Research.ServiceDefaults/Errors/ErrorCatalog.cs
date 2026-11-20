using Research.Shared.Core.Errors;
using Research.SharedKernel.Results;

namespace Research.ServiceDefaults.Errors;

/// <summary>
/// Every error code the service can return, from all registered contributors. Built once at composition time; an
/// invalid catalog (duplicate code, missing prefix) stops the service before it starts.
/// </summary>
public sealed class ErrorCatalog
{
    private readonly Dictionary<string, Error> _errors;

    public ErrorCatalog(IEnumerable<IErrorCatalogContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        _errors = new Dictionary<string, Error>(StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var contributor in contributors)
        {
            foreach (var error in contributor.GetErrors())
            {
                if (!error.Code.StartsWith(contributor.Prefix, StringComparison.Ordinal))
                {
                    failures.Add($"{error.Code} does not start with {contributor.Prefix}");
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
